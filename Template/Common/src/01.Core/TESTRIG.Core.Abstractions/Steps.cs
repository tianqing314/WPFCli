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
    /// 按类型 + 实例键解析设备（标准模块等多实例设备：如 DPSEX1 / DPSEX2）。
    /// </summary>
    /// <typeparam name="T">设备接口类型。</typeparam>
    /// <param name="deviceKey">实例键（manifest ToolDevices 的 Key）。</param>
    /// <returns>设备实例。</returns>
    T GetDevice<T>(string deviceKey) where T : class, IDevice;

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
    /// 弹出人工确认框（复用 <c>ManualConfirmDialog</c>），等待操作员 OK/NG。
    /// 取消 / NG / 超时返回 false。用于旧脚本的 <c>OpenInfoConfirmWindow</c> 等场景：
    /// 测试项执行中途请求人工确认，取消通常意味失败，调用方按 <c>if (!(await ctx.ConfirmAsync(msg))) pass = false;</c> 处理。
    /// 无 UI 订阅时返回 false（避免号位挂死）。
    /// </summary>
    /// <param name="message">确认消息（显示在弹窗主体）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=操作员确认通过；false=不合格/超时/取消/无 UI。</returns>
    Task<bool> ConfirmAsync(string message, CancellationToken ct = default);

    /// <summary>
    /// 弹出带图片的人工确认框（如指引图、参考照片），等待操作员 OK/NG。取消/NG/超时返回 false。
    /// 用于旧脚本的 <c>OpenInfoImgConfirmWindow</c> 场景。<paramref name="imagePath"/> 原样透传给 UI，
    /// 可为 pack URI（如 <c>pack://application:,,,/Assy;Component/images/x.png</c>）或文件路径，UI 端按需解析。
    /// </summary>
    /// <param name="message">确认消息。</param>
    /// <param name="imagePath">图片路径（pack URI 或文件路径）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=操作员确认通过；false=不合格/超时/取消/无 UI。</returns>
    Task<bool> ConfirmAsync(string message, string? imagePath, CancellationToken ct = default);

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
/// 人工确认结论（整机模板 ManualStep：操作员观察后点按）。
/// </summary>
public enum ManualConfirmResult
{
    /// <summary>
    /// 操作员确认通过。
    /// </summary>
    Ok,

    /// <summary>
    /// 操作员确认不合格。
    /// </summary>
    Ng,

    /// <summary>
    /// 超时未确认（引擎按不合格收尾）。
    /// </summary>
    Timeout,
}

/// <summary>
/// 人工确认请求事件参数：<see cref="TestRunner"/> 执行到 <c>StepType=Manual</c> 的测试项时发布，
/// 或测试项处理器执行中调用 <see cref="ITestContext.ConfirmAsync"/> 时发布，
/// UI 订阅后弹出确认框（说明/操作指引 + OK/NG 按钮 + 可选超时 + 可选附图），操作员确认后调 <see cref="Respond"/> 回传。
/// 该号位暂停等待，不阻塞其他号位。
/// </summary>
public sealed class ManualConfirmRequestedEventArgs : EventArgs
{
    /// <summary>
    /// 等待确认的号位。
    /// </summary>
    public int PositionIndex { get; }

    /// <summary>
    /// 等待确认的测试项。
    /// </summary>
    public StepDescriptor Step { get; }

    /// <summary>
    /// 确认超时毫秒数（0 = 不限时）。
    /// </summary>
    public int TimeoutMs { get; }

    /// <summary>
    /// 内联确认消息（测试项执行中调用 <see cref="ITestContext.ConfirmAsync"/> 时设置）。
    /// null = 整机 ManualStep 流程，UI 用 <see cref="Step"/> 的 Name/Description 显示；
    /// 非空 = 内联确认，UI 用此消息作为弹窗主体文本（标题仍用 <see cref="Step"/>.Name）。
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 内联确认附图路径（pack URI 或文件路径）。null = 无图。
    /// 形如 <c>pack://application:,,,/Assy;Component/images/x.png</c> 原样透传，UI 端按需解析资源。
    /// </summary>
    public string? ImagePath { get; init; }

    /// <summary>
    /// 回传确认结果的通道（UI 调用 <see cref="Respond"/>）。
    /// </summary>
    private readonly TaskCompletionSource<ManualConfirmResult> _tcs;

    /// <summary>
    /// 构造确认事件参数。
    /// </summary>
    /// <param name="positionIndex">号位索引。</param>
    /// <param name="step">测试项。</param>
    /// <param name="timeoutMs">超时毫秒数。</param>
    /// <param name="tcs">回传通道。</param>
    public ManualConfirmRequestedEventArgs(int positionIndex, StepDescriptor step, int timeoutMs,
        TaskCompletionSource<ManualConfirmResult> tcs)
    {
        PositionIndex = positionIndex;
        Step = step;
        TimeoutMs = timeoutMs;
        _tcs = tcs;
    }

    /// <summary>
    /// UI 回传人工确认结果（OK/NG；超时由引擎按 TimeoutMs 处理，UI 无需传 Timeout）。
    /// </summary>
    /// <param name="result">确认结果。</param>
    public void Respond(ManualConfirmResult result) => _tcs.TrySetResult(result);
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
