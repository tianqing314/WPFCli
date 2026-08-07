using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using TESTRIG.Devices.Dut;
using TESTRIG.Devices.StandardBox;

namespace TESTRIG.Devices;

/// <summary>
/// 按号位创建设备提供者：被检按 manifest 的型号经 <see cref="DutDriverRegistry"/> 创建；
/// 标准模块（共享设备，如 DPSEX1 正压 / DPSEX2 真空）按 manifest <c>ToolDevices</c> 经
/// <see cref="StandardModuleRegistry"/> 每号位创建实例，处理器按 DeviceKey 获取。
/// 整机模板无 ConST326 标准盒（与动态模板的差异）。
/// </summary>
public sealed class DeviceProviderFactory : IDeviceProviderFactory
{
    /// <summary>
    /// 被检驱动注册表。
    /// </summary>
    private readonly DutDriverRegistry _registry;

    /// <summary>
    /// 标准模块注册表。
    /// </summary>
    private readonly StandardModuleRegistry _stdRegistry;

    /// <summary>
    /// 连接配置（被检端点覆盖来源）。
    /// </summary>
    private readonly ConnectionSettings _connections;

    /// <summary>
    /// 连接解析器：把被检/标准模块串口物理链路号解析成当前实际 COM。
    /// </summary>
    private readonly IConnectionResolver _resolver;

    /// <summary>
    /// 共享设备配置仓储（测试项维护写入的 .shared.json；存在即取代 manifest 默认 ToolDevices）。
    /// </summary>
    private readonly ISharedDeviceStore _sharedStore;

    /// <summary>
    /// 构造设备提供者工厂。
    /// </summary>
    /// <param name="registry">被检驱动注册表。</param>
    /// <param name="stdRegistry">标准模块注册表。</param>
    /// <param name="connections">连接配置。</param>
    /// <param name="resolver">连接解析器（串口物理链路 → 当前 COM）。</param>
    /// <param name="sharedStore">共享设备配置仓储。</param>
    public DeviceProviderFactory(DutDriverRegistry registry, StandardModuleRegistry stdRegistry,
        ConnectionSettings connections,
        IConnectionResolver resolver, ISharedDeviceStore sharedStore)
    {
        _registry = registry;
        _stdRegistry = stdRegistry;
        _connections = connections;
        _resolver = resolver;
        _sharedStore = sharedStore;
    }

    /// <summary>
    /// 为某号位创建设备提供者：该号位专属被检实例 + 标准模块实例（端点按解析/覆盖处理）。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="position">号位。</param>
    /// <returns>该号位的设备提供者。</returns>
    public IDeviceProvider Create(JigManifest manifest, PositionDescriptor position)
    {
        // 被检连接优先级：connections.json 覆盖 > manifest 号位 Comm > Dut 旧默认
        var comm = position.Comm;
        if (_connections.Duts.TryGetValue(manifest.Key, out var list))
        {
            var i = position.Index - 1;
            if (i >= 0 && i < list.Count)
            {
                comm = list[i];
            }
        }
        // 串口被检：物理链路号 → 当前实际 COM（COM 号会变、物理链路不变）。解析成功才替换，
        // 失败则保留原值（连接时驱动 Open 会报错并触发重连）。网络/USB 不变。
        if (comm is not null && comm.Link == LinkType.Serial)
        {
            var r = _resolver.Resolve(comm);
            if (r.Ok && !string.IsNullOrWhiteSpace(r.Target))
            {
                comm = comm with { PhysicalLink = r.Target };
            }
        }

        var dut = comm is not null ? manifest.Dut with { Comm = comm } : manifest.Dut;

        // 标准模块（共享设备）：独立配置优先（测试项维护 .shared.json，存在即完全取代），否则 manifest 默认（References 转换）
        var toolSource = _sharedStore.Load(manifest.DeviceFamily, manifest.Key) ?? manifest.ToolDevices;
        var tools = new Dictionary<string, IStandardModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in toolSource)
        {
            var toolComm = tool.Comm;
            // 期望序列号 = 清单 SerialNumber（用户配置）；连接时读设备 SN 比对（见 DPSEXStandardModule.ConnectAsync）
            var expectedSn = tool.SerialNumber;
            if (toolComm is not null && toolComm.Link == LinkType.Serial)
            {
                var tr = _resolver.Resolve(toolComm);
                if (tr.Ok && !string.IsNullOrWhiteSpace(tr.Target))
                {
                    toolComm = toolComm with { PhysicalLink = tr.Target };
                }
            }
            var descriptor = new DeviceDescriptor(tool.Name, tool.Model) { Comm = toolComm, SerialNumber = expectedSn };
            tools[tool.Key] = _stdRegistry.Create(descriptor);
        }
        return new DeviceProvider(_registry.Create(dut), tools);
    }

    /// <summary>
    /// 单号位设备提供者：持有共享标准盒 + 该号位被检，按类型解析。
    /// </summary>
    private sealed class DeviceProvider : IDeviceProvider
    {
        /// <summary>
        /// 该号位被检。
        /// </summary>
        private readonly IDutDevice _dut;

        /// <summary>
        /// 该号位标准模块实例表（按 manifest ToolDevices 的 Key）。
        /// </summary>
        private readonly IReadOnlyDictionary<string, IStandardModule> _tools;

        /// <summary>
        /// 构造设备提供者。
        /// </summary>
        /// <param name="dut">该号位被检。</param>
        /// <param name="tools">该号位标准模块实例表。</param>
        public DeviceProvider(IDutDevice dut, IReadOnlyDictionary<string, IStandardModule> tools)
        {
            _dut = dut;
            _tools = tools;
        }

        /// <summary>
        /// 按类型解析设备（该号位被检）。
        /// </summary>
        /// <typeparam name="T">设备接口类型。</typeparam>
        /// <returns>设备实例。</returns>
        /// <exception cref="InvalidOperationException">该号位未提供此类型设备。</exception>
        public T GetDevice<T>()
            where T : class, IDevice
        {
            if (_dut is T dut)
            {
                return dut;
            }

            throw new InvalidOperationException($"该号位未提供 {typeof(T).Name} 设备（标准模块请用 GetDevice<T>(deviceKey)）");
        }

        /// <summary>
        /// 按类型 + 实例键解析设备（标准模块等多实例设备）。
        /// </summary>
        /// <typeparam name="T">设备接口类型。</typeparam>
        /// <param name="deviceKey">实例键（manifest ToolDevices 的 Key，如 DPSEX1）。</param>
        /// <returns>设备实例。</returns>
        /// <exception cref="InvalidOperationException">未找到该键的标准模块或类型不匹配。</exception>
        public T GetDevice<T>(string deviceKey)
            where T : class, IDevice
        {
            if (_tools.TryGetValue(deviceKey, out var module) && module is T typed)
            {
                return typed;
            }
            throw new InvalidOperationException($"未找到标准模块「{deviceKey}」或类型不匹配 {typeof(T).Name}");
        }

        /// <summary>
        /// 释放**该号位被检与标准模块**的连接（串口/网络），避免下一次运行新建时端口仍被占用（如串口 COMx）。
        /// 共享标准盒/PLC 是全局单例，**不**在此释放。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _dut.DisposeAsync();
            foreach (var module in _tools.Values)
            {
                await module.DisposeAsync();
            }
        }
    }
}
