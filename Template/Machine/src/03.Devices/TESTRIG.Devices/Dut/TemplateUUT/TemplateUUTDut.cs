using System.Globalization;
using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.DPG2;

namespace TESTRIG.Devices.Dut.{{DutType}};

/// <summary>
/// {{DutType}} 被检**占位驱动**：走 Xmas11 <see cref="DPG2SCPI"/> 通讯库。
/// 接入真实产品时由 References 引擎自动生成（或按此模板手动扩展）。
/// 每条命令 <c>iResponse.IsCorrect=false</c> 即抛 <see cref="DeviceCommException"/>，交引擎按异常收尾。
/// </summary>
[DutDriver("{{DutType}}")]
public sealed class {{DutType}}Dut : I{{DutType}}Dut
{
    /// <summary>日志。</summary>
    private readonly ILogger _logger;

    /// <summary>连接端点（号位 Comm）。</summary>
    private readonly CommEndpoint? _comm;

    /// <summary>{{DutType}} 通讯实例（连接后有值）。</summary>
    private DPG2SCPI? _dev;

    /// <summary>设备键。</summary>
    public string Key { get; }

    /// <summary>设备型号名。</summary>
    public string Model { get; }

    /// <summary>是否已连接。</summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 取 {{DutType}} 实例，未连接抛 <see cref="DeviceCommException"/>（CommunicationError）。
    /// </summary>
    private DPG2SCPI Dev => _dev ?? throw new DeviceCommException("{{DutType}} 未连接", TestResultStatus.CommunicationError);

    /// <summary>
    /// 用设备描述符构造真机被检（端点取号位 Comm）。
    /// </summary>
    /// <param name="descriptor">设备描述符（含号位 Comm）。</param>
    /// <param name="logger">日志。</param>
    public {{DutType}}Dut(DeviceDescriptor descriptor, ILogger logger)
    {
        _logger = logger;
        Key = descriptor.Model;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
    }

    /// <summary>
    /// 连接被检：按端点（网络/串口）建 DPG2SCPI，Open 成功即连接成功。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try { _dev?.Close(); } catch { }
            _dev = Build(_comm);
            var opened = _dev.Open();
            IsConnected = opened;
            _logger.LogInformation(IsConnected ? "{{DutType}} 真机连接成功" : "{{DutType}} 连接未就绪（将重试）");
        }, ct);
    }

    /// <summary>
    /// 按端点构造 DPG2SCPI（网络/串口）。
    /// </summary>
    /// <param name="ep">连接端点。</param>
    /// <returns>通讯实例。</returns>
    private static DPG2SCPI Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet)
        {
            var ip = ep?.Ip ?? Environment.GetEnvironmentVariable("TESTRIG_DUT_IP") ?? "192.168.40.107";
            var port = ep?.Port ?? int.Parse(Environment.GetEnvironmentVariable("TESTRIG_DUT_PORT") ?? "1030", CultureInfo.InvariantCulture);
            return new DPG2SCPI(IPAddress.Parse(ip), port);
        }

        if (ep.Link == LinkType.Serial)
        {
            var sp = ep.Serial ?? new SerialParams();
            var portName = string.IsNullOrWhiteSpace(ep.PhysicalLink) ? "COM1" : ep.PhysicalLink!;
            var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;
            var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;
            return new DPG2SCPI(portName, sp.Baud, sp.DataBits, stopBits, parity);
        }

        throw new DeviceCommException("{{DutType}} 不支持 USB 连接", TestResultStatus.CommunicationError);
    }

    /// <summary>
    /// 补充连接（重连）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否连接成功。</returns>
    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)
    {
        await ConnectAsync(ct);
        return IsConnected;
    }

    // ===== IDutDevice 必需实现 =====

    /// <summary>读被检序列号。</summary>
    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)
        => Str(() => Dev.GetSerialNumber(), "读取SN", ct);

    /// <summary>读固件版本。</summary>
    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)
        => Str(() => Dev.GetVersion(), "读取版本", ct);

    /// <summary>写初始信息（占位空实现，接入时按需扩展）。</summary>
    public Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>读一路测量值（占位返回 0，接入时按需扩展）。</summary>
    public Task<double> MeasureAsync(string point, CancellationToken ct = default)
        => Task.FromResult(0d);

    /// <summary>设置被检序列号（占位空实现，接入时按需扩展）。</summary>
    public Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default)
        => Task.FromResult(true);

    /// <summary>设置产品型号/主设备类型（占位空实现，接入时按需扩展）。</summary>
    public Task<bool> SetPrimaryDeviceTypeAsync(string deviceType, CancellationToken ct = default)
        => Task.FromResult(true);

    /// <summary>通用布尔查询（占位返回 false，接入时按需扩展）。</summary>
    public Task<bool> QueryBooleanAsync(string method, object? arg, CancellationToken ct = default)
        => Task.FromResult(false);

    /// <summary>通用文本查询（占位返回空串，接入时按需扩展）。</summary>
    public Task<string> QueryTextAsync(string method, object? arg, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    /// <summary>通用指令执行（占位空实现，接入时按需扩展）。</summary>
    public Task CommandAsync(string method, object? arg, CancellationToken ct = default)
        => Task.CompletedTask;

    // ===== iResponse 包装：失败抛 DeviceCommException =====

    /// <summary>执行一条返回字符串的命令，失败抛通讯异常。</summary>
    private Task<string> Str(Func<iResponse<string>> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
                throw new DeviceCommException($"{what}失败", TestResultStatus.CommunicationError);
            return r.Result;
        }, ct);
    }

    /// <summary>释放连接。</summary>
    public ValueTask DisposeAsync()
    {
        try { _dev?.Close(); } catch { }
        _dev = null;
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
