namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// ConST326 标准表源/测档位。**忠实复刻**旧 <c>DynamicStandardTestBench.MeasureGear326</c>。
/// </summary>
public enum Gear326
{
    /// <summary>
    /// 无效档。
    /// </summary>
    Invalid,

    /// <summary>
    /// 关档。
    /// </summary>
    Off,

    /// <summary>
    /// 电压 V 档（-30~30V）。
    /// </summary>
    V,

    /// <summary>
    /// 毫伏 mV 档。
    /// </summary>
    mV,

    /// <summary>
    /// 电流 mA 档（-30~30mA）。
    /// </summary>
    mA,

    /// <summary>
    /// 机械开关档。
    /// </summary>
    Switch,

    /// <summary>
    /// mA 带 24V 环路档。
    /// </summary>
    mAWith24V,

    /// <summary>
    /// 关闭 24V 供电。
    /// </summary>
    Close24V,

    /// <summary>
    /// 热电偶 K 型档。
    /// </summary>
    TC_K,

    /// <summary>
    /// 热电偶 N 型档。
    /// </summary>
    TC_N,
}

/// <summary>
/// 标准盒继电器指令。**逐条复刻**旧 <c>DynamicStandardTestBench.CommandEnum</c>（继电器 A/B/C 档位真值），
/// 名称与旧一致以便与旧 <c>Configs</c> 寄存器映射串按名匹配（<see cref="IConST326StandardBox.RelayCommandAsync"/>）。
/// </summary>
public enum BoxRelayCommand
{
    继电器A_1档位_CHA_1_2_3_4_5_6_通道电源选择_3_3V供电,
    继电器A_2档位_CHA_1_2_3_4_5_6_通道电源选择_5V供电,
    继电器A_3档位_CHA_1_2_3_4_5_6_通道电源选择_12V供电,
    继电器A_4档位_CHA_1_2_3_4_5_6_通道电源选择_24V供电,
    继电器A_5档位_CHA_1_2_3_4_5_6_通道电源选择_27V供电,
    继电器A_6档位_CHB_7_8_9_10_11_12_通道电源选择_3_3V供电,
    继电器A_7档位_CHB_7_8_9_10_11_12_通道电源选择_5V供电,
    继电器A_8档位_CHB_7_8_9_10_11_12_通道电源选择_12V供电,
    继电器A_9档位_CHB_7_8_9_10_11_12_通道电源选择_24V供电,
    继电器A_10档位_CHB_7_8_9_10_11_12_通道电源选择_27V供电,
    继电器A_11档位_CH1通道电源控制_上电,
    继电器A_12档位_CH1通道电源控制_断电,
    继电器A_13档位_CH2通道电源控制_上电,
    继电器A_14档位_CH2通道电源控制_断电,
    继电器A_15档位_CH3通道电源控制_上电,
    继电器A_16档位_CH3通道电源控制_断电,
    继电器A_17档位_CH4通道电源控制_上电,
    继电器A_18档位_CH4通道电源控制_断电,
    继电器A_19档位_CH5通道电源控制_上电,
    继电器A_20档位_CH5通道电源控制_断电,
    继电器A_21档位_CH6通道电源控制_上电,
    继电器A_22档位_CH6通道电源控制_断电,
    继电器A_23档位_CH7通道电源控制_上电,
    继电器A_24档位_CH7通道电源控制_断电,
    继电器A_25档位_CH8通道电源控制_上电,
    继电器A_26档位_CH8通道电源控制_断电,
    继电器A_27档位_CH9通道电源控制_上电,
    继电器A_28档位_CH9通道电源控制_断电,
    继电器A_29档位_CH10通道电源控制_上电,
    继电器A_30档位_CH10通道电源控制_断电,
    继电器A_31档位_CH11通道电源控制_上电,
    继电器A_32档位_CH11通道电源控制_断电,
    继电器A_33档位_CH12通道电源控制_上电,
    继电器A_34档位_CH12通道电源控制_断电,
    继电器A_35档位_CH1通道电流采集控制_短路,
    继电器A_36档位_CH1通道电流采集控制_测量,
    继电器A_37档位_CH2通道电流采集控制_短路,
    继电器A_38档位_CH2通道电流采集控制_测量,
    继电器A_39档位_CH3通道电流采集控制_短路,
    继电器A_40档位_CH3通道电流采集控制_测量,
    继电器A_41档位_CH4通道电流采集控制_短路,
    继电器A_42档位_CH4通道电流采集控制_测量,
    继电器A_43档位_CH5通道电流采集控制_短路,
    继电器A_44档位_CH5通道电流采集控制_测量,
    继电器A_45档位_CH6通道电流采集控制_短路,
    继电器A_46档位_CH6通道电流采集控制_测量,
    继电器A_47档位_CH7通道电流采集控制_短路,
    继电器A_48档位_CH7通道电流采集控制_测量,
    继电器A_49档位_CH8通道电流采集控制_短路,
    继电器A_50档位_CH8通道电流采集控制_测量,
    继电器A_51档位_CH9通道电流采集控制_短路,
    继电器A_52档位_CH9通道电流采集控制_测量,
    继电器A_53档位_CH10通道电流采集控制_短路,
    继电器A_54档位_CH10通道电流采集控制_测量,
    继电器A_55档位_CH11通道电流采集控制_短路,
    继电器A_56档位_CH11通道电流采集控制_测量,
    继电器A_57档位_CH12通道电流采集控制_短路,
    继电器A_58档位_CH12通道电流采集控制_测量,
    继电器C_17档位_CHA_1_2_3_4_5_6_通道电源控制_断电,
    继电器C_18档位_CHA_1_2_3_4_5_6_通道电源控制_上电,
    继电器C_19档位_CHB_7_8_9_10_11_12_通道电源控制_断电,
    继电器C_20档位_CHB_7_8_9_10_11_12_通道电源控制_上电,
    继电器C_21档位_标准表326开关机控制_上电,
    继电器C_22档位_标准表326开关机控制_断电,
    继电器B_1档位_测试通道选择_连接E1通道,
    继电器B_2档位_测试通道选择_连接E2通道,
    继电器B_3档位_测试通道选择_连接E3通道,
    继电器B_4档位_测试通道选择_连接E4通道,
    继电器B_5档位_测试通道选择_连接E5通道,
    继电器B_6档位_测试通道选择_连接E6通道,
    继电器B_7档位_测试通道选择_连接E7通道,
    继电器B_8档位_测试通道选择_连接E8通道,
    继电器B_9档位_测试类型选择_电流测量,
    继电器B_10档位_测试类型选择_电压测量,
    继电器B_11档位_测试类型选择_电阻输出_测量,
    继电器B_12档位_测试类型选择_TC输出_测量,
    继电器B_13档位_测试类型选择_电流输出,
    继电器B_14档位_测试类型选择_电压输出,
    继电器B_15档位_测试类型选择_内源内阻HART测试,
    继电器B_16档位_测试类型选择_外源外阻HART测试,
    继电器C_1档位_1通道控制_常闭点,
    继电器C_2档位_1通道控制_常开点,
    继电器C_3档位_2通道控制_常闭点,
    继电器C_4档位_2通道控制_常开点,
    继电器C_5档位_3通道控制_常闭点,
    继电器C_6档位_3通道控制_常开点,
    继电器C_7档位_4通道控制_常闭点,
    继电器C_8档位_4通道控制_常开点,
    继电器C_9档位_5通道控制_常闭点,
    继电器C_10档位_5通道控制_常开点,
    继电器C_11档位_6通道控制_常闭点,
    继电器C_12档位_6通道控制_常开点,
    继电器C_13档位_7通道控制_常闭点,
    继电器C_14档位_7通道控制_常开点,
    继电器C_15档位_8通道控制_常闭点,
    继电器C_16档位_8通道控制_常开点,
}

