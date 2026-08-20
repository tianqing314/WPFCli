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

    /// <summary>读取继电器/开关状态。具体设备负责把名称映射到硬件通道。</summary>
    Task<bool> GetOutputStateAsync(string output, CancellationToken ct = default);

    /// <summary>读取电压（V）。</summary>
    Task<double> ReadVoltageAsync(int channel = 0, CancellationToken ct = default);

    /// <summary>读取电流（A）。</summary>
    Task<double> ReadCurrentAsync(int channel = 0, CancellationToken ct = default);
}
