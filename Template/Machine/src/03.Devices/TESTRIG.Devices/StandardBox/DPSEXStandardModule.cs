using System.Globalization;
using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Dut;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;

namespace TESTRIG.Devices.StandardBox;

/// <summary>
/// DPSEX 正压/真空标准模块驱动（整机模板标准设备示例）。**人工填充**自旧
/// <c>Bots.TestBench.Device.DPSEX</c>（StandardBox 目录）：串口走 Xmas11 <c>DPSEXBase</c>，
/// 作造压标准源 / 校准标准表。命令失败抛 <see cref="DeviceCommException"/>。
/// 其他标准设备接入：实现 <see cref="IStandardModule"/> 并打 <c>[DutDriver("型号")]</c> 即可自动注册。
/// </summary>
[DutDriver("DPSEX")]
public sealed class DPSEXStandardModule : IStandardModule
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// DPSEX 通讯实例（真机）。
    /// </summary>
    private DPSEXBase? _dev;

    /// <summary>
    /// 号位连接端点。
    /// </summary>
    private readonly CommEndpoint? _comm;

    /// <summary>
    /// 期望设备序列号（配置 DevSn 时非空，连接时读设备 SN 比对，不匹配即断开）。
    /// </summary>
    private readonly string? _expectedSn;

    /// <summary>
    /// 设备键（=manifest ToolDevices Key，如 DPSEX1/DPSEX2）。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 型号（DPSEX）。
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 构造 DPSEX 标准模块驱动。
    /// </summary>
    /// <param name="descriptor">设备描述符（Model 决定驱动，Comm 为连接端点）。</param>
    /// <param name="logger">日志。</param>
    public DPSEXStandardModule(DeviceDescriptor descriptor, ILogger logger)
    {
        _logger = logger;
        Key = descriptor.Model;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
        _expectedSn = descriptor.SerialNumber;
    }

    /// <summary>
    /// 连接标准模块：按端点（串口）建 DPSEXBase，Open 探活；
    /// 配置了 DevSn 时读设备序列号比对，匹配才认为连接成功，否则关闭连接（参考旧 Bots.TestBench DPSEX.Open）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try { _dev?.Close(); } catch { }
            _dev = Build(_comm);
            var opened = _dev.Open();
            var ready = opened && _dev.Connected && _dev.IsExist();
            if (ready && !string.IsNullOrWhiteSpace(_expectedSn))
            {
                // 配置了 DevSn：读设备序列号比对，匹配才认为连接成功，否则关闭连接
                var r = _dev.GetDeviceSerialNumber();
                var sn = r.IsCorrect ? r.Result?.Trim() : "";
                if (MatchSerial(_expectedSn, sn))
                {
                    IsConnected = true;
                    _logger.LogInformation("DPSEX 标准模块连接成功（SN {Sn} 匹配配置）", sn);
                }
                else
                {
                    try { _dev.Close(); } catch { }
                    IsConnected = false;
                    _logger.LogWarning("DPSEX 标准模块序列号不匹配：期望 {Expected}，读到 {Actual}，已断开", _expectedSn, sn);
                }
            }
            else
            {
                IsConnected = ready;
                _logger.LogInformation(IsConnected ? "DPSEX 标准模块连接成功" : "DPSEX 标准模块连接未就绪（将重试）");
            }
        }, ct);
    }

    /// <summary>
    /// 序列号匹配（与旧 DPSEX.Open 相同：相等或配置 DevSn 包含读值，忽略大小写/空白）。
    /// </summary>
    /// <param name="expected">配置 DevSn。</param>
    /// <param name="actual">设备读回的序列号。</param>
    /// <returns>是否匹配。</returns>
    private static bool MatchSerial(string expected, string? actual)
        => !string.IsNullOrWhiteSpace(actual)
           && (string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase)
               || expected.ToLowerInvariant().Contains(actual.ToLowerInvariant().Trim()));

    /// <summary>
    /// 按端点构造 DPSEXBase（标准模块默认串口 4800/Two/None；网络预留）。
    /// </summary>
    /// <param name="ep">连接端点。</param>
    /// <returns>通讯实例。</returns>
    private static DPSEXBase Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet)
        {
            var ip = ep?.Ip ?? Environment.GetEnvironmentVariable("TESTRIG_STD_IP") ?? "192.168.40.110";
            var port = ep?.Port ?? int.Parse(Environment.GetEnvironmentVariable("TESTRIG_STD_PORT") ?? "1030", CultureInfo.InvariantCulture);
            return new DPSEXBase(IPAddress.Parse(ip), port);
        }

        var sp = ep.Serial ?? new SerialParams();
        var portName = string.IsNullOrWhiteSpace(ep.PhysicalLink) ? "COM2" : ep.PhysicalLink!;
        var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.Two;
        var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;
        return new DPSEXBase(portName, sp.Baud, sp.DataBits, stopBits, parity);
    }

    /// <summary>
    /// 通讯实例（未连接抛通讯异常）。
    /// </summary>
    private DPSEXBase Dev => _dev ?? throw new DeviceCommException("DPSEX 标准模块未连接", TestResultStatus.CommunicationError);

    /// <summary>
    /// iResponse 失败抛通讯异常。
    /// </summary>
    private static void Check(iResponse r, string what)
    {
        if (!r.IsCorrect)
        {
            throw new DeviceCommException($"{what}失败：{r.ErrorCode} {r.GetContent(true, true)}", TestResultStatus.HardwareError);
        }
    }

    /// <summary>
    /// 压力类型字符串 → 枚举（正压=表压 G，真空=V）。
    /// </summary>
    private static PressureType ToType(string pressureType)
        => pressureType.Equals("Vacuum", StringComparison.OrdinalIgnoreCase) ? PressureType.V : PressureType.G;

    /// <summary>读标准模块序列号。</summary>
    public Task<string> GetSerialNumberAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetDeviceSerialNumber();
            Check(r, "读 DPSEX 序列号");
            return r.Result;
        }, ct);

    /// <summary>读标准模块版本号。</summary>
    public Task<string> GetVersionAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetiVersion();
            Check(r, "读 DPSEX 版本");
            var v = r.Result;
            return $"{v.MajorVersion}.{v.MinorVersion}.{v.BuildNumber}.{v.Revision}";
        }, ct);

    /// <summary>设置压力类型（正压/真空）。</summary>
    public Task<bool> SetPressureTypeAsync(string pressureType, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetPressureType(ToType(pressureType)), "设置 DPSEX 压力类型");
            return true;
        }, ct);

    /// <summary>读标准压力（kPa）。</summary>
    public Task<double> GetPressureKpaAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetPressure();
            Check(r, "读 DPSEX 标准压力");
            return r.Result.Value;
        }, ct);

    /// <summary>读模块温度（℃）。</summary>
    public Task<double> GetTemperatureAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetTemperature();
            Check(r, "读 DPSEX 温度");
            return r.Result.Value;
        }, ct);

    /// <summary>复位标准模块。</summary>
    public Task<bool> ResetAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SoftReset(), "复位 DPSEX");
            return true;
        }, ct);

    /// <summary>
    /// 释放通讯连接。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        try { _dev?.Close(); } catch { }
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
