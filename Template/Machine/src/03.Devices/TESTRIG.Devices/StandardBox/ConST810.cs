using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Dut;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;

namespace TESTRIG.Devices.StandardBox;

/// <summary>ConST810 真实共享设备驱动。</summary>
[DutDriver("ConST810")]
public sealed class ConST810 : IConST810
{
    private readonly ILogger _logger;
    private readonly CommEndpoint? _comm;
    private HPC? _dev;
    public ConST810(DeviceDescriptor descriptor, ILogger logger) { Key = descriptor.Name; Model = descriptor.Model; _comm = descriptor.Comm; _logger = logger; }
    public string Key { get; }
    public string Model { get; }
    public bool IsConnected { get; private set; }
    public bool IsRealHardware => true;
    public Task ConnectAsync(CancellationToken ct = default) => Task.Run(() => { try { _dev?.Close(); } catch { } _dev = Build(_comm); IsConnected = _dev.Open() && _dev.IsExist(); _logger.LogInformation(IsConnected ? "P06/ConST810 连接成功" : "P06/ConST810 连接未就绪"); }, ct);
    private static HPC Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet) return new HPC(IPAddress.Parse(ep?.Ip ?? "192.168.40.107"), ep?.Port ?? 8000);
        if (ep.Link == LinkType.Usb) return new HPC((ushort)(ep.Vid ?? 0x2E19), (ushort)(ep.Pid ?? 0x02F8), ep.PhysicalLink ?? "");
        var sp = ep.Serial ?? new SerialParams();
        var stop = Enum.TryParse<StopBits>(sp.StopBits, true, out var sb) ? sb : StopBits.One;
        var parity = Enum.TryParse<Parity>(sp.Parity, true, out var pa) ? pa : Parity.None;
        return new HPC(ep.PhysicalLink ?? "COM1", sp.Baud, sp.DataBits, stop, parity);
    }
    private HPC Dev => _dev ?? throw new DeviceCommException("ConST810 未连接", TestResultStatus.CommunicationError);
    private static void Check(Xmas11.Comm.Devices.iResponse response, string what) { if (!response.IsCorrect) throw new DeviceCommException($"{what}失败：{response.GetContent(true, true)}", TestResultStatus.HardwareError); }
    public Task<bool> SetOutputAsync(string output, bool open, CancellationToken ct = default) => Task.Run(() => { Check(Dev.SetDeviceSwitchState(open ? OpenCloseState.Open : OpenCloseState.Close), "ConST810 输出"); return true; }, ct);
    public Task<double> ReadVoltageAsync(int channel = 0, CancellationToken ct = default) => Task.Run(() => { var r = Dev.GetVoltageCheckStata(); Check(r, "ConST810 电压"); return r.Result.Count > channel ? r.Result[channel] : throw new DeviceCommException($"ConST810 电压通道 {channel} 不存在", TestResultStatus.HardwareError); }, ct);
    public Task<double> ReadCurrentAsync(int channel = 0, CancellationToken ct = default) => Task.Run(() => { var r = Dev.GetCurrentCheckStata(); Check(r, "ConST810 电流"); return r.Result.Count > channel ? r.Result[channel] : throw new DeviceCommException($"ConST810 电流通道 {channel} 不存在", TestResultStatus.HardwareError); }, ct);
    public Task<IReadOnlyList<double>> ReadVoltageSamplesAsync(CancellationToken ct = default) => Task.Run<IReadOnlyList<double>>(() => { var r = Dev.GetVoltageCheckStata(); Check(r, "ConST810 电压"); return r.Result; }, ct);
    public Task<IReadOnlyList<double>> ReadCurrentSamplesAsync(CancellationToken ct = default) => Task.Run<IReadOnlyList<double>>(() => { var r = Dev.GetCurrentCheckStata(); Check(r, "ConST810 电流"); return r.Result; }, ct);
    public Task<string> GetSerialNumberAsync(CancellationToken ct = default) => Task.Run(() => { var r = Dev.GetSerialNumber(); Check(r, "ConST810 SN"); return r.Result; }, ct);
    public Task<string> GetVersionAsync(CancellationToken ct = default) => Task.Run(() => { var r = Dev.GetVersion(); Check(r, "ConST810 版本"); return r.Result; }, ct);
    public Task<bool> SetPressureTypeAsync(string pressureType, CancellationToken ct = default) => throw new DeviceCommException("ConST810 不提供压力类型设置", TestResultStatus.HardwareError);
    public Task<double> GetPressureKpaAsync(CancellationToken ct = default) => throw new DeviceCommException("ConST810 不提供压力读数", TestResultStatus.HardwareError);
    public Task<double> GetTemperatureAsync(CancellationToken ct = default) => throw new DeviceCommException("ConST810 不提供温度读数", TestResultStatus.HardwareError);
    public Task<bool> ResetAsync(CancellationToken ct = default) => Task.Run(() => { Check(Dev.Reset(), "ConST810 复位"); return true; }, ct);
    public ValueTask DisposeAsync() { try { _dev?.Close(); } catch { } IsConnected = false; return ValueTask.CompletedTask; }
}
