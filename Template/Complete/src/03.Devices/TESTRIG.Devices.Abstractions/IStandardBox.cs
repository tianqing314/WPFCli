using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 动态测试标准盒（DSTB）。对当前业务**通用、全局共享**：所有被检都依赖它，不归属某块板。
/// 屏蔽硬件细节，上位机只按"真值表档位"切换线路——对应 TESTRIG动态测试业务介绍.md 的继电器 A/B/C 真值表。
/// </summary>
public interface IStandardBox : IDevice
{
    /// <summary>
    /// 按真值表切换某继电器到指定档位（1-based 档位序号）。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gearIndex">档位序号。</param>
    /// <param name="ct">取消令牌。</param>
    Task SwitchGearAsync(string relay, int gearIndex, CancellationToken ct = default);

    /// <summary>
    /// 给某通道上电。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    Task PowerOnAsync(int channel, CancellationToken ct = default);

    /// <summary>
    /// 给某通道断电。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    Task PowerOffAsync(int channel, CancellationToken ct = default);

    /// <summary>
    /// 读取某通道电流计电流（mA）。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流（mA）。</returns>
    Task<double> ReadCurrentAsync(int channel, CancellationToken ct = default);
}
