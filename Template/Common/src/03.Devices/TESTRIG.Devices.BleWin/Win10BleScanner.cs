using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TESTRIG.Devices.Abstractions;
using Windows.Devices.Bluetooth.Advertisement;

namespace TESTRIG.Devices.BleWin;

/// <summary>
/// 上位机 Win10 BLE 扫描：用 WinRT <see cref="BluetoothLEAdvertisementWatcher"/> 监听广播，
/// 按 MAC（或名称）命中取 RSSI。取代旧 <c>Win10BLEDeviceHelper.SeBleDeviceAsync</c>。
///
/// **并行要点**：整机只有一个蓝牙适配器。早先每次扫描都新建一个 watcher，4 个号位同时测蓝牙就会有
/// 4 个 watcher 抢同一个射频、互相打断，表现为「蓝牙扫描超时」。现改为**全局常驻单个 watcher**——
/// 所有号位共用它收到的广播，各自按自己的 MAC/名称从缓存里取最新一条，既不排队也不互相干扰。
/// </summary>
public sealed class Win10BleScanner : IBleScanner
{
    /// <summary>
    /// 全局唯一的广播监听器（懒启动、常驻）。
    /// </summary>
    private static BluetoothLEAdvertisementWatcher? _watcher;

    /// <summary>
    /// 启动 <see cref="_watcher"/> 的互斥锁。
    /// </summary>
    private static readonly object WatcherLock = new();

    /// <summary>
    /// 按 MAC 地址缓存的最近一条广播（RSSI + 收到时刻）。
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, (int Rssi, DateTime At)> ByAddress = new();

    /// <summary>
    /// 按广播名称缓存的最近一条广播（RSSI + 收到时刻）。MAC 解析不出时的兜底。
    /// </summary>
    private static readonly ConcurrentDictionary<string, (int Rssi, DateTime At)> ByName = new();

    /// <summary>
    /// 单次扫描内部重试轮数。**按现场要求设为 1（单次 20s，不做任何重试层）**。
    /// 旧平台 SeBleDeviceAsync 的 maxRetry=3，如需恢复 3 轮把这里改回 3 即可。
    /// </summary>
    private const int MaxAttempts = 1;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<Win10BleScanner> _logger;

    /// <summary>
    /// 构造 Win10 BLE 扫描器。
    /// </summary>
    /// <param name="logger">日志。</param>
    public Win10BleScanner(ILogger<Win10BleScanner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 确保全局 watcher 处于 Started（幂等）。
    /// **常驻 watcher 的关键风险**：WinRT 的 watcher 可能被系统中止（Aborted）或停止（Stopped），
    /// 一旦如此，后续所有号位的扫描都会永久超时。这里每次扫描前检查状态，非 Started 就重建重启。
    /// </summary>
    private void EnsureWatcher()
    {
        var w0 = _watcher;
        if (w0 is not null && w0.Status is BluetoothLEAdvertisementWatcherStatus.Started
                                        or BluetoothLEAdvertisementWatcherStatus.Created)
        {
            return;
        }

        lock (WatcherLock)
        {
            var cur = _watcher;
            if (cur is not null && cur.Status is BluetoothLEAdvertisementWatcherStatus.Started
                                              or BluetoothLEAdvertisementWatcherStatus.Created)
            {
                return;
            }

            if (cur is not null)
            {
                _logger.LogWarning("BLE 全局监听状态异常（{Status}），重建", cur.Status);
                try { cur.Stop(); } catch { /* 已停止 */ }
            }

            var w = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            w.Received += (_, e) =>
            {
                var now = DateTime.Now;
                ByAddress[e.BluetoothAddress] = (e.RawSignalStrengthInDBm, now);
                var ln = e.Advertisement?.LocalName;
                if (!string.IsNullOrEmpty(ln))
                {
                    ByName[ln] = (e.RawSignalStrengthInDBm, now);
                }
            };
            try
            {
                w.Start();
                _watcher = w;
                _logger.LogInformation("BLE 全局广播监听已启动（多号位共用）");
            }
            catch (Exception ex)
            {
                // 启动失败（无适配器/被禁用/驱动异常）不抛：让扫描按超时返回 null，由处理器判不合格
                _logger.LogWarning(ex, "BLE 全局广播监听启动失败");
                _watcher = null;
            }
        }
    }

    /// <summary>
    /// 扫描目标 BLE 并取 RSSI：从全局广播缓存里等一条**本次扫描开始之后**收到的记录，
    /// 优先按 MAC 命中，MAC 解析不出时按名称兜底。超时/取消返回 null。
    /// </summary>
    /// <param name="name">蓝牙名称。</param>
    /// <param name="mac">蓝牙 MAC（12 位十六进制）。</param>
    /// <param name="timeoutSeconds">扫描超时（秒）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>RSSI(dBm)，超时/取消返回 null。</returns>
    public async Task<int?> ScanRssiAsync(string name, string mac, int timeoutSeconds = 20, CancellationToken ct = default)
    {
        var target = ParseMac(mac);
        var per = Math.Max(1, timeoutSeconds);

        // PORT: 旧 Win10BLEDeviceHelper.SeBleDeviceAsync(mac, names, timeout, maxRetry: 3)——内部就重试 3 轮。
        // 每轮开始前检查并按需重启全局 watcher，避免它被系统中止后所有号位永久扫不到。
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            EnsureWatcher();

            // 宽限 2s：全局 watcher 常驻，目标广播可能刚好在本次调用前一瞬收到，不该因此白等一轮
            var since = DateTime.Now.AddSeconds(-2);
            var deadline = DateTime.Now.AddSeconds(per);

            while (DateTime.Now < deadline)
            {
                if (ct.IsCancellationRequested)
                {
                    return null;
                }

                if (target != 0 && ByAddress.TryGetValue(target, out var a) && a.At >= since)
                {
                    _logger.LogInformation("BLE 扫描 {Name}/{Mac} → {Rssi}（第 {N} 轮）", name, mac, a.Rssi, attempt);
                    return a.Rssi;
                }

                if (target == 0 && !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out var b) && b.At >= since)
                {
                    _logger.LogInformation("BLE 扫描 {Name}（按名称）→ {Rssi}（第 {N} 轮）", name, b.Rssi, attempt);
                    return b.Rssi;
                }

                try
                {
                    await Task.Delay(100, ct);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            _logger.LogWarning("BLE 扫描 {Name}/{Mac} 第 {N}/{Max} 轮超时（{Sec}s）", name, mac, attempt, MaxAttempts, per);
            if (attempt < MaxAttempts)
            {
                try
                {
                    await Task.Delay(500, ct);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        _logger.LogWarning("BLE 扫描 {Name}/{Mac} → {Max} 轮共 {Total}s 均未收到广播", name, mac, MaxAttempts, MaxAttempts * per);
        return null;
    }

    /// <summary>
    /// MAC 串（"0123456789AB"）→ WinRT ulong 地址。
    /// </summary>
    /// <param name="mac">MAC 串。</param>
    /// <returns>ulong 地址，解析失败为 0。</returns>
    private static ulong ParseMac(string mac)
    {
        var hex = new string((mac ?? "").Where(Uri.IsHexDigit).ToArray());
        return ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
