namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 环境大气压读取（**非被检指令**）：从产线温湿度计监控服务按传感器 SN 取当前大气压。
/// 取代旧 <c>Helper.GetLatestAtoms</c> 的产线单/多传感器 HTTP 取值分支（同一监控接口，仅 <c>type=BP</c>）。
/// 与 <see cref="IEnvironmentTemperature"/> 为同一监控设备的两个量纲（温度 <c>type=T</c> / 大气压 <c>type=BP</c>）。
/// </summary>
public interface IEnvironmentPressure
{
    /// <summary>
    /// 依次尝试各温湿度计 SN，返回首个新鲜（10 分钟内）大气压（kPa），全失败返回 null。
    /// </summary>
    /// <param name="sensorSns">温湿度计 SN 集合（按序尝试）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>大气压（kPa），全失败返回 null。</returns>
    Task<double?> ReadAsync(IEnumerable<string> sensorSns, CancellationToken ct = default);
}