/// <summary>
/// 在 <see cref="IDynamicStandardBox"/> 之上扩展 ConST560 测量板需要的标准盒能力（比 218A 多）：
/// ConST326 标准表源/测（Xmas11 <c>DPC2Base</c>）、DAM6803D 电压表、以及基于 <see cref="BoxRelayCommand"/>
/// 的继电器切换（旧 <c>RespondToCommand</c>）。忠实迁移旧 <c>DynamicStandardTestBench</c>。
/// 设备特有处理器通过 <c>ctx.GetDevice&lt;IConST326StandardBox&gt;()</c> 获取（仅真机标准盒实现，仿真盒不实现）。
/// </summary>
public interface IConST326StandardBox : IDynamicStandardBox
{
    /// <summary>
    /// ConST326 输出档位切换。PORT: ConST326SwitchOutputGearTo。
    /// </summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetOutputGearAsync(Gear326 gear, CancellationToken ct = default);

    /// <summary>
    /// ConST326 测量档位切换。PORT: ConST326SwitchMeasureGearTo。
    /// </summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetMeasureGearAsync(Gear326 gear, CancellationToken ct = default);

    /// <summary>
    /// ConST326 设置电压输出值（V）。PORT: ConST326SetOutputVoltage_V。
    /// </summary>
    /// <param name="volts">电压（V）。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetOutputVoltageVAsync(double volts, CancellationToken ct = default);

