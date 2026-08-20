using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using TESTRIG.Devices.Dut;
using TESTRIG.Devices.StandardBox;

namespace TESTRIG.Devices;

/// <summary>
/// 共享设备连接管理：整机模板的共享设备 = 标准模块（Tool 设备，如 DPSEX1 正压 / DPSEX2 真空标准模块），
/// 统一连接/断开/查状态，并通过 <see cref="IConnectionResolver"/> 把配置里的物理链路解析到当前 COM/网络目标。
/// 支持单独连接（按 DeviceKey）与端点试连。整机无 ConST326 标准盒、无 PLC。
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
    /// 标准模块注册表（共享设备：正压/真空标准模块按型号创建探活）。
    /// </summary>
    private readonly StandardModuleRegistry _stdRegistry;

    /// <summary>
    /// 构造连接管理器。
    /// </summary>
    /// <param name="resolver">连接解析器。</param>
    /// <param name="dutRegistry">被检驱动注册表。</param>
    /// <param name="stdRegistry">标准模块注册表。</param>
    /// <param name="logger">日志。</param>
    public ConnectionManager(IConnectionResolver resolver, DutDriverRegistry dutRegistry,
        StandardModuleRegistry stdRegistry, ILogger<ConnectionManager> logger)
    {
        _resolver = resolver;
        _dutRegistry = dutRegistry;
        _stdRegistry = stdRegistry;
        _logger = logger;
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
    /// **真连**被检：按型号建驱动并 Connect 探活，测完关闭。
    /// 用于连接配置页被检行的一键连接/单行连接。
    /// </summary>
    /// <param name="dut">被检描述符（型号/键）。</param>
    /// <param name="endpoint">被检端点（页面当前值）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public async Task<(bool Ok, string Message)> TestDutAsync(DeviceDescriptor dut, CommEndpoint endpoint, CancellationToken ct = default)
    {
        // 串口：物理链路号（如 DevSn）不是 COM 名，先解析成当前实际 COM 再交驱动 Open。
        if (endpoint.Link == LinkType.Serial)
        {
            var r = _resolver.Resolve(endpoint);
            if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
            {
                return (false, r.Message);
            }
            endpoint = endpoint with { PhysicalLink = r.Target };
        }

        var descriptor = dut with { Comm = endpoint };
        try
        {
            var dutDriver = _dutRegistry.Create(descriptor);
            await dutDriver.ConnectAsync(ct);
            var ok = dutDriver.IsConnected;
            await dutDriver.DisposeAsync();
            return (ok, ok ? "连通" : "连接失败");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "被检 {Model} 连通性测试失败", dut.Model);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// **真连**标准模块（如 DPSEX1 正压 / DPSEX2 真空）：按型号建驱动并 Connect 探活，测完关闭。
    /// 用于连接配置页单设备/一键连接。配置了 DevSn（<paramref name="expectedSn"/>）时，驱动连接后
    /// 读设备序列号比对，匹配才认为连接成功，否则关闭通讯端口（见 <c>DPSEXStandardModule.ConnectAsync</c>）。
    /// </summary>
    /// <param name="deviceKey">标准模块实例键（manifest ToolDevices 的 Key，如 DPSEX1）。</param>
    /// <param name="deviceName">设备名（仅显示）。</param>
    /// <param name="model">型号（驱动注册键，如 DPSEX）。</param>
    /// <param name="endpoint">该标准模块端点（页面当前值，物理链路为所选 COM）。</param>
    /// <param name="expectedSn">配置的设备序列号（DevSn；无则跳过序列号比对）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public async Task<(bool Ok, string Message)> TestStandardModuleAsync(
        string deviceKey, string deviceName, string model, CommEndpoint endpoint, string? expectedSn = null, CancellationToken ct = default)
    {
        // 串口：物理链路为所选 COM 名；配置 DevSn 时驱动连接后读设备序列号比对（见 DPSEXStandardModule.ConnectAsync）。
        if (endpoint.Link == LinkType.Serial)
        {
            var r = _resolver.Resolve(endpoint);
            if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
            {
                return (false, r.Message);
            }
            endpoint = endpoint with { PhysicalLink = r.Target };
        }

        var descriptor = new DeviceDescriptor(deviceName, model) { Comm = endpoint, SerialNumber = expectedSn };
        try
        {
            var module = _stdRegistry.Create(descriptor);
            await module.ConnectAsync(ct);
            var ok = module.IsConnected;
            await module.DisposeAsync();
            return (ok, ok ? "连通" : "连接失败");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "标准模块 {Key} 连通性测试失败", deviceKey);
            return (false, ex.Message);
        }
    }
}
