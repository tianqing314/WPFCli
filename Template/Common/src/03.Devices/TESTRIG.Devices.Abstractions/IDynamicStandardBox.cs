using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 动态测试标准盒**忠实**操作面——与旧 <c>DynamicStandardTestBench</c> 一一对应，供设备特有处理器
/// 按原脚本的继电器通道序列/电流计读数逐项复刻（取代旧 <c>DSTB.NetSwitch*</c> / <c>GetCurrentMeasureValue</c>）。
/// 真机实现 <c>RealStandardBox</c>（走 Xmas11 通讯库），仿真实现 <c>SimulatedStandardBox</c>。
/// </summary>
public interface IDynamicStandardBox : IStandardBox
{
    /// <summary>
    /// 是否真机硬件。处理器据此决定是否插入真实继电器/被检建立稳定延时（仿真跳过以保持快速）。
    /// </summary>
    bool IsRealHardware { get; }

    /// <summary>
    /// **统一继电器切换（枚举法）**：按继电器指令切换（继电器 A/B/C 真值表）。PORT: DSTB.RespondToCommand。
    /// 合并了旧 NetSwitchA/B/C 与 RespondToCommand 两套切换——共用同一张寄存器表。
    /// </summary>
    /// <param name="cmd">继电器指令。</param>
    /// <param name="ct">取消令牌。</param>
    Task RelayCommandAsync(BoxRelayCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// **统一继电器切换（按档位号）**：打开某继电器一个或多个档位。与 <see cref="RelayCommandAsync"/> 共用同一张寄存器表，
    /// 供处理器用动态档位号切换（如 218A 的 Pos*2+33）。取代旧 NetSwitchA/B/COpenChannels。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gears">档位号数组（1 起）。</param>
    /// <param name="ct">取消令牌。</param>
    Task RelayGearAsync(char relay, int[] gears, CancellationToken ct = default);

    /// <summary>
    /// 读电流表通道测量值（uA）。<paramref name="is12Channel"/>=true 走 12 路电流表，false 走 2 路电流表。
    /// PORT: GetCurrentMeasureValue(bool is12Channel, int channel, out double value)。
    /// </summary>
    /// <param name="is12Channel">true 走 12 路电流表，false 走 2 路电流表。</param>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流测量值（uA）。</returns>
    Task<double> GetCurrentMeasureValueAsync(bool is12Channel, int channel, CancellationToken ct = default);

    /// <summary>
    /// **真连通性测试**：按子设备键（ConST326/BNRC32A/BNRC32B/BNRC16C/ZH4402A/ZH4412A/DAM6803D）
    /// 用给定端点构造对应驱动并真正 <c>Open()</c> + <c>IsExist()</c> 探活，测完即关闭以释放端口。
    /// 端点由调用方（连接配置页）传入，取页面**当前编辑值**（未保存亦可测）。真调硬件，非仿真解析；仿真实现返回成功。
    /// </summary>
    /// <param name="key">标准盒子设备键。</param>
    /// <param name="endpoint">该子设备的通讯端点（页面当前值）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 结果说明)。</returns>
    Task<(bool Ok, string Message)> TestSubDeviceAsync(string key, CommEndpoint endpoint, CancellationToken ct = default);
}
