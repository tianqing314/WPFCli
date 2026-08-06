using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using TESTRIG.Devices.Dut;

namespace TESTRIG.Devices;

/// <summary>
/// 共享设备连接管理：持有**全局共享**的标准盒与 PLC（国锐），统一连接/断开/查状态，
/// 并通过 <see cref="IConnectionResolver"/> 把配置里的物理链路解析到当前 COM/网络目标。
/// 支持一键连接（全部）与单独连接（按端点）。
/// </summary>
public sealed class ConnectionManager
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<ConnectionManager> _logger;

    /// <summary>
    /// 连接解析器（物理链路 → 当前目标）。
    /// </summary>
    private readonly IConnectionResolver _resolver;

    /// <summary>
    /// 被检驱动注册表（连通性测试按型号建被检驱动）。
    /// </summary>
    private readonly DutDriverRegistry _dutRegistry;

    /// <summary>
    /// 构造连接管理器。
    /// </summary>
    /// <param name="standardBox">全局共享标准盒。</param>
    /// <param name="plc">全局共享 PLC。</param>
    /// <param name="resolver">连接解析器。</param>
    /// <param name="dutRegistry">被检驱动注册表。</param>
    /// <param name="logger">日志。</param>
    public ConnectionManager(IStandardBox standardBox, IPlcController plc,
        IConnectionResolver resolver, DutDriverRegistry dutRegistry, ILogger<ConnectionManager> logger)
    {
        StandardBox = standardBox;
        Plc = plc;
        _resolver = resolver;
        _dutRegistry = dutRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 全局共享标准盒。
    /// </summary>
    public IStandardBox StandardBox { get; }

    /// <summary>
    /// 全局共享 PLC。
    /// </summary>
    public IPlcController Plc { get; }

    /// <summary>
    /// 标准盒是否已连接。
    /// </summary>
    public bool IsBoxConnected => StandardBox.IsConnected;

    /// <summary>
    /// PLC 是否已连接。
    /// </summary>
    public bool IsPlcConnected => Plc.IsConnected;

    /// <summary>
    /// 连接共享设备（标准盒 + PLC）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectSharedAsync(CancellationToken ct = default)
    {
        if (!StandardBox.IsConnected)
        {
            await StandardBox.ConnectAsync(ct);
        }

        if (!Plc.IsConnected)
        {
            await Plc.ConnectAsync(ct);
        }

        _logger.LogInformation("共享设备连接：标准盒={Box} PLC={Plc}", StandardBox.IsConnected, Plc.IsConnected);
    }

    /// <summary>
    /// 单独连接标准盒（含内部子设备）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectBoxAsync(CancellationToken ct = default)
    {
        if (!StandardBox.IsConnected)
        {
            await StandardBox.ConnectAsync(ct);
        }

        _logger.LogInformation("标准盒连接：{Box}", StandardBox.IsConnected);
    }

    /// <summary>
    /// 单独连接 PLC（国锐）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectPlcAsync(CancellationToken ct = default)
    {
        if (!Plc.IsConnected)
        {
            await Plc.ConnectAsync(ct);
        }

        _logger.LogInformation("PLC连接：{Plc}", Plc.IsConnected);
    }

    /// <summary>
    /// 单独连接/试连一个端点：解析物理链路并返回结果（仿真）。被检在任务开始时才上电，不在此处连接。
    /// </summary>
    /// <param name="endpoint">通讯端点。</param>
    /// <returns>解析结果。</returns>
    public ResolvedEndpoint TestConnect(CommEndpoint endpoint)
    {
        return _resolver.Resolve(endpoint);
    }

    /// <summary>
    /// **真连**标准盒某子设备：按键调驱动 <c>Open()</c>（+ConST326 <c>IsExist()</c>），测完关闭。
    /// 真机走硬件；仿真返回成功。用于连接配置页单设备/一键连接。
    /// </summary>
    /// <param name="key">子设备键（ConST326/BNRC32A/BNRC32B/BNRC16C/ZH4402A/ZH4412A/DAM6803D）。</param>
    /// <param name="endpoint">该子设备端点（页面当前值）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public Task<(bool Ok, string Message)> TestBoxSubDeviceAsync(string key, CommEndpoint endpoint, CancellationToken ct = default)
    {
        if (StandardBox is not IDynamicStandardBox box)
        {
            return Task.FromResult((false, "标准盒不支持子设备连通性测试"));
        }

        // 串口：物理链路号（如 "3.1"）不是 COM 名，先解析成当前实际 COM 再交驱动 Open。
        if (endpoint.Link == LinkType.Serial)
        {
            var r = _resolver.Resolve(endpoint);
            if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
            {
                return Task.FromResult((false, r.Message));
            }
            endpoint = endpoint with { PhysicalLink = r.Target };
        }

        return box.TestSubDeviceAsync(key, endpoint, ct);
    }

    /// <summary>
    /// **真连**某被检：按型号建驱动、用给定端点 <c>ConnectAsync</c> 探活，测完关闭（不占用；正式测试时由继电器上电后重连）。
    /// 串口端点先经解析器把物理链路转当前 COM。用于连接配置页被检行的单独连接/断开。
    /// </summary>
    /// <param name="descriptor">被检描述符（型号/键，来自 manifest.Dut）。</param>
    /// <param name="endpoint">该号位被检端点（页面当前值）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public async Task<(bool Ok, string Message)> TestDutAsync(DeviceDescriptor descriptor, CommEndpoint endpoint, CancellationToken ct = default)
    {
        var ep = endpoint;
        // 串口：物理链路号 → 当前实际 COM
        if (ep.Link == LinkType.Serial)
        {
            var r = _resolver.Resolve(ep);
            if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
            {
                return (false, r.Message);
            }
            ep = ep with { PhysicalLink = r.Target };
        }

        await using var dut = _dutRegistry.Create(descriptor with { Comm = ep });
        try
        {
            await dut.ConnectAsync(ct);
            return dut.IsConnected
                ? (true, $"被检连接成功（{ep.Describe()}）")
                : (false, $"被检连接失败（{ep.Describe()}），请确认已上电/接线");
        }
        catch (Exception ex)
        {
            return (false, $"被检连接异常：{ex.Message}");
        }
    }

    /// <summary>
    /// **针床工装子设备连通性测试**（针床继电器 D/E、2 路电流计等，均为串口）：物理链路号解析为当前 COM，
    /// 再做端口占用/存在探测。轻量校验「串口可用」，不做协议握手（真实建连在测试任务开始时由针床工装驱动完成）。
    /// 供连接配置页「针床工装」行的单独连接/断开。
    /// </summary>
    /// <param name="endpoint">针床工装子设备端点（页面当前值，串口）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否可用, 说明)。</returns>
    public Task<(bool Ok, string Message)> TestFixtureAsync(CommEndpoint endpoint, CancellationToken ct = default)
    {
        if (endpoint.Link != LinkType.Serial)
        {
            return Task.FromResult((false, "针床工装设备须为串口连接"));
        }

        var r = _resolver.Resolve(endpoint);
        if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
        {
            return Task.FromResult((false, r.Message));
        }

        var probe = SerialPortProbe.Probe(r.Target);
        return Task.FromResult(probe.Ok
            ? (true, $"串口可用（{r.Target}）")
            : (false, probe.Message));
    }

    /// <summary>
    /// 断开共享设备（标准盒 + PLC）。
    /// </summary>
    public async Task DisconnectSharedAsync()
    {
        await StandardBox.DisposeAsync();
        await Plc.DisposeAsync();
    }
}
