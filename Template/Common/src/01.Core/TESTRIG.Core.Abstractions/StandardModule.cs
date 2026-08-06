namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 标准模块（Tool 设备）契约：整机等模板的标准设备（如 ConST171 的 DPSEX 正压/真空标准模块，
/// 作造压标准源 / 校准标准表）。具体设备驱动（如 DPSEXStandardModule）实现本接口，
/// 处理器按 manifest <c>ToolDevices</c> 的 DeviceKey 用 <c>GetDevice&lt;T&gt;(deviceKey)</c> 获取实例
/// （同一型号可多实例，独立串口/SN）。失败抛 <see cref="DeviceCommException"/>。
/// </summary>
public interface IStandardModule : IDevice
{
    /// <summary>
    /// 读标准模块序列号。
    /// </summary>
    Task<string> GetSerialNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// 读标准模块版本号。
    /// </summary>
    Task<string> GetVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 设置压力类型（"Pressure" 正压 / "Vacuum" 真空）。
    /// </summary>
    Task<bool> SetPressureTypeAsync(string pressureType, CancellationToken ct = default);

    /// <summary>
    /// 读标准压力（kPa）。
    /// </summary>
    Task<double> GetPressureKpaAsync(CancellationToken ct = default);

    /// <summary>
    /// 读模块温度（℃）。
    /// </summary>
    Task<double> GetTemperatureAsync(CancellationToken ct = default);

    /// <summary>
    /// 复位标准模块。
    /// </summary>
    Task<bool> ResetAsync(CancellationToken ct = default);
}
