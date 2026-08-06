using System.Reflection;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Dut;

/// <summary>
/// 被检驱动注册表：设备型号 → IDutDevice 工厂。新设备真实驱动在此按型号注册一行即可，
/// 无需改引擎/UI。未注册型号回落到通用仿真，保证本轮全仿真也能跑任意新板。
/// </summary>
public sealed class DutDriverRegistry
{
    /// <summary>
    /// 型号 → 驱动工厂（大小写不敏感）。
    /// </summary>
    private readonly Dictionary<string, Func<DeviceDescriptor, IDutDevice>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 日志工厂（供回落仿真驱动建日志）。
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造被检驱动注册表。
    /// </summary>
    /// <param name="loggerFactory">日志工厂。</param>
    public DutDriverRegistry(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 注册某型号的真实驱动工厂。
    /// </summary>
    /// <param name="model">设备型号。</param>
    /// <param name="factory">驱动工厂。</param>
    public void Register(string model, Func<DeviceDescriptor, IDutDevice> factory)
    {
        _factories[model] = factory;
    }

    /// <summary>
    /// 反射扫描本程序集（TESTRIG.Devices），把所有打了 <see cref="DutDriverAttribute"/> 的被检驱动按型号自动注册——
    /// 新增被检驱动只需给类打特性，无需再手写 Register。同一型号若同时有真机与仿真实现，按 <paramref name="useReal"/>
    /// 择一（真机开关关且无仿真变体时回落真机实现，对齐纯真机板 560/660/802 的既有行为）。
    /// 驱动须为非抽象公开类、实现 <see cref="IDutDevice"/>、含 <c>(DeviceDescriptor, ILogger)</c> 构造函数。
    /// </summary>
    /// <param name="useReal">是否用真机驱动。</param>
    public void AutoRegisterFromAssembly(bool useReal)
    {
        var candidates = typeof(DutDriverRegistry).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDutDevice).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<DutDriverAttribute>().Select(a => (a.Model, a.IsSimulation, Type: t)));

        foreach (var group in candidates.GroupBy(x => x.Model, StringComparer.OrdinalIgnoreCase))
        {
            var real = group.FirstOrDefault(x => !x.IsSimulation).Type;
            var sim = group.FirstOrDefault(x => x.IsSimulation).Type;
            var chosen = useReal ? real ?? sim : sim ?? real;
            if (chosen is null)
            {
                continue;
            }

            var type = chosen;
            var model = group.Key;
            Register(model, d => (IDutDevice)Activator.CreateInstance(type, d, _loggerFactory.CreateLogger($"DUT.{d.Model}"))!);
        }
    }

    /// <summary>
    /// 按描述符型号创建被检驱动；未注册型号回落通用仿真。
    /// </summary>
    /// <param name="descriptor">设备描述符。</param>
    /// <returns>被检驱动实例。</returns>
    public IDutDevice Create(DeviceDescriptor descriptor)
    {
        if (_factories.TryGetValue(descriptor.Model, out var factory))
        {
            return factory(descriptor);
        }
        // 未注册型号 -> 通用仿真（本轮全仿真）
        return new SimulatedDut(descriptor, _loggerFactory.CreateLogger($"DUT.{descriptor.Model}"));
    }
}
