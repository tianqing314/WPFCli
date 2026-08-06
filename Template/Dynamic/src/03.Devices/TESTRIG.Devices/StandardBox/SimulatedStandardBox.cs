using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.StandardBox;

/// <summary>
/// 仿真台协调（**仅仿真用**）：真机上电流由被检功耗模式决定，仿真里被检与标准盒是两个对象无法互知，
/// 故用此进程内静态桥——仿真被检设速率/关机时写入当前功耗模式，仿真电流表据此给出对应标称电流，
/// 使逐项核对后的功耗项在纯仿真下仍绿。真机路径不经此桥（真值来自真实电流表）。
/// </summary>
internal static class SimBench
{
    /// <summary>
    /// 当前功耗模式（AsyncLocal 按号位异步流隔离）：并行号位下各 RunPositionAsync 任务各有独立上下文，
    /// 被检设速率写入、电流表读取处于同一异步流，天然按号位隔离，互不串扰。
    /// </summary>
    private static readonly AsyncLocal<string?> Mode = new();

    /// <summary>
    /// 当前功耗模式（Stable/Low/SuperLow/PowerOff），默认 Stable。
    /// </summary>
    public static string CurrentMode
    {
        get => Mode.Value ?? "Stable";
        set => Mode.Value = value;
    }

    /// <summary>
    /// ConST283 接口板外接模块通断态（仿真被检写、仿真 DAM6803D 通道 4 读）。
    /// </summary>
    private static readonly AsyncLocal<bool> ExtModule = new();

    /// <summary>
    /// 外接模块是否上电（true→通道 4 给有效电压，false→趋 0）。
    /// </summary>
    public static bool ExtModuleOn
    {
        get => ExtModule.Value;
        set => ExtModule.Value = value;
    }

    /// <summary>
    /// ConST283 接口板被检是否在线（仿真开关机：被检软关机写 false、继电器模拟按键释放翻转、上电写 true；
    /// 仿真重连 <c>ReplenishLinkAsync</c> 据此返回连通与否）。默认 true。
    /// </summary>
    private static readonly AsyncLocal<bool?> DutOnline = new();

    /// <summary>
    /// 被检是否在线（默认 true）。写操作须在首个 await 之前同步执行，才能沿异步流回传（见类注释）。
    /// </summary>
    public static bool DutAlive
    {
        get => DutOnline.Value ?? true;
        set => DutOnline.Value = value;
    }
}

