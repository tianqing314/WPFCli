using System.Reflection;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Dut;

namespace TESTRIG.Devices.StandardBox;

/// <summary>
/// 标准模块注册表：型号 → <see cref="IStandardModule"/> 工厂。新增标准设备（如正压/真空标准模块）
/// 只需给驱动类打 <see cref="DutDriverAttribute"/>（实现 <see cref="IStandardModule"/>）即可自动注册，
/// 无需改引擎/UI。未注册型号抛异常（标准模块不是仿真可替代的被检）。
/// </summary>
public sealed class StandardModuleRegistry
{
    /// <summary>
    /// 型号 → 标准模块驱动工厂（大小写不敏感）。
    /// </summary>
    private readonly Dictionary<string, Func<DeviceDescriptor, IStandardModule>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 日志工厂（供驱动建日志）。
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造标准模块注册表。
    /// </summary>
    /// <param name="loggerFactory">日志工厂。</param>
    public StandardModuleRegistry(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 注册某型号的标准模块驱动工厂。
    /// </summary>
    /// <param name="model">型号（=manifest ToolDevices 的 Model）。</param>
    /// <param name="factory">驱动工厂。</param>
    public void Register(string model, Func<DeviceDescriptor, IStandardModule> factory)
    {
        _factories[model] = factory;
    }

    /// <summary>
    /// 反射扫描本程序集（TESTRIG.Devices），把实现 <see cref="IStandardModule"/> 且打
    /// <see cref="DutDriverAttribute"/> 的标准模块驱动按型号自动注册——新增标准设备只需给驱动类打特性。
    /// </summary>
    public void AutoRegisterFromAssembly()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsClass || !typeof(IStandardModule).IsAssignableFrom(type))
            {
                continue;
            }
            var attr = type.GetCustomAttribute<DutDriverAttribute>();
            if (attr is null)
            {
                continue;
            }
            Register(attr.Model, d => (IStandardModule)Activator.CreateInstance(type, d, _loggerFactory.CreateLogger($"STD.{d.Model}"))!);
        }
    }

    /// <summary>
    /// 按描述符型号创建标准模块驱动。
    /// </summary>
    /// <param name="descriptor">设备描述符（Model 决定驱动，Comm 为连接端点）。</param>
    /// <returns>标准模块驱动实例。</returns>
    /// <exception cref="InvalidOperationException">型号未注册。</exception>
    public IStandardModule Create(DeviceDescriptor descriptor)
    {
        if (_factories.TryGetValue(descriptor.Model, out var factory))
        {
            return factory(descriptor);
        }
        throw new InvalidOperationException($"未注册标准模块驱动：{descriptor.Model}（实现 IStandardModule 并打 [DutDriver(\"{descriptor.Model}\")]）");
    }
}
