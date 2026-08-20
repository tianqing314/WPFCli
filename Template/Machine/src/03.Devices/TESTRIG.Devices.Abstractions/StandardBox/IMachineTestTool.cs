using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 整机测试共享设备的最小契约。具体型号仍按 manifest ToolDevices 的 Key 获取，
/// 避免把动态/整机模板中不相关的 ConST326 标准盒接口带入整机产品。
/// </summary>
public interface IMachineTestTool : IStandardModule
{
    /// <summary>始终为真；整机共享设备没有动态模板的仿真标准盒。</summary>
    bool IsRealHardware { get; }
    /// <summary>执行继电器/开关操作。具体设备负责把名称映射到硬件通道。</summary>
    Task<bool> SetOutputAsync(string output, bool open, CancellationToken ct = default);

    /// <summary>读取电压（V）。</summary>
    Task<double> ReadVoltageAsync(int channel = 0, CancellationToken ct = default);

    /// <summary>读取电流（A）。</summary>
    Task<double> ReadCurrentAsync(int channel = 0, CancellationToken ct = default);
}

/// <summary>ConST811A 工装 GZP21 的共享设备契约。</summary>
public interface IConST811ATestTool : IMachineTestTool
{
    Task<bool> Set27VAsync(bool open, CancellationToken ct = default);
    Task<bool> SetElectricalAsync(bool open, CancellationToken ct = default);
    Task<bool> SetHartAsync(bool open, CancellationToken ct = default);
    Task<bool> SetPaAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 读 27V 电源当前开关状态（旧脚本 <c>Gett27VState(out state)</c>）。
    /// 与 <see cref="Set27VAsync"/> 对应，用于回读确认。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=已开；false=已关。</returns>
    Task<bool> Get27VStateAsync(CancellationToken ct = default);

    /// <summary>
    /// 设备是否已打开/连接（旧脚本 <c>item.GetDevice("GZP21").IsOpen</c>）。
    /// 整机模板下 GZP21 总返回实例，原 null 检查丢弃，但 IsOpen 仍可表达"未初始化/未连接"语义。
    /// </summary>
    bool IsOpen { get; }
}

/// <summary>ConST810 电测/压力共享设备契约。</summary>
public interface IConST810 : IMachineTestTool
{
    Task<IReadOnlyList<double>> ReadVoltageSamplesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<double>> ReadCurrentSamplesAsync(CancellationToken ct = default);
}
