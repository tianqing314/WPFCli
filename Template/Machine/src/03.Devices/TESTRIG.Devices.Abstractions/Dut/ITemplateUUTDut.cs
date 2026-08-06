using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// {{DutType}} 控制板（设备族 {{DutType}}）模块名。**忠实对应**旧 <c>Xmas11.Comm.Devices.ConST171.Data.ModuleName</c>
/// （Pre=前级、Boost=增压[旧 ModuleName.Pressure]、Vacuum=真空），在抽象层定义以免上层引用 Xmas11。
/// </summary>
public enum {{DutType}}Module
{
    /// <summary>前级组件（旧 ModuleName.Pre）。</summary>
    Pre,

    /// <summary>增压组件（旧 ModuleName.Pressure）。</summary>
    Boost,

    /// <summary>真空组件（旧 ModuleName.Vacuum）。</summary>
    Vacuum,
}

/// <summary>
/// {{DutType}} 控制板阀门名。**忠实对应**旧 <c>Xmas11.Comm.Devices.ConST171.Data.ValveName</c>
/// （Boost=增压 V1、Pre=前级 V2、Vacuum1/2=真空 V3/V4）。
/// </summary>
public enum {{DutType}}Valve
{
    /// <summary>增压阀 V1（旧 ValveName.Boost）。</summary>
    Boost,

    /// <summary>前级阀 V2（旧 ValveName.Pre）。</summary>
    Pre,

    /// <summary>真空阀 V3（旧 ValveName.Vacuu1）。</summary>
    Vacuum1,

    /// <summary>真空阀 V4（旧 ValveName.Vacuu2）。</summary>
    Vacuum2,
}

/// <summary>
/// {{DutType}} 控制板（设备族 {{DutType}}）被检命令层。**忠实对应**旧 <c>Bots.TestBench.Device.ConST171CommonBase</c> 里
/// {{DutType}} 动态测试用到的方法（内部转调 Xmas11 <c>ConST171Base</c>，返回 <c>iResponse</c>）。
/// 读值方法返回值、通讯/执行失败抛 <see cref="DeviceCommException"/>（由引擎按异常收尾并落盘）。
/// </summary>
public interface I{{DutType}}Dut : IDutDevice
{
    /// <summary>
    /// 补充连接（重连），返回是否已连接。PORT: 旧 ConST171CommonBase.ReplenishLink（针床设备逻辑简化为直接建连）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否连接成功。</returns>
    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);

    /// <summary>
    /// 读控制器软件版本。PORT: GetControlVersion（ConST171Base.GetCONTrolsoftversion）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    Task<string> ReadCtlVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 读控制器硬件版本。PORT: GetCONTrolhardversion（ConST171Base.GetCONTrolhardversion）。
    /// 注：旧 ConST171CommonBase 包装存在复制粘贴 bug（硬件版本方法内实读软件版本），本迁移改调正确的硬件版本命令。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    Task<string> ReadCtlHardVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 读 DC24V 电压。PORT: GetDC24Volt（ConST171Base.GetDC24Volt）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    Task<double> ReadDC24VoltAsync(CancellationToken ct = default);

    /// <summary>
    /// 读 BOOST-SENSOR 电压。PORT: GetBoostVolt（ConST171Base.GetBoostVolt）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    Task<double> ReadBoostVoltAsync(CancellationToken ct = default);

    /// <summary>
    /// 读 VACUUM-SENSOR 电压。PORT: GetVacuumVolt（ConST171Base.GetVacuumVolt）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    Task<double> ReadVacuumVoltAsync(CancellationToken ct = default);

    /// <summary>
    /// 设置蜂鸣器开/关。PORT: SetBuzzerState（ConST171Base.SetBuzzer）。
    /// </summary>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetBuzzerAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 读某组件压力值。PORT: GetPressure（ConST171Base.GetPressure）。
    /// </summary>
    /// <param name="module">组件（增压/真空）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>压力值（读失败/无效返回 0）。</returns>
    Task<double> ReadPressureAsync({{DutType}}Module module, CancellationToken ct = default);

    /// <summary>
    /// 读板载 NTC 温度。PORT: GetBoardTemperature（ConST171Base.GetBoardTemperature）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>温度（℃）。</returns>
    Task<double> ReadBoardTemperatureAsync(CancellationToken ct = default);

    /// <summary>
    /// 读某组件 NTC 温度。PORT: GetTemperature（ConST171Base.GetTemperature）。
    /// </summary>
    /// <param name="module">组件（前级/增压）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>温度（℃）。</returns>
    Task<double> ReadTemperatureAsync({{DutType}}Module module, CancellationToken ct = default);

    /// <summary>
    /// 设置某组件风扇占空比。PORT: SetFanSpeed（ConST171Base.SetFanSpeed）。
    /// </summary>
    /// <param name="module">组件（前级/增压）。</param>
    /// <param name="pwm">占空比。</param>
    /// <param name="ct">取消令牌。</param>
    Task SetFanSpeedAsync({{DutType}}Module module, double pwm, CancellationToken ct = default);

    /// <summary>
    /// 读某组件风扇转速。PORT: GetFanSpeed（ConST171Base.GetFanSpeed）。
    /// </summary>
    /// <param name="module">组件（前级/增压）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>转速。</returns>
    Task<double> ReadFanSpeedAsync({{DutType}}Module module, CancellationToken ct = default);

    /// <summary>
    /// 设置整机测试模式（含重连重试与回读校验）。PORT: SetDiagnosticTest（ConST171Base.SetDiagnosticTest + GetDiagnosticTestState）。
    /// </summary>
    /// <param name="open">true 进入，false 退出。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否设置成功（回读一致）。</returns>
    Task<bool> SetDiagnosticTestAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置阀门开/关。PORT: SetValveStatus（ConST171Base.SetValveStatus）。
    /// </summary>
    /// <param name="valve">阀门。</param>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否下发成功。</returns>
    Task<bool> SetValveAsync({{DutType}}Valve valve, bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置某组件泵开/关。PORT: SetPumpStatus（ConST171Base.SetPumpStatus）。
    /// </summary>
    /// <param name="module">组件（前级/增压/真空）。</param>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否下发成功。</returns>
    Task<bool> SetPumpAsync({{DutType}}Module module, bool open, CancellationToken ct = default);

    /// <summary>
    /// 读增压/前级泵工作电压。PORT: GetPumpVoltage（ConST171Base.GetPumpVoltage）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(前级电压, 增压电压)。</returns>
    Task<(double Pre, double Boost)> ReadPumpVoltageAsync(CancellationToken ct = default);

    /// <summary>
    /// 读泵控制器故障码。PORT: GetPumpControllerFaultCode（ConST171Base.GetPumpControllerFaultCode）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(前级故障码, 增压故障码)。</returns>
    Task<(double Pre, double Boost)> ReadPumpFaultCodeAsync(CancellationToken ct = default);

    /// <summary>
    /// 读某组件 FOC 芯片状态是否正常。PORT: IsFocNormal（ConST171Base.IsFOCNormal）。
    /// </summary>
    /// <param name="module">组件（前级/增压）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否正常。</returns>
    Task<bool> IsFocNormalAsync({{DutType}}Module module, CancellationToken ct = default);
}
