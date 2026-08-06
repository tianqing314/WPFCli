namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 环境温度读取（**非被检指令**）：从产线温湿度计监控服务按传感器 SN 取当前室温。
/// 取代旧 <c>Helper.GetEnvironmentTemp(sn)</c>（HTTP 监控接口 + 10 分钟新鲜度校验）。
/// </summary>
public interface IEnvironmentTemperature
{
    /// <summary>
    /// 依次尝试各温湿度计 SN，返回首个新鲜（10 分钟内）室温（℃），全失败返回 null。
    /// </summary>
    /// <param name="sensorSns">温湿度计 SN 集合（按序尝试）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>室温（℃），全失败返回 null。</returns>
    Task<double?> ReadAsync(IEnumerable<string> sensorSns, CancellationToken ct = default);
}
