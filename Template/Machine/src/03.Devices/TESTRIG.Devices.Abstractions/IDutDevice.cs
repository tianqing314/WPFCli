using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 被检板（DUT）通用契约。各产品被检板实现此接口（如 ConST218A）。
/// </summary>
public interface IDutDevice : IDevice
{
    /// <summary>
    /// 读取被检序列号。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>序列号。</returns>
    Task<string> ReadSerialNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// 读取被检固件版本。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 写入板卡类型/初始量程等初始信息。
    /// </summary>
    /// <param name="boardType">板卡类型。</param>
    /// <param name="ct">取消令牌。</param>
    Task WriteInitInfoAsync(string boardType, CancellationToken ct = default);

    /// <summary>
    /// 读取一路模拟量测量值（被检板回读）。
    /// </summary>
    /// <param name="point">测量点标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值。</returns>
    Task<double> MeasureAsync(string point, CancellationToken ct = default);

    /// <summary>
    /// 设置被检序列号。PORT: 旧 SetDUTSN。
    /// </summary>
    Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default);

    /// <summary>
    /// 设置产品型号/主设备类型。PORT: 旧 SetPrimaryDeviceType。
    /// </summary>
    Task<bool> SetPrimaryDeviceTypeAsync(string deviceType, CancellationToken ct = default);

    /// <summary>
    /// 通用布尔查询（遗留脚本自动转换）。按方法名 + 参数执行设备指令，返回布尔结果。
    /// </summary>
    Task<bool> QueryBooleanAsync(string method, object? arg, CancellationToken ct = default);

    /// <summary>
    /// 通用文本查询（遗留脚本自动转换）。按方法名 + 参数执行设备指令，返回文本结果。
    /// </summary>
    Task<string> QueryTextAsync(string method, object? arg, CancellationToken ct = default);

    /// <summary>
    /// 通用指令执行（遗留脚本自动转换）。按方法名 + 参数执行设备指令，无返回值。
    /// </summary>
    Task CommandAsync(string method, object? arg, CancellationToken ct = default);
}
