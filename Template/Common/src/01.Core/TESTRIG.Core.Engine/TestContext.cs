using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Core.Engine;

/// <summary>
/// 每个测试项一份的运行上下文。设备从号位的 <see cref="IDeviceProvider"/> 强类型解析，无字符串反射。
/// </summary>
internal sealed class TestContext : ITestContext
{
    /// <summary>
    /// 该号位设备提供者。
    /// </summary>
    private readonly IDeviceProvider _devices;

    /// <summary>
    /// 实时消息回调。
    /// </summary>
    private readonly Action<string, RealtimeLevel> _report;

    /// <summary>
    /// 采样点回调（实时推 UI 曲线，可空）。
    /// </summary>
    private readonly Action<SampleEventArgs>? _onSample;

    /// <summary>
    /// 人工确认回调（弹 ManualConfirmDialog，可空——无 UI 订阅时 ConfirmAsync 返回 false）。
    /// 参数：(message, imagePath, ct) → true=OK，false=NG/超时/取消。
    /// </summary>
    private readonly Func<string, string?, CancellationToken, Task<bool>>? _confirm;

    /// <summary>
    /// 采集量单位。
    /// </summary>
    private string _unit = "V";

    /// <summary>
    /// 采集通道名。
    /// </summary>
    private string[] _channelNames = [];

    /// <summary>
    /// 采集起始时刻。
    /// </summary>
    private DateTime _samplingStart;

    /// <summary>
    /// 采集时间轴（相对秒）。
    /// </summary>
    private readonly List<double> _time = [];

    /// <summary>
    /// 各通道采集值。
    /// </summary>
    private readonly List<List<double>> _channelValues = [];

    /// <summary>
    /// 一次性记录的采集结果（无流式采集时用）。
    /// </summary>
    private ProcessDataSeries? _oneShot;

    /// <summary>
    /// 构造测试项运行上下文。
    /// </summary>
    /// <param name="devices">该号位设备提供者。</param>
    /// <param name="position">号位。</param>
    /// <param name="step">测试项。</param>
    /// <param name="evaluator">判定器。</param>
    /// <param name="logger">日志。</param>
    /// <param name="report">实时消息回调。</param>
    /// <param name="onSample">采样点回调（可空）。</param>
    /// <param name="confirm">人工确认回调（可空——无 UI 订阅时 ConfirmAsync 返回 false）。</param>
    public TestContext(
        IDeviceProvider devices,
        PositionDescriptor position,
        StepDescriptor step,
        IConditionEvaluator evaluator,
        ILogger logger,
        Action<string, RealtimeLevel> report,
        Action<SampleEventArgs>? onSample = null,
        Func<string, string?, CancellationToken, Task<bool>>? confirm = null
    )
    {
        _devices = devices;
        Position = position;
        Step = step;
        Evaluator = evaluator;
        Logger = logger;
        _report = report;
        _onSample = onSample;
        _confirm = confirm;
    }

    /// <summary>
    /// 当前号位/板位。
    /// </summary>
    public PositionDescriptor Position { get; }

    /// <summary>
    /// 当前测试项数据。
    /// </summary>
    public StepDescriptor Step { get; }

    /// <summary>
    /// 被检序列号。
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// 日志。
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// 判定器。
    /// </summary>
    public IConditionEvaluator Evaluator { get; }

    /// <summary>
    /// 按类型解析设备。
    /// </summary>
    /// <typeparam name="T">设备接口类型。</typeparam>
    /// <returns>设备实例。</returns>
    public T GetDevice<T>()
        where T : class, IDevice
    {
        return _devices.GetDevice<T>();
    }

    /// <summary>
    /// 按类型 + 实例键解析设备（标准模块等多实例设备）。
    /// </summary>
    /// <typeparam name="T">设备接口类型。</typeparam>
    /// <param name="deviceKey">实例键（manifest ToolDevices 的 Key）。</param>
    /// <returns>设备实例。</returns>
    public T GetDevice<T>(string deviceKey)
        where T : class, IDevice
    {
        return _devices.GetDevice<T>(deviceKey);
    }

