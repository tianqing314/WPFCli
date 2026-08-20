using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Dut;
using Xmas11.Comm.Data.Common;

namespace TESTRIG.Devices.StandardBox;

/// <summary>ConST811A GZP21 真实工装驱动。</summary>
[DutDriver("ConST811ATestTool")]
public sealed class ConST811ATestTool : IConST811ATestTool
{
    private readonly ILogger _logger;
    private readonly CommEndpoint? _comm;
    private Xmas11.Comm.Device.ConSTGZ811A? _dev;

    public ConST811ATestTool(DeviceDescriptor descriptor, ILogger logger)
    {
        Key = descriptor.Name;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
        _logger = logger;
    }

    public string Key { get; }
    public string Model { get; }
    public bool IsConnected { get; private set; }
    public bool IsRealHardware => true;
    /// <summary>设备是否已打开/连接（旧脚本 item.GetDevice("GZP21").IsOpen）。整机模板下 GetDevice 总返回实例，原 null 检查丢弃，IsOpen 复用 IsConnected 语义。</summary>
    public bool IsOpen => IsConnected;

    public Task ConnectAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        try { _dev?.Close(); } catch { }
        _dev = Build(_comm);
        IsConnected = _dev.Open() && _dev.IsExist();
        _logger.LogInformation(IsConnected ? "GZP21 工装连接成功" : "GZP21 工装连接未就绪");
    }, ct);

    private static Xmas11.Comm.Device.ConSTGZ811A Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet)
            return new Xmas11.Comm.Device.ConSTGZ811A(IPAddress.Parse(ep?.Ip ?? "192.168.40.107"), ep?.Port ?? 8899);
        if (ep.Link == LinkType.Serial)
        {
            var sp = ep.Serial ?? new SerialParams();
            var stop = Enum.TryParse<StopBits>(sp.StopBits, true, out var sb) ? sb : StopBits.One;
            var parity = Enum.TryParse<Parity>(sp.Parity, true, out var pa) ? pa : Parity.None;
            return new Xmas11.Comm.Device.ConSTGZ811A(ep.PhysicalLink ?? "COM1", sp.Baud, sp.DataBits, stop, parity);
        }
        throw new DeviceCommException("GZP21 不支持 USB 端点", TestResultStatus.CommunicationError);
    }

    private Xmas11.Comm.Device.ConSTGZ811A Dev => _dev ?? throw new DeviceCommException("GZP21 未连接", TestResultStatus.CommunicationError);
    private static void Check(Xmas11.Comm.Devices.iResponse response, string what)
    {
        if (!response.IsCorrect)
            throw new DeviceCommException($"{what}失败：{response.GetContent(true, true)}", TestResultStatus.HardwareError);
    }
    private static void Check<T>(Xmas11.Comm.Devices.iResponse<T> response, string what)
    {
        if (!response.IsCorrect)
            throw new DeviceCommException($"{what}失败：{response.GetContent(true, true)}", TestResultStatus.HardwareError);
    }
    private Task<bool> Set(Func<Xmas11.Comm.Devices.iResponse> call, string what, CancellationToken ct)
        => Task.Run(() => { var response = call(); Check(response, what); return true; }, ct);

    public Task<bool> Set27VAsync(bool open, CancellationToken ct = default)
        => Set(() => Dev.SetY1SwitchState(open ? OpenCloseState.Open : OpenCloseState.Close, 0), "GZP21 27V", ct);

    /// <summary>读 27V 电源当前开关状态（旧脚本 Gett27VState(out state)）。Open→true，Close/UnKnown→false。</summary>
    public Task<bool> Get27VStateAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var response = Dev.GetY1SwitchState();
            Check(response, "GZP21 27V 状态读取");
            return response.Result == OpenCloseState.Open;
        }, ct);
    public Task<bool> SetElectricalAsync(bool open, CancellationToken ct = default)
        => Set(() => Dev.SetY2SwitchState(open ? OpenCloseState.Open : OpenCloseState.Close, 0), "GZP21 电测", ct);
    public Task<bool> SetHartAsync(bool open, CancellationToken ct = default)
        => Set(() => Dev.SetY3SwitchState(open ? OpenCloseState.Open : OpenCloseState.Close, 0), "GZP21 HART", ct);
    public Task<bool> SetPaAsync(bool open, CancellationToken ct = default)
        => Set(() => Dev.SetY4SwitchState(open ? OpenCloseState.Open : OpenCloseState.Close, 0), "GZP21 PA", ct);
    public Task<bool> SetOutputAsync(string output, bool open, CancellationToken ct = default)
        => output.Equals("27V", StringComparison.OrdinalIgnoreCase) ? Set27VAsync(open, ct)
        : output.Equals("HART", StringComparison.OrdinalIgnoreCase) ? SetHartAsync(open, ct)
        : output.Equals("PA", StringComparison.OrdinalIgnoreCase) ? SetPaAsync(open, ct)
        : SetElectricalAsync(open, ct);
    public Task<double> ReadVoltageAsync(int channel = 0, CancellationToken ct = default)
        => throw new DeviceCommException("GZP21 没有电压测量通道", TestResultStatus.HardwareError);
    public Task<double> ReadCurrentAsync(int channel = 0, CancellationToken ct = default)
        => throw new DeviceCommException("GZP21 没有电流测量通道", TestResultStatus.HardwareError);
    public Task<string> GetSerialNumberAsync(CancellationToken ct = default) => Task.FromResult(Dev.GetSN());
    public Task<string> GetVersionAsync(CancellationToken ct = default) => Task.FromResult(Dev.ToString());
    public Task<bool> SetPressureTypeAsync(string pressureType, CancellationToken ct = default)
        => throw new DeviceCommException("GZP21 不提供压力类型设置", TestResultStatus.HardwareError);
    public Task<double> GetPressureKpaAsync(CancellationToken ct = default)
        => throw new DeviceCommException("GZP21 不提供压力读数", TestResultStatus.HardwareError);
    public Task<double> GetTemperatureAsync(CancellationToken ct = default)
        => throw new DeviceCommException("GZP21 不提供温度读数", TestResultStatus.HardwareError);
    public Task<bool> ResetAsync(CancellationToken ct = default)
        => Set(() => Dev.SetY1SwitchState(OpenCloseState.Close, 0), "GZP21 复位", ct);
    public ValueTask DisposeAsync() { try { _dev?.Close(); } catch { } IsConnected = false; return ValueTask.CompletedTask; }
}
