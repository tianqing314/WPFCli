using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using Xmas11.Comm.Devices;

namespace TESTRIG.Devices.StandardBox;

/// <summary>
/// 动态测试标准盒**真机驱动**：走 Xmas11 通讯库直接驱动继电器（BNRC32 A/B、BNRC16 C）与电流计（ZH4402/ZH4412）。
/// 完整迁移自旧 <c>Bots.TestBench.Device.DynamicStandardTestBench</c>——继电器切换统一走
/// <see cref="RelayGearAsync"/>/<see cref="RelayCommandAsync"/> 的单张寄存器表（见 StandardBox.ConST326.cs），
/// 切档指令 <c>ZQWL_SetOutputCMD</c> 与旧一致。各子设备 IP/端口**独立**，均取自 connections.json。
/// 仅当 <c>TESTRIG_REAL_HARDWARE=1</c> 或配置 Pcba:Hardware:UseReal 时注册启用；否则用 <see cref="SimulatedStandardBox"/>。
/// </summary>
public sealed partial class StandardBox : IConST326StandardBox
{
    /// <summary>
    /// 继电器板地址占位。ZQWL <c>Adressint</c> 仅 485 通讯用；本项目全网络通讯，硬件忽略此参数，固定给 1。
    /// </summary>
    private const int NetRelayAddress = 1;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<StandardBox> _logger;

    /// <summary>
    /// 单台物理标准盒所有号位共用。并行号位下继电器/电流表 socket 操作必须串行，
    /// 否则同一 socket 并发写会错乱。用此闸串行化全部盒操作（连接/切档/读电流），保证指令原子、状态一致。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 继电器 A（32 路）。
    /// </summary>
    private BNRC32? _bnrc32A;

    /// <summary>
    /// 继电器 B（32 路）。
    /// </summary>
    private BNRC32? _bnrc32B;

    /// <summary>
    /// 继电器 C（16 路）。
    /// </summary>
    private BNRC16? _bnrc16C;

    /// <summary>
    /// 2 路电流表。
    /// </summary>
    private ZH44023D? _zh4402A;

    /// <summary>
    /// 12 路电流表。
    /// </summary>
    private ZH4412? _zh4412A;

    /// <summary>
    /// 连接配置（ConST326 串口 / DAM6803D 网络端点来源）。
    /// </summary>
    private readonly Core.Abstractions.ConnectionSettings _connections;

    /// <summary>
    /// 连接解析器：把串口物理链路号解析成当前实际 COM（COM 号会变、物理链路不变）。
    /// </summary>
    private readonly IConnectionResolver _resolver;

    /// <summary>
    /// 构造真机标准盒。全部子设备端点取自 <paramref name="connections"/>。
    /// </summary>
    /// <param name="connections">连接配置（各子设备 IP/端口）。</param>
    /// <param name="resolver">连接解析器（串口物理链路 → 当前 COM）。</param>
    /// <param name="logger">日志。</param>
    public StandardBox(Core.Abstractions.ConnectionSettings connections, IConnectionResolver resolver, ILogger<StandardBox> logger)
    {
        _logger = logger;
        _connections = connections;
        _resolver = resolver;
    }

    /// <summary>
    /// 设备键。
    /// </summary>
    public string Key => "DSTB";

    /// <summary>
    /// 设备型号名。
    /// </summary>
    public string Model => "DynamicStandardTestBench";

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 真机硬件标识（用于处理器决定是否插入真实建立延时）。
    /// </summary>
    public bool IsRealHardware => true;

    /// <summary>
    /// 连接标准盒各子设备（共享单例：多号位 Prep 只实连一次）。连接失败抛 <see cref="DeviceCommException"/>（HardwareError）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsConnected)
            {
                return;
            }

            // 各子设备独立 IP/端口，逐个从 connections.json 取（非单 IP+基址偏移）
            var (ipA, portA) = Endpoint("BNRC32A");
            var (ipB, portB) = Endpoint("BNRC32B");
            var (ipC, portC) = Endpoint("BNRC16C");
            var (ip2, port2) = Endpoint("ZH4402A");
            var (ip12, port12) = Endpoint("ZH4412A");
            _bnrc32A = new BNRC32(ipA, portA);
            _bnrc32B = new BNRC32(ipB, portB);
            _bnrc16C = new BNRC16(ipC, portC);
            _zh4402A = new ZH44023D(ip2, port2);
            _zh4412A = new ZH4412(ip12, port12);