/// <summary>
/// 动态测试标准盒仿真：真值表档位切换/上下电/电流计读数都打日志并返回仿真值。全局共享单例。
/// PORT: 旧 Bots.TestBench.Device.DynamicStandardTestBench（继电器 A/B/C + 电流计）。
/// 亦实现 <see cref="IConST326StandardBox"/>（ConST326 表源/测 + DAM6803D 电压表 + 全关继电器）——
/// 使 ConST326 家族板（如 ConST283 接口板）可在纯仿真下跑通调试；ConST326 特有方法给仿真标称值/记录日志。
/// </summary>
public sealed class SimulatedStandardBox : IConST326StandardBox
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<SimulatedStandardBox> _logger;

    /// <summary>
    /// 仿真随机源。
    /// </summary>
    private readonly Random _rng = new();

    /// <summary>
    /// 用日志构造仿真标准盒。
    /// </summary>
    /// <param name="logger">日志。</param>
    public SimulatedStandardBox(ILogger<SimulatedStandardBox> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 设备键。
    /// </summary>
    public string Key => "DSTB";

    /// <summary>
    /// 设备型号名。
    /// </summary>
    public string Model => "DynamicStandardTestBench";

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 是否真机硬件（仿真恒为 false，处理器据此跳过建立延时）。
    /// </summary>
    public bool IsRealHardware => false;

    /// <summary>
    /// 仿真连接。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct);
        IsConnected = true;
        _logger.LogInformation("标准盒仿真连接成功");
    }

    /// <summary>
    /// 仿真切档（仅记录日志）。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gearIndex">档位序号。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SwitchGearAsync(string relay, int gearIndex, CancellationToken ct = default)
    {
        await Task.Delay(15, ct);
        _logger.LogInformation("继电器 {Relay} 切换到档位 {Gear}", relay, gearIndex);
    }

    /// <summary>
    /// 仿真上电（仅记录日志）。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task PowerOnAsync(int channel, CancellationToken ct = default)
    {
        await Task.Delay(15, ct);
        _logger.LogInformation("通道 {Ch} 上电", channel);
    }

    /// <summary>
    /// 仿真断电（仅记录日志）。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task PowerOffAsync(int channel, CancellationToken ct = default)
    {
        await Task.Delay(15, ct);
        _logger.LogInformation("通道 {Ch} 断电", channel);
    }

    /// <summary>
    /// 仿真通用电流读数（mA），按通道给标称值使各 manifest 功耗 Range 条件通过。
    /// 通道1≈20（背光100%/218A整机）、通道2≈10（背光50%）、通道3≈300（背光0%）、通道4≈5（静态功耗）。
    /// 218A 各功耗项用通道1、量程 0~50，仍在范围内，无回归。
    /// </summary>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流（mA）。</returns>
    public async Task<double> ReadCurrentAsync(int channel, CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        return channel switch
        {
            // 19.9~20.1
            1 => 20 + (_rng.NextDouble() - 0.5) * 0.2,
            // 9.95~10.05
            2 => 10 + (_rng.NextDouble() - 0.5) * 0.1,
            // 296~304
            3 => 300 + (_rng.NextDouble() - 0.5) * 8,
            // 4~6
            4 => 5 + (_rng.NextDouble() - 0.5) * 2,
            // 兜底：10~15
            _ => 10 + _rng.NextDouble() * 5,
        };
    }

    /// <summary>
    /// 仿真继电器指令切换（枚举法，仅记录日志）。
    /// </summary>
    /// <param name="cmd">继电器指令。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RelayCommandAsync(BoxRelayCommand cmd, CancellationToken ct = default)
    {
        // 仿真开关机桥（ConST283 接口板）：状态须在首个 await 之前同步写入，才能沿异步流回传给被检重连读取。
        // 上电档→被检上线；1 通道复位（模拟按键释放）→翻转在线态（短按=开机、长按=硬关机，靠时序区分，翻转即可）。
        switch (cmd)
        {
            case BoxRelayCommand.继电器C_18档位_CHA_1_2_3_4_5_6_通道电源控制_上电:
            case BoxRelayCommand.继电器C_20档位_CHB_7_8_9_10_11_12_通道电源控制_上电:
                SimBench.DutAlive = true;
                break;
            case BoxRelayCommand.继电器C_1档位_1通道控制_常闭点:
                SimBench.DutAlive = !SimBench.DutAlive;
                break;
        }

        await Task.Delay(5, ct);
        _logger.LogInformation("继电器指令 {Cmd}", cmd);
    }

    /// <summary>
    /// 仿真继电器按档位号切换（仅记录日志）。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gears">档位号数组。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RelayGearAsync(char relay, int[] gears, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        _logger.LogInformation("继电器{Relay} 打开档位 {Ch}", relay, string.Join(",", gears));
    }

    /// <summary>
    /// 仿真电流表读数（uA）。通道 &gt;=7（旧外供电功耗 6+工位）给外供电电流；否则按当前功耗模式给待机电流，
    /// 使逐项核对后各功耗项在纯仿真下落在对应区间。
    /// </summary>
    /// <param name="is12Channel">true 走 12 路电流表，false 走 2 路电流表。</param>
    /// <param name="channel">通道号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电流（uA）。</returns>
    public async Task<double> GetCurrentMeasureValueAsync(bool is12Channel, int channel, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        double N(double span)
        {
            return (_rng.NextDouble() - 0.5) * span;
        }

        // 2 路电流表（is12Channel=false）：ConST283 接口板功耗项读通道 2，处理器 ×1000 转 mA
        // （基准约定见输入文档 §8）。给 ~0.12 基准 → ~120mA，满足峰值<150、平均 100~250。
        if (!is12Channel)
        {
            return 0.12 + N(0.01);
        }

        // 外供电功耗 8000~10000
        if (channel >= 7)
        {
            return 9000 + N(200);
        }

        // 待机功耗（径向-恒流分支区间）
        return SimBench.CurrentMode switch
        {
            // 966.99~1166.99
            "Stable" => 1066 + N(40),
            // 313.27~513.27
            "Low" => 413 + N(30),
            // 54.315~124.315
            "SuperLow" => 89 + N(20),
            // 0~5
            "PowerOff" => 2 + N(1),
            _ => 1000 + N(60),
        };
    }

    /// <summary>
    /// 仿真子设备连通性测试：不碰硬件，恒返回成功。
    /// </summary>
    /// <param name="key">子设备键。</param>
    /// <param name="endpoint">通讯端点（仿真忽略）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public async Task<(bool Ok, string Message)> TestSubDeviceAsync(string key, CommEndpoint endpoint, CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        _logger.LogInformation("子设备 {Key} 仿真连接成功", key);
        return (true, $"{key} 仿真连接成功");
    }

    // ===== IConST326StandardBox（ConST326 表源/测 + DAM6803D 电压表 + 全关继电器）：仿真标称值/记录日志 =====

    /// <summary>仿真 ConST326 输出档位切换（记录日志）。</summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SetOutputGearAsync(Gear326 gear, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 输出档位 → {Gear}", gear);
    }

    /// <summary>仿真 ConST326 测量档位切换（记录日志）。</summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SetMeasureGearAsync(Gear326 gear, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 测量档位 → {Gear}", gear);
    }

    /// <summary>仿真 ConST326 设置电压输出（记录日志）。</summary>
    /// <param name="volts">电压（V）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SetOutputVoltageVAsync(double volts, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 输出电压 {V}V", volts);
    }

    /// <summary>仿真 ConST326 设置电流输出（记录日志）。</summary>
    /// <param name="milliAmps">电流（mA）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SetOutputCurrentMaAsync(double milliAmps, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 输出电流 {Ma}mA", milliAmps);
    }

    /// <summary>仿真 ConST326 设置热电偶输出温度（记录日志）。</summary>
    /// <param name="centigrade">温度（℃）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SetOutputTCCentigradeAsync(double centigrade, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 输出温度 {C}℃", centigrade);
    }

    /// <summary>仿真 ConST326 开/关 24V 供电（记录日志）。</summary>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task Set24VAsync(bool open, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("ConST326 24V {State}", open ? "开" : "关");
    }

    /// <summary>仿真 ConST326 读当前测量值（标称 ~0，随机微噪）。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值。</returns>
    public async Task<double> ReadConST326ValueAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        return (_rng.NextDouble() - 0.5) * 0.01;
    }

    /// <summary>
    /// 仿真 DAM6803D 读某通道电压（通道 0 起）。
    /// ConST283 接口板：通道 4=外接模块（据 <see cref="SimBench.ExtModuleOn"/> 给 ~5V/趋 0）；
    /// 通道 0~3=电源轨 VCC_D5V/1.2V/3.3V/RTC_3.3V 给对应标称电压（manifest 目标占位 0 时处理器仅记录）。
    /// </summary>
    /// <param name="channel">通道（0 起）。</param>
    /// <param name="reverse">是否取反。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压测量值（V）。</returns>
    public async Task<double> GetVoltageMeasureValueAsync(int channel, bool reverse = false, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        double N(double span)
        {
            return (_rng.NextDouble() - 0.5) * span;
        }

        var v = channel switch
        {
            // 外接模块：上电 ~5V（判>1）、掉电 ~0.05V（判<0.5）
            4 => SimBench.ExtModuleOn ? 5.0 + N(0.05) : 0.05 + N(0.02),
            // 电源轨标称
            0 => 5.0 + N(0.02),
            1 => 1.2 + N(0.01),
            2 => 3.3 + N(0.02),
            3 => 3.3 + N(0.02),
            _ => N(0.02),
        };
        return reverse ? -v : v;
    }

    /// <summary>仿真关闭继电器 C 通道（记录日志）。</summary>
    /// <param name="firstEightOnly">仅关前 8 路。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task CloseAllCChannelsAsync(bool firstEightOnly = true, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("继电器C 全关（前8路={First}）", firstEightOnly);
    }

    /// <summary>仿真关闭继电器 A 全部通道（记录日志）。</summary>
    /// <param name="ct">取消令牌。</param>
    public async Task CloseAllAChannelsAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("继电器A 全关");
    }

    /// <summary>仿真关闭继电器 B 全部通道（记录日志）。</summary>
    /// <param name="ct">取消令牌。</param>
    public async Task CloseAllBChannelsAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("继电器B 全关");
    }

    /// <summary>仿真关闭全部继电器（A/B/C，记录日志）。</summary>
    /// <param name="ct">取消令牌。</param>
    public async Task CloseAllRelaysAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _logger.LogInformation("继电器 A/B/C 全关");
    }

    /// <summary>
    /// 释放（置未连接）。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
