using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// {{DutType}} 被检设备接口占位。接入真实产品时由 References 引擎自动生成（或按此模板手动扩展）。
/// 读值方法返回值、通讯/执行失败抛 <see cref="DeviceCommException"/>（由引擎按异常收尾并落盘）。
/// </summary>
public interface I{{DutType}}Dut : IDutDevice
{
    /// <summary>
    /// 补充连接（重连），返回是否已连接。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否连接成功。</returns>
    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);
}
