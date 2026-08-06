namespace TESTRIG.Devices.Dut;

/// <summary>
/// 标注一个 <see cref="Abstractions.IDutDevice"/> 实现为某型号的被检驱动，供
/// <see cref="DutDriverRegistry.AutoRegisterFromAssembly"/> 反射自动注册——**新增被检驱动只需打此特性，
/// 不必再去 DevicesServiceCollectionExtensions 手写一行 Register**。
/// 驱动类须为非抽象、公开、含 <c>(DeviceDescriptor, ILogger)</c> 构造函数（与既有驱动一致）。
/// 一个型号可同时有真机实现与仿真实现（用 <see cref="IsSimulation"/> 区分）：真机开关开则用真机、否则用仿真；
/// 只有真机实现的板（如 ConST560/660/802）两种开关都用真机。一个驱动服务多型号时可重复标注（如 218A/218P 共用 ConST218ADut）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DutDriverAttribute : Attribute
{
    /// <summary>
    /// 用型号构造特性。
    /// </summary>
    /// <param name="model">设备型号（=manifest <c>Dut.Model</c> / 注册键，大小写不敏感）。</param>
    public DutDriverAttribute(string model)
    {
        Model = model;
    }

    /// <summary>
    /// 设备型号（=manifest <c>Dut.Model</c> / 注册键）。
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// 该实现是否为仿真驱动。true=仿真变体（真机开关关时用）；false（默认）=真机驱动。
    /// </summary>
    public bool IsSimulation { get; init; }
}
