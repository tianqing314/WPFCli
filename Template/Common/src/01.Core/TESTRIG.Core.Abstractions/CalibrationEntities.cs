namespace TESTRIG.Core.Abstractions;

// 此文件承载压力校准类设备（ConST811A 等）的强类型结果实体：driver 经 QueryDoubleAsync/QueryTextAsync
// 取回裸值后由 handler 包装为这些 record，保留单位语义与结构化字段，避免裸 double 散落或留 TODO。

/// <summary>
/// 压力读数（旧脚本 <c>Pressure</c>）。driver 经 <c>QueryDoubleAsync("GetPressure_IPM")</c> 取回 Value 后包装。
/// 运算取 <see cref="Value"/>；<see cref="Unit"/> 保留单位语义（默认 kPa）。
/// </summary>
/// <param name="Value">压力值。</param>
/// <param name="Unit">单位（默认 kPa）。</param>
public sealed record Pressure(double Value, string Unit = "kPa");

/// <summary>
/// 压力量程（旧脚本 <c>PressureRange</c>）。<see cref="LowerValue"/>/<see cref="UpperValue"/> 为量程上下限。
/// </summary>
/// <param name="LowerValue">下限值。</param>
/// <param name="UpperValue">上限值。</param>
/// <param name="Unit">单位（默认 kPa）。</param>
public sealed record PressureRange(double LowerValue, double UpperValue, string Unit = "kPa");

/// <summary>
/// 电测功能档位（旧脚本 <c>ElectricMeasureFunction</c>）。
/// </summary>
public enum ElectricMeasureFunction
{
    /// <summary>无。</summary>
    None,
    /// <summary>HART 通讯。</summary>
    Hart,
    /// <summary>电压（V）。</summary>
    Vol,
    /// <summary>毫伏（mV）。</summary>
    Mvol,
    /// <summary>电流（mA/A）。</summary>
    Curr,
    /// <summary>压力传感器。</summary>
    Pa,
    /// <summary>开关量。</summary>
    Sw,
}

/// <summary>
/// 电测测量值（旧脚本 <c>ElectricMeasure</c>）。<see cref="MeasureValue"/> 为读数，<see cref="Unit"/> 保留单位，
/// <see cref="MeasureFunction"/> 标明当前档位（用于判定是否切到 PA 传感器等）。
/// </summary>
/// <param name="MeasureValue">测量值。</param>
/// <param name="Unit">单位。</param>
/// <param name="MeasureFunction">电测功能档位。</param>
public sealed record ElectricMeasure(double MeasureValue, string Unit, ElectricMeasureFunction MeasureFunction);

/// <summary>
/// 气泵测试流程状态（旧脚本 <c>PumpTestProcessState</c>）。
/// </summary>
public enum PumpTestProcessState
{
    /// <summary>未知。</summary>
    UnKnown,
    /// <summary>关闭/未启动。</summary>
    ShutDown,
    /// <summary>进行中。</summary>
    InProgress,
    /// <summary>已完成。</summary>
    Completed,
}

/// <summary>
/// 气泵测试子项结论（旧脚本 <c>PumpTestResultState</c>）。
/// </summary>
public enum PumpTestResultState
{
    /// <summary>未知。</summary>
    UnKnown,
    /// <summary>成功。</summary>
    Succeed,
    /// <summary>失败。</summary>
    Failed,
    /// <summary>未完成。</summary>
    NoFinished,
}

/// <summary>
/// 气泵测试状态（旧脚本 <c>PumpTestState</c>，由 <c>APC2.GetPumpTestState()</c> 返回）。
/// <see cref="TestState"/> 为整体流程阶段；正/负压各有 <c>PresTest</c>（压力测试结论）与
/// <c>PersSensorTest</c>（传感器测试结论），及对应的传感器误差（百分比，旧脚本按 <c>ToString("P")</c> 输出）。
/// </summary>
/// <param name="TestState">整体流程状态。</param>
/// <param name="PositivePresTest">正压测试结论。</param>
/// <param name="PositivePersSensorTest">正压传感器测试结论。</param>
/// <param name="PositiveSensorError">正压传感器误差。</param>
/// <param name="NegativePresTest">负压测试结论。</param>
/// <param name="NegativePersSensorTest">负压传感器测试结论。</param>
/// <param name="NegativeSensorError">负压传感器误差。</param>
public sealed record PumpTestState(
    PumpTestProcessState TestState,
    PumpTestResultState PositivePresTest,
    PumpTestResultState PositivePersSensorTest,
    double PositiveSensorError,
    PumpTestResultState NegativePresTest,
    PumpTestResultState NegativePersSensorTest,
    double NegativeSensorError);

/// <summary>
/// 进气传感器校准状态测试阶段（旧脚本 <c>CalibrationSensorStateTest</c>）。
/// </summary>
public enum CalibrationSensorStateTest
{
    /// <summary>未知/未开始。</summary>
    UnKnown,
    /// <summary>进行中。</summary>
    Process,
    /// <summary>完成。</summary>
    Complete,
    /// <summary>失败。</summary>
    Failed,
}

/// <summary>
/// 进气传感器校准数据（旧脚本 <c>IntakeSensorCalibrationData</c>，由 <c>APC2.GetCalibrationSensorState()</c> 返回）。
/// <see cref="ResultType"/> 为校准阶段；<see cref="ProcessValue"/> 为进度百分比。
/// </summary>
/// <param name="ResultType">校准阶段。</param>
/// <param name="ProcessValue">进度百分比。</param>
public sealed record IntakeSensorCalibrationData(CalibrationSensorStateTest ResultType, double ProcessValue);

/// <summary>
/// 自整定测试阶段（旧脚本 <c>SelfTuningTestType</c>）。
/// </summary>
public enum SelfTuningTestType
{
    /// <summary>未知/未开始。</summary>
    Unknown,
    /// <summary>进行中。</summary>
    InProgress,
    /// <summary>完成。</summary>
    Completed,
    /// <summary>失败。</summary>
    Failed,
}

/// <summary>
/// 自整定数据（旧脚本 <c>SelfTuningData</c>，由 <c>APC2.GetSelfTuningState()</c> 返回）。
/// <see cref="ResultType"/> 为整定阶段；进行中时携带 <see cref="ProcessValue"/>（进度%）、
/// <see cref="SetPoint"/>（设定点）、<see cref="IntakeValveControls"/>（进气阀控制量）、
/// <see cref="OuttakeValveControls"/>（放气阀控制量）。
/// </summary>
/// <param name="ResultType">整定阶段。</param>
/// <param name="ProcessValue">进度百分比。</param>
/// <param name="SetPoint">设定点。</param>
/// <param name="IntakeValveControls">进气阀控制量。</param>
/// <param name="OuttakeValveControls">放气阀控制量。</param>
public sealed record SelfTuningData(
    SelfTuningTestType ResultType,
    double ProcessValue,
    double SetPoint,
    double IntakeValveControls,
    double OuttakeValveControls);
