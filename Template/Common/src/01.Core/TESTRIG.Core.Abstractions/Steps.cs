using Microsoft.Extensions.Logging;

namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 一类测试项的强类型处理器——取代旧巨型 <c>TestTaskRunViewModel</c> 的 switch + CS-Script <c>dynamic</c>。
/// 一个 Kind 一个处理器，25 块板复用同一批处理器；新板只在 JSON 里列"测试项(kind+data)"。可断点、编译期检查。
/// </summary>
public interface IStepHandler
{
    /// <summary>
    /// 处理的测试项类型，对应 <see cref="StepDescriptor.Kind"/>。
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// 限定的设备家族（对应 <see cref="JigManifest.DeviceFamily"/>）。
    /// null = 通用处理器（多设备复用）；非 null = 设备特有处理器（仅该设备的板使用，
    /// 内部用 <c>ctx.GetDevice&lt;该设备特有驱动接口&gt;()</c> 调其专属指令）。
    /// 引擎解析时设备特有优先于通用。
    /// </summary>
    string? DeviceFamily => null;

    /// <summary>
    /// 执行该测试项。
    /// </summary>
    /// <param name="context">测试项运行上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    Task<StepResult> ExecuteAsync(ITestContext context, CancellationToken ct = default);
}

/// <summary>
/// 测试项运行上下文：拿当前项数据(Step)、拿设备、判定、报实时消息、记日志。设备解析强类型，无字符串反射。
/// </summary>
public interface ITestContext
{
    /// <summary>
    /// 当前号位/板位。
    /// </summary>
    PositionDescriptor Position { get; }

    /// <summary>
    /// 当前测试项数据（Settings/Parameters/Conditions 都在这里）。
    /// </summary>
    StepDescriptor Step { get; }

    /// <summary>
    /// 被检序列号（可读写，写入后同工位后续项共用）。
    /// </summary>
    string? SerialNumber { get; set; }

    /// <summary>
    /// 日志。
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// 判定器。
    /// </summary>
    IConditionEvaluator Evaluator { get; }

    /// <summary>
    /// 按类型解析设备（被检 IDutDevice、共享标准盒 IStandardBox 等）。
    /// </summary>
    /// <typeparam name="T">设备接口类型。</typeparam>
    /// <returns>设备实例。</returns>
    T GetDevice<T>() where T : class, IDevice;

    /// <summary>
    /// 向 UI 推送一条实时消息。
    /// </summary>
    /// <param name="message">消息。</param>
    /// <param name="level">消息级别。</param>
    void Report(string message, RealtimeLevel level = RealtimeLevel.Info);

    /// <summary>
    /// 记录该测试项的实时采集数据序列（一次性，多通道，曲线图用，最终序列化落 test_process_data）。
    /// </summary>
    /// <param name="series">采集数据序列。</param>
    void RecordProcessData(ProcessDataSeries series);

    /// <summary>
    /// 开始一段实时采集：设定单位与通道名（顺序与后续 <see cref="ReportSample"/> 的值一致）。
    /// </summary>
    /// <param name="unit">采集量单位。</param>
    /// <param name="channelNames">各通道名。</param>
    void BeginSampling(string unit, params string[] channelNames);

    /// <summary>
    /// 上报一个采样点（timeSec=相对起点秒，values=各通道值）。实时推送到 UI 曲线，并累积落库。
    /// </summary>
    /// <param name="timeSec">相对起点秒。</param>
    /// <param name="values">各通道值。</param>
    void ReportSample(double timeSec, params double[] values);

    /// <summary>
    /// 当前测试项的参数集合。
    /// </summary>
    IReadOnlyList<ParameterDescriptor> Parameters => Step.Parameters;

    /// <summary>
    /// 当前测试项的判定条件集合。
    /// </summary>
    IReadOnlyList<ConditionDescriptor> Conditions => Step.Conditions;

    /// <summary>
    /// 读取命名参数（找不到返回 null）。
    /// </summary>
    /// <param name="name">参数名。</param>
    /// <returns>参数，或 null。</returns>
    ParameterDescriptor? Parameter(string name)
    {
        return Step.Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 读取命名设置项（找不到返回 null）。
    /// </summary>
    /// <param name="name">设置名。</param>
    /// <returns>设置值，或 null。</returns>
    string? Setting(string name)
    {
        return Step.Settings.TryGetValue(name, out var v) ? v : null;
    }
}

/// <summary>
/// 实时消息级别。
/// </summary>
public enum RealtimeLevel
{
    /// <summary>
    /// 普通信息。
    /// </summary>
    Info,

    /// <summary>
    /// 成功。
    /// </summary>
    Success,

    /// <summary>
    /// 警告。
    /// </summary>
    Warn,

    /// <summary>
    /// 错误。
    /// </summary>
    Error,
}

/// <summary>
/// 测试项结果结论。
/// </summary>
public enum StepOutcome
{
    /// <summary>
    /// 通过。
    /// </summary>
    Pass,

    /// <summary>
    /// 不通过（指标异常）。
    /// </summary>
    Fail,

    /// <summary>
    /// 跳过。
    /// </summary>
    Skip,

    /// <summary>
    /// 异常（通讯/工装）。
    /// </summary>
    Error,
}

/// <summary>
/// 测试项结果（取代旧 <c>Result</c>，去掉 WPF 耦合）。
/// </summary>
/// <param name="Outcome">结论。</param>
/// <param name="Summary">结果摘要。</param>
/// <param name="MeasuredValue">测量值（可空）。</param>
/// <param name="Detail">明细/异常堆栈（可空）。</param>
public sealed record StepResult(StepOutcome Outcome, string Summary, string? MeasuredValue = null, string? Detail = null)
{
    /// <summary>
    /// 是否通过。
    /// </summary>
    public bool IsPass => Outcome == StepOutcome.Pass;

    /// <summary>
    /// 落库的细分状态。Pass→Success，Fail→MetricFail，Error→默认工装通讯异常（被检异常用 Error(..., CommunicationError)）。
    /// </summary>
    public TestResultStatus Status { get; init; } = TestResultStatus.Success;

    /// <summary>
    /// 构造"通过"结果。
    /// </summary>
    /// <param name="summary">摘要。</param>
    /// <param name="value">测量值。</param>
    /// <returns>结果。</returns>
    public static StepResult Pass(string summary, string? value = null)
    {
        return new(StepOutcome.Pass, summary, value) { Status = TestResultStatus.Success };
    }

    /// <summary>
    /// 构造"不通过"结果（指标异常）。
    /// </summary>
    /// <param name="summary">摘要。</param>
    /// <param name="value">测量值。</param>
    /// <returns>结果。</returns>
    public static StepResult Fail(string summary, string? value = null)
    {
        return new(StepOutcome.Fail, summary, value) { Status = TestResultStatus.MetricFail };
    }

    /// <summary>
    /// 构造"跳过"结果。
    /// </summary>
    /// <param name="summary">摘要。</param>
    /// <returns>结果。</returns>
    public static StepResult Skip(string summary)
    {
        return new(StepOutcome.Skip, summary) { Status = TestResultStatus.Success };
    }

    /// <summary>
    /// 构造"异常"结果（默认工装通讯异常；被检异常传 CommunicationError）。
    /// </summary>
    /// <param name="summary">摘要。</param>
    /// <param name="detail">明细。</param>
    /// <param name="status">落库状态。</param>
    /// <returns>结果。</returns>
    public static StepResult Error(string summary, string? detail = null, TestResultStatus status = TestResultStatus.HardwareError)
    {
        return new(StepOutcome.Error, summary, null, detail) { Status = status };
    }
}