    /// <summary>
    /// 推送一条实时消息。
    /// </summary>
    /// <param name="message">消息。</param>
    /// <param name="level">级别。</param>
    public void Report(string message, RealtimeLevel level = RealtimeLevel.Info)
    {
        _report(message, level);
    }

    /// <summary>
    /// 一次性记录采集数据序列。
    /// </summary>
    /// <param name="series">采集序列。</param>
    public void RecordProcessData(ProcessDataSeries series)
    {
        _oneShot = series;
    }

    /// <summary>
    /// 开始一段流式采集，设定单位与通道名。
    /// </summary>
    /// <param name="unit">采集量单位。</param>
    /// <param name="channelNames">各通道名。</param>
    public void BeginSampling(string unit, params string[] channelNames)
    {
        _unit = unit;
        _channelNames = channelNames;
        _samplingStart = DateTime.Now;
        _time.Clear();
        _channelValues.Clear();
        foreach (var _ in channelNames)
        {
            _channelValues.Add([]);
        }
    }

    /// <summary>
    /// 上报一个采样点，累积落库并实时推送 UI 曲线。
    /// </summary>
    /// <param name="timeSec">相对起点秒。</param>
    /// <param name="values">各通道值。</param>
    public void ReportSample(double timeSec, params double[] values)
    {
        _time.Add(timeSec);
        for (var i = 0; i < _channelValues.Count && i < values.Length; i++)
        {
            _channelValues[i].Add(values[i]);
        }

        _onSample?.Invoke(
            new SampleEventArgs
            {
                PositionIndex = Position.Index,
                StepKey = Step.Key,
                Unit = _unit,
                ChannelNames = _channelNames,
                TimeSec = timeSec,
                Values = values,
            }
        );
    }

    /// <summary>
    /// 弹出人工确认框，等待操作员 OK/NG。取消/NG/超时/无 UI 订阅返回 false。
    /// 复用 <see cref="TESTRIG.Core.Abstractions.ManualConfirmRequestedEventArgs"/> 与
    /// <c>ManualConfirmDialog</c>（整机模板订阅 <see cref="TestRunner.ManualConfirmRequested"/> 事件后弹出）。
    /// </summary>
    /// <param name="message">确认消息。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=OK；false=NG/超时/取消/无 UI。</returns>
    public Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
        => ConfirmAsync(message, null, ct);

    /// <summary>
    /// 弹出带图片的人工确认框，等待操作员 OK/NG。取消/NG/超时/无 UI 订阅返回 false。
    /// <paramref name="imagePath"/> 原样透传给 UI（pack URI 或文件路径）。
    /// </summary>
    /// <param name="message">确认消息。</param>
    /// <param name="imagePath">图片路径（可空）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=OK；false=NG/超时/取消/无 UI。</returns>
    public async Task<bool> ConfirmAsync(string message, string? imagePath, CancellationToken ct = default)
    {
        // 无 UI 订阅：按取消（false）返回，避免号位挂死
        if (_confirm is null)
        {
            return false;
        }
        return await _confirm(message, imagePath, ct);
    }

    /// <summary>
    /// 引擎在测试项执行后取出：优先流式采集结果，否则一次性记录的结果。
    /// </summary>
    public ProcessDataSeries? CollectedProcessData =>
        _time.Count > 0
            ? new ProcessDataSeries
            {
                Unit = _unit,
                StartedAt = _samplingStart,
                IntervalMs = _time.Count > 1 ? (int)Math.Round((_time[1] - _time[0]) * 1000) : 0,
                TimeSec = _time.ToList(),
                Channels = _channelNames
                    .Select((n, i) => new ProcessChannel(n, _channelValues[i].ToList()))
                    .ToList(),
            }
            : _oneShot;
}
