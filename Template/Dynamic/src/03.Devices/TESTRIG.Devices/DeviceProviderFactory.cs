using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using TESTRIG.Devices.Dut;

namespace TESTRIG.Devices;

/// <summary>
/// 按号位创建设备提供者：标准盒取**全局共享单例**（纠正第一轮 per-module scoped 的错误），
/// 被检按 manifest 的型号经 <see cref="DutDriverRegistry"/> 创建。
/// </summary>
public sealed class DeviceProviderFactory : IDeviceProviderFactory
{
    /// <summary>
    /// 全局共享标准盒。
    /// </summary>
    private readonly IStandardBox _standardBox;

    /// <summary>
    /// 被检驱动注册表。
    /// </summary>
    private readonly DutDriverRegistry _registry;

    /// <summary>
    /// 连接配置（被检端点覆盖来源）。
    /// </summary>
    private readonly ConnectionSettings _connections;

    /// <summary>
    /// 连接解析器：把被检串口物理链路号解析成当前实际 COM。
    /// </summary>
    private readonly IConnectionResolver _resolver;

    /// <summary>
    /// 构造设备提供者工厂。
    /// </summary>
    /// <param name="standardBox">全局共享标准盒。</param>
    /// <param name="registry">被检驱动注册表。</param>
    /// <param name="connections">连接配置。</param>
    /// <param name="resolver">连接解析器（串口物理链路 → 当前 COM）。</param>
    public DeviceProviderFactory(IStandardBox standardBox, DutDriverRegistry registry, ConnectionSettings connections,
        IConnectionResolver resolver)
    {
        _standardBox = standardBox;
        _registry = registry;
        _connections = connections;
        _resolver = resolver;
    }

    /// <summary>
    /// 为某号位创建设备提供者：共享标准盒 + 该号位专属被检实例（端点按优先级解析）。
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
        return new DeviceProvider(_standardBox, _registry.Create(dut));
    }

    /// <summary>
    /// 单号位设备提供者：持有共享标准盒 + 该号位被检，按类型解析。
    /// </summary>
    private sealed class DeviceProvider : IDeviceProvider
    {
        /// <summary>
        /// 共享标准盒。
        /// </summary>
        private readonly IStandardBox _box;

        /// <summary>
        /// 该号位被检。
        /// </summary>
        private readonly IDutDevice _dut;

        /// <summary>
        /// 构造设备提供者。
        /// </summary>
        /// <param name="box">共享标准盒。</param>
        /// <param name="dut">该号位被检。</param>
        public DeviceProvider(IStandardBox box, IDutDevice dut)
        {
            _box = box;
            _dut = dut;
        }

        /// <summary>
        /// 按类型解析设备（标准盒或被检）。
        /// </summary>
        /// <typeparam name="T">设备接口类型。</typeparam>
        /// <returns>设备实例。</returns>
        /// <exception cref="InvalidOperationException">该号位未提供此类型设备。</exception>
        public T GetDevice<T>()
            where T : class, IDevice
        {
            if (_box is T box)
            {
                return box;
            }

            if (_dut is T dut)
            {
                return dut;
            }

            throw new InvalidOperationException($"当前号位未提供设备 {typeof(T).Name}");
        }

        /// <summary>
        /// 释放**该号位被检**的连接（串口/网络），避免下一次运行新建被检时端口仍被占用（如串口 COMx）。
        /// 共享标准盒/PLC 是全局单例，**不**在此释放。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            return _dut.DisposeAsync();
        }
    }
}