            var ok =
                _bnrc32A.Open()
                && _bnrc32B.Open()
                && _bnrc16C.Open()
                && _zh4402A.Open()
                && _zh4412A.Open();
            IsConnected = ok;
            if (ok)
            {
                // PORT: 旧 DSTB.Open() 末尾同样设 12 路表刷新周期为 100ms。表内部按该周期积分，
                // 漏设会退回上电默认值，待机功耗这类占空比负载读到的是瞬时值而非 100ms 均值。
                _zh4412A.SetUpdateInterval(ZH4412.UpdateInterval._100ms);
                _logger.LogInformation("标准盒真机连接成功（继电器 {A}/{B}/{C}，电流计 {Z2}/{Z12}，12路表刷新 100ms）",
                    ipA, ipB, ipC, ip2, ip12);
            }
            else
            {
                _logger.LogError("标准盒真机连接失败（继电器 {A}/{B}/{C}，电流计 {Z2}/{Z12}）",
                    ipA, ipB, ipC, ip2, ip12);
                throw new DeviceCommException(
                    "标准盒连接失败（继电器/电流计，端点见 connections.json）",
                    TestResultStatus.HardwareError
                );
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 从连接配置按键解析一个网络子设备端点（继电器/电流计）。缺失或非网络端点即抛，避免用错默认地址。
    /// </summary>
    /// <param name="key">子设备键（BNRC32A/BNRC32B/BNRC16C/ZH4402A/ZH4412A）。</param>
    /// <returns>(IP, 端口)。</returns>
    private (IPAddress Ip, int Port) Endpoint(string key)
    {
        var sub = _connections.StandardBox.FirstOrDefault(s => s.Key == key);
        var ep = sub?.Comm;
        if (ep is null || ep.Link != LinkType.Ethernet || string.IsNullOrWhiteSpace(ep.Ip) || ep.Port is null)
        {
            throw new DeviceCommException($"标准盒子设备 {key} 缺少网络端点配置（connections.json）", TestResultStatus.HardwareError);
        }
        return (IPAddress.Parse(ep.Ip), ep.Port.Value);
    }

    /// <summary>
    /// 读电流表通道测量值（uA）。串行化执行。PORT: GetCurrentMeasureValue。
    /// </summary>
    /// <param name="is12Channel">true 走 12 路电流表，false 走 2 路电流表。</param>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流测量值（uA）。</returns>
    public async Task<double> GetCurrentMeasureValueAsync(bool is12Channel, int channel, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(
                () =>
                {
                    if (is12Channel)
                    {
                        if (channel is < 1 or > 12)
                        {
                            throw new ArgumentOutOfRangeException(nameof(channel), "12路电流表通道 1-12");
                        }
                        RefreshCurrents12();
                        return _ld12[channel - 1];
                    }
                    var r2 = _zh4402A!.GetChannelReading(channel);
                    double v = (double)r2.Result;
                    v *= 1000000;
                    return v;
                },
                ct
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 12 路电流表最近一次读数缓存（uA）。
    /// </summary>
    private readonly double[] _ld12 = new double[12];

    /// <summary>
    /// 12 路电流表缓存的刷新时刻。
    /// </summary>
    private DateTime _ld12UpdatedAt = DateTime.MinValue;

    /// <summary>
    /// 刷新 12 路电流表缓存。**完整 PORT: 旧 DSTB.UpdateCurrentStatus**——
    /// ① 距上次刷新不足 100ms 直接用缓存（表本身就是 100ms 刷新一次，读更快没有意义）；
    /// ② 应答为 null 或不足 12 路时重读，**绝不拿残缺列表按下标取值**
    /// （pcba 原实现直接 <c>(IList)r.Result</c> 再 <c>list[ch-1]</c>：为 null 时 NRE，
    /// 不足 12 路时会静默取到错位/未刷新的数，这正是待机功耗读数异常的可疑来源）。
    /// 旧代码是无限 while，这里加上限并抛异常，避免卡死。
    /// </summary>
    private void RefreshCurrents12()
    {
        if ((DateTime.Now - _ld12UpdatedAt).TotalMilliseconds < 100)
        {
            return;
        }

        System.Collections.IList? list = null;
        for (int i = 0; i < 50; i++)
        {
            list = _zh4412A!.GetChannelsReadingByRange(1, 12, ZH4412.Range.Automatic).Result as System.Collections.IList;
            if (list is not null && list.Count >= 12)
            {
                break;
            }

            list = null;
            Thread.Sleep(100);
        }

        if (list is null)
        {
            throw new DeviceCommException("12路电流表读数不完整（重试 50 次仍不足 12 路）", TestResultStatus.HardwareError);
        }

        for (int i = 0; i < 12; i++)
        {
            _ld12[i] = Convert.ToDouble(list[i]);
        }

        _ld12UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 按继电器 + 档位切档（IStandardBox 通用面，映射到统一的 <see cref="RelayGearAsync"/>）。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gearIndex">档位序号。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SwitchGearAsync(string relay, int gearIndex, CancellationToken ct = default)
    {
        switch (relay.ToUpperInvariant())
        {
            case "A":
                return RelayGearAsync('A', new[] { gearIndex }, ct);
            case "B":
                return RelayGearAsync('B', new[] { gearIndex }, ct);
            case "C":
                return RelayGearAsync('C', new[] { gearIndex }, ct);
            default:
                throw new ArgumentException($"未知继电器 {relay}");
        }
    }

    /// <summary>
    /// 通用上电（占位）。真机上/断电由设备特有处理器用 NetSwitch 精确序列实现，此处仅记录以保持接口完整。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    public Task PowerOnAsync(int channel, CancellationToken ct = default)
    {
        _logger.LogDebug("PowerOn 通道 {Ch}（真机由处理器 NetSwitch 序列控制）", channel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通用断电（占位）。真机上/断电由设备特有处理器用 NetSwitch 精确序列实现，此处仅记录以保持接口完整。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    public Task PowerOffAsync(int channel, CancellationToken ct = default)
    {
        _logger.LogDebug("PowerOff 通道 {Ch}（真机由处理器 NetSwitch 序列控制）", channel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通用电流读数（mA）：走 2 路电流表，uA→mA。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流（mA）。</returns>
    public async Task<double> ReadCurrentAsync(int channel, CancellationToken ct = default)
    {
        var ua = await GetCurrentMeasureValueAsync(false, channel, ct);
        return ua / 1000.0;
    }

    /// <summary>
    /// 关闭各子设备连接。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        try
        {
            _bnrc32A?.Close();
            _bnrc32B?.Close();
            _bnrc16C?.Close();
            _zh4402A?.Close();
            _zh4412A?.Close();
            _const326?.Close();
            _dam6803d?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "标准盒关闭异常");
        }
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