    /// <summary>
    /// ConST326 设置电流输出值（mA）。PORT: ConST326SetOutputCurrent_mA。
    /// </summary>
    /// <param name="milliAmps">电流（mA）。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetOutputCurrentMaAsync(double milliAmps, CancellationToken ct = default);

    /// <summary>
    /// ConST326 设置热电偶输出温度（℃）。PORT: ConST326SetOutputTCCentigrade（TC 档下设定输出值）。
    /// </summary>
    /// <param name="centigrade">温度（℃）。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetOutputTCCentigradeAsync(double centigrade, CancellationToken ct = default);

    /// <summary>
    /// ConST326 开/关 24V 供电。PORT: ConST326OpenDCVoltage/CloseDCVoltage。
    /// </summary>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    Task Set24VAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// ConST326 读当前测量值。PORT: ConST326ReadValue。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值。</returns>
    Task<double> ReadConST326ValueAsync(CancellationToken ct = default);

    /// <summary>
    /// DAM6803D 读某通道电压测量值（通道从 0 起）。PORT: GetVoltageMeasureValue。
    /// </summary>
    /// <param name="channel">通道（0 起）。</param>
    /// <param name="reverse">是否取反。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压测量值。</returns>
    Task<double> GetVoltageMeasureValueAsync(int channel, bool reverse = false, CancellationToken ct = default);

    /// <summary>
    /// 关闭继电器 C 通道（默认仅前 8 路）。PORT: NetSwitchCCloseAllChannels。
    /// </summary>
    /// <param name="firstEightOnly">仅关前 8 路。</param>
    /// <param name="ct">取消令牌。</param>
    Task CloseAllCChannelsAsync(bool firstEightOnly = true, CancellationToken ct = default);

    /// <summary>
    /// 关闭继电器 A 全部通道。PORT: NetSwitchACloseAllChannels（BNRC32A.ZQWL_SetOutputCloseAll）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task CloseAllAChannelsAsync(CancellationToken ct = default);

    /// <summary>
    /// 关闭继电器 B 全部通道。PORT: NetSwitchBCloseAllChannels（BNRC32B.ZQWL_SetOutputCloseAll）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task CloseAllBChannelsAsync(CancellationToken ct = default);

    /// <summary>
    /// 关闭全部继电器（A/B/C 三组）。PORT: NetSwitchCloseAllChannels
    /// （BNRC32A/BNRC32B/BNRC16C 各 <c>ZQWL_SetOutputCloseAll</c>）。系统板工装准备清零工装用。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task CloseAllRelaysAsync(CancellationToken ct = default);
}
