using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Core.Engine;

/// <summary>
/// 测试项进度阶段。
/// </summary>
public enum StepPhase
{
    /// <summary>
    /// 待执行。
    /// </summary>
    Pending,

    /// <summary>
    /// 执行中。
    /// </summary>
    Running,

    /// <summary>
    /// 已完成。
    /// </summary>
    Completed,
}

/// <summary>
/// 测试项进度事件参数。
/// </summary>
public sealed class StepProgressEventArgs : EventArgs
{
    /// <summary>
    /// 号位序号。
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 测试项。
    /// </summary>
    public required StepDescriptor Step { get; init; }

    /// <summary>
    /// 进度阶段。
    /// </summary>
    public required StepPhase Phase { get; init; }

    /// <summary>
    /// 结果（完成阶段有值）。
    /// </summary>
    public StepResult? Result { get; init; }
}

/// <summary>
/// 实时采集到的一个采样点（多通道，曲线图用）。
/// </summary>
public sealed class SampleEventArgs : EventArgs
{
    /// <summary>
    /// 号位序号。
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 所属测试项 Key。
    /// </summary>
    public required string StepKey { get; init; }

    /// <summary>
    /// 采集量单位。
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// 各通道名。
    /// </summary>
    public required IReadOnlyList<string> ChannelNames { get; init; }

    /// <summary>
    /// 相对起点秒。
    /// </summary>
    public required double TimeSec { get; init; }

    /// <summary>
    /// 各通道值。
    /// </summary>
    public required IReadOnlyList<double> Values { get; init; }
}

/// <summary>
/// 实时消息事件参数。
/// </summary>
public sealed class RealtimeMessageEventArgs : EventArgs
{
    /// <summary>
    /// 号位序号。
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 消息所属测试项 Key；null 表示工位级消息（开始/结束）。
    /// </summary>
    public string? StepKey { get; init; }

    /// <summary>
    /// 消息内容。
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 消息级别。
    /// </summary>
    public RealtimeLevel Level { get; init; }

    /// <summary>
    /// 消息时刻。
    /// </summary>
    public DateTime At { get; init; } = DateTime.Now;
}

/// <summary>运行选项：选哪些号位、各号位条码、是否单项过滤、是否并行，以及落库元数据。</summary>
public sealed record RunOptions
{
    /// <summary>
    /// 要跑的号位（null=全部）。
    /// </summary>
    public IReadOnlyCollection<int>? Positions { get; init; }

    /// <summary>
    /// 各号位预置条码（号位号 → 条码）。
    /// </summary>
    public IReadOnlyDictionary<int, string>? SerialNumbers { get; init; }

    /// <summary>
    /// 失败即停。
    /// </summary>
    public bool StopOnFail { get; init; } = false;

    /// <summary>
    /// 仅跑这些测试项 Key（单测/重测用）；null 表示全部（=全测，写主表）。
    /// </summary>
    public IReadOnlyCollection<string>? StepKeys { get; init; }

    /// <summary>
    /// 单个测试项最多尝试次数（含首次）。**默认 1 = 不重试**，与引擎原有行为一致。
    /// 重试是**按板卡按测试项**决定的策略，不应由引擎一刀切：不同产品线的处理器副作用差别很大
    /// （如 PS02 涉及固件烧录、P06/P21 有机械动作），盲目重跑代价和风险都不可控。
    /// 需要重试的场景请在对应板卡的处理器内部实现（如 218A 用 <c>Retry218A.OnCommErrorAsync</c>），
    /// 那里能精确区分「通讯抖动」与「指标不合格」，也能避开有副作用的步骤。
    /// </summary>
    public int StepAttempts { get; init; } = 1;

    /// <summary>
    /// 多工位是否并行执行（对齐产线：各工位同时跑）。默认 true。
    /// </summary>
    public bool Parallel { get; init; } = true;

    /// <summary>
    /// 批次号（落库元数据）。
    /// </summary>
    public string? BatchNo { get; init; }

    /// <summary>
    /// 操作员（落库元数据）。
    /// </summary>
    public string? Operator { get; init; }

    /// <summary>
    /// 自动化工位号（A/B/C/空）。
    /// </summary>
    public string? StationNo { get; init; }

    /// <summary>
    /// 号位是否被选中执行。
    /// </summary>
    /// <param name="index">号位号。</param>
    /// <returns>是否选中。</returns>
    internal bool IsPositionSelected(int index)
    {
        return Positions is null || Positions.Contains(index);
    }

    /// <summary>
    /// 测试项是否被选中执行。
    /// </summary>
    /// <param name="key">测试项 Key。</param>
    /// <returns>是否选中。</returns>
    internal bool IsStepSelected(string key)
    {
        return StepKeys is null || StepKeys.Contains(key);
    }

    /// <summary>
    /// 取号位预置条码（无则 null）。
    /// </summary>
    /// <param name="index">号位号。</param>
    /// <returns>条码或 null。</returns>
    internal string? SerialOf(int index)
    {
        return SerialNumbers != null && SerialNumbers.TryGetValue(index, out var s) ? s : null;
    }
}

/// <summary>
/// 测试流程引擎：按 号位 × 有序测试项 驱动，按 (设备家族, Kind) 派发到 <see cref="IStepHandler"/>。
/// 多工位**并行**执行（对齐产线）。对设备类型、对具体板子都零感知——新增板子无需改引擎。
/// </summary>
public sealed class TestRunner
{
    // test_process_data 序列化：camelCase，紧凑（曲线数据点多）
    private static readonly System.Text.Json.JsonSerializerOptions ProcessDataJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    // (deviceFamily|"", kind) -> handler；deviceFamily="" 表示通用
    private readonly Dictionary<(string family, string kind), IStepHandler> _handlers;

    // deviceFamily -> lifecycle handler
    private readonly Dictionary<string, IBoardLifecycleHandler> _lifecycleHandlers;

    // 已执行过整体测试前的 DeviceFamily 集合（每 DeviceFamily 仅执行一次）
    private readonly HashSet<string> _preTestCompleted = [];

    private readonly IConditionEvaluator _evaluator;
    private readonly IDeviceProviderFactory _deviceFactory;
    private readonly ILogger<TestRunner> _logger;

    /// <summary>
    /// 构造测试引擎，按 (设备家族, Kind) 索引所有处理器（重复即报错）。
    /// </summary>
    /// <param name="handlers">全部测试项处理器（DI 自动扫描注入）。</param>
    /// <param name="evaluator">判定器。</param>
    /// <param name="deviceFactory">设备提供者工厂。</param>
    /// <param name="logger">日志。</param>
    public TestRunner(
        IEnumerable<IStepHandler> handlers,
        IEnumerable<IBoardLifecycleHandler> lifecycleHandlers,
        IConditionEvaluator evaluator,
        IDeviceProviderFactory deviceFactory,
        ILogger<TestRunner> logger)
    {
        // 自动扫描注册下，重复的 (设备家族, Kind) 多半是复制处理器后忘改 Kind —— 给出清晰报错
        _handlers = new Dictionary<(string family, string kind), IStepHandler>();
        foreach (var h in handlers)
        {
            // 处理器为 DI 单例、多号位并行共用同一实例：禁止可变实例状态，否则号位间串数据（偶发不可复现）。
            EnsureStateless(h);

            var key = (h.DeviceFamily ?? "", h.Kind);
            if (!_handlers.TryAdd(key, h))
            {
                throw new InvalidOperationException(
                    $"重复的测试项处理器：设备家族='{key.Item1}' Kind='{h.Kind}'（{h.GetType().Name} 与 {_handlers[key].GetType().Name} 冲突）");
            }
        }

        // 索引生命周期处理器：每 DeviceFamily 最多一个
        _lifecycleHandlers = new Dictionary<string, IBoardLifecycleHandler>();
        foreach (var lh in lifecycleHandlers)
        {
            if (!_lifecycleHandlers.TryAdd(lh.DeviceFamily, lh))
            {
                throw new InvalidOperationException(
                    $"重复的板级生命周期处理器：DeviceFamily='{lh.DeviceFamily}'（{lh.GetType().Name} 与 {_lifecycleHandlers[lh.DeviceFamily].GetType().Name} 冲突）");
            }
        }

        _evaluator = evaluator;
        _deviceFactory = deviceFactory;
        _logger = logger;
    }

    /// <summary>
    /// 启动期守卫：处理器为 DI 单例、多号位**并行**共用同一实例，因此必须无状态。
    /// 凡有可变实例字段（含可变自动属性 <c>{ get; set; }</c> 的 backing field）即报错——
    /// 注入依赖存 <c>readonly</c> 字段、<c>{ get; }</c> 只读属性均放行；状态只应进局部变量或 <c>ITestContext</c>。
    /// 这样号位间不会串数据（那类 bug 偶发、不可复现、最难查），把约定变成开机即拦的硬约束。
    /// </summary>
    /// <param name="handler">待校验的处理器实例。</param>
    private static void EnsureStateless(IStepHandler handler)
    {
        var offenders = new List<string>();
        for (var t = handler.GetType(); t is not null && t != typeof(object); t = t.BaseType)
        {
            var fields = t.GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var f in fields)
            {
                // readonly（含 { get; } 只读自动属性的 backing field）安全；其余可变实例字段禁止。
                if (!f.IsInitOnly)
                {
                    offenders.Add(f.Name);
                }
            }
        }

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                $"测试项处理器 {handler.GetType().Name} 含可变实例字段 [{string.Join(", ", offenders)}]。" +
                "处理器是并行共用的单例，禁止可变状态（会致号位间串数据）；" +
                "请改用局部变量或 ITestContext 承载状态，注入依赖存 readonly 字段。");
        }
    }

    /// <summary>
    /// 测试项进度变更事件。
    /// </summary>
    public event EventHandler<StepProgressEventArgs>? StepChanged;

    /// <summary>
    /// 实时消息事件。
    /// </summary>
    public event EventHandler<RealtimeMessageEventArgs>? Message;

    /// <summary>
    /// 实时采集数据点（曲线图用）。
    /// </summary>
    public event EventHandler<SampleEventArgs>? SampleReported;

    /// <summary>
    /// 人工确认请求事件（<c>StepType=Manual</c> 的测试项）：暂停该号位，发布确认请求，
    /// 等待 UI 弹确认框回传 OK/NG；不阻塞其他号位。整机模板订阅弹 <c>ManualConfirmDialog</c>。
    /// </summary>
    public event EventHandler<ManualConfirmRequestedEventArgs>? ManualConfirmRequested;

    /// <summary>按 (设备家族, Kind) 解析处理器：设备特有优先，回落通用。</summary>
    private IStepHandler? ResolveHandler(string deviceFamily, string kind)
    {
        if (_handlers.TryGetValue((deviceFamily, kind), out var specific))
        {
            return specific;
        }

        if (_handlers.TryGetValue(("", kind), out var shared))
        {
            return shared;
        }

        return null;
    }

    /// <summary>
    /// 运行一次测试会话（按选项选号位/测试项，可并行多工位），聚合各号位结果。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="options">运行选项（null=默认全跑）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>会话结果。</returns>
    public async Task<TestSessionResult> RunAsync(JigManifest manifest, RunOptions? options = null, CancellationToken ct = default)
    {
        options ??= new RunOptions();
        var started = DateTime.Now;

        // 首次运行前执行整体测试前（每 DeviceFamily 仅执行一次）
        var preTestResult = await RunPreTestIfNeededAsync(manifest, ct);
        // 整体测试前失败时仍继续运行（报警但不阻止），由日志记录

        var positions = manifest.Positions
            .Where(p => options.IsPositionSelected(p.Index))
            .OrderBy(p => p.Index)
            .ToList();

        List<PositionResult> results;
        if (options.Parallel)
        {
            // 多工位并行（各工位独立设备提供者，互不干扰）
            var tasks = positions.Select(p => RunPositionAsync(manifest, p, options, ct)).ToList();
            results = (await Task.WhenAll(tasks)).OrderBy(r => r.Position.Index).ToList();
        }
        else
        {
            results = [];
            foreach (var p in positions)
            {
                results.Add(await RunPositionAsync(manifest, p, options, ct));
            }
        }

        var passed = results.Count > 0 && results.All(r => r.Passed);
        _logger.LogInformation("会话 {Task} 结束：{Result}", manifest.Key, passed ? "通过" : "不通过");
        return new TestSessionResult(manifest.Key, results, passed, started, DateTime.Now)
        {
            BatchNo = options.BatchNo,
            Operator = options.Operator,
            StationNo = options.StationNo,
            DeviceModel = manifest.Dut.Model,
            FullRun = options.StepKeys is null,   // 未按测试项过滤 = 跑了全部测试项
            ExpectedStepCount = positions.Count * manifest.Steps.Count,
        };
    }

    /// <summary>
    /// 运行单个号位的全部（选中）测试项，逐项派发处理器、判定、记录、落库元数据。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="pos">号位。</param>
    /// <param name="options">运行选项。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>号位结果。</returns>
    private async Task<PositionResult> RunPositionAsync(JigManifest manifest, PositionDescriptor pos, RunOptions options, CancellationToken ct)
    {
        // await using：本号位跑完释放被检连接（串口/网络），避免下次运行端口仍被占（如串口 COMx）。共享标准盒不受影响。
        await using var provider = _deviceFactory.Create(manifest, pos);
        var records = new List<StepRecord>();
        bool posPassed = true;
        bool fatalAbort = false;   // 测试项抛异常（通讯/串口/硬件/未预期）→ 终止本工位后续测试项
        var serialNo = options.SerialOf(pos.Index);

        void Report(string? stepKey, string msg, RealtimeLevel lvl)
        {
            Message?.Invoke(this, new RealtimeMessageEventArgs { PositionIndex = pos.Index, StepKey = stepKey, Message = msg, Level = lvl });
        }

        var steps = manifest.Steps.Where(s => options.IsStepSelected(s.Key)).OrderBy(s => s.Order).ToList();
        Report(null, $"=== [{pos.Name}] 开始，共 {steps.Count} 项 ===", RealtimeLevel.Info);

        bool userStopped = false;   // 用户点「停止」→ 优雅收尾并落盘已测数据

        foreach (var step in steps)
        {
            // 取消不再抛异常丢结果：项间被取消则优雅停止，保留已跑数据供落盘
            if (ct.IsCancellationRequested)
            {
                userStopped = true;
                break;
            }
            StepChanged?.Invoke(this, new StepProgressEventArgs { PositionIndex = pos.Index, Step = step, Phase = StepPhase.Running });

            var stepStarted = DateTime.Now;
            var processInfos = new List<string>();   // 该项过程日志（落 test_process_infos）
            void StepReport(string m, RealtimeLevel lvl)
            {
                processInfos.Add($"{DateTime.Now:HH:mm:ss.fff} {m}");
                Report(step.Key, m, lvl);
            }

            // 测试项级重试（PORT: 旧 WatchAndProcessIntergrade 的 SetRetryCount(pos, 3)）。
            // 不通过就整项重来，最多 options.StepAttempts 次；通讯异常也先重试，重试用尽才判致命并终止本工位。
            var maxAttempts = Math.Max(1, options.StepAttempts);
            TestContext ctx = null!;
            StepResult result;
            var attempt = 0;

            // ---- Manual 人工确认步（整机模板专属）：暂停本号位 → 弹确认框等操作员 OK/NG（可选超时），
            //      确认只发生一次，不参与自动重试；确认 NG/超时按不合格收尾 ----
            if (step.StepType.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            {
                ctx = new TestContext(provider, pos, step, _evaluator, _logger, StepReport,
                    s => SampleReported?.Invoke(this, s),
                    (msg, img, ct2) => RequestConfirmAsync(pos.Index, step, msg, img, ct2))
                { SerialNumber = serialNo };
                result = await ConfirmManualAsync(ctx, step, ct);
                serialNo = ctx.SerialNumber ?? serialNo;
            }
            else
            {
            while (true)
            {
                attempt++;
                if (attempt > 1)
                {
                    StepReport($"—— 本项第 {attempt}/{maxAttempts} 次尝试 ——", RealtimeLevel.Warn);
                }

                ctx = new TestContext(provider, pos, step, _evaluator, _logger, StepReport,
                    s => SampleReported?.Invoke(this, s),
                    (msg, img, ct2) => RequestConfirmAsync(pos.Index, step, msg, img, ct2))
                { SerialNumber = serialNo };

                var fatal = false;
                try
                {
                    var handler = ResolveHandler(manifest.DeviceFamily, step.Kind);
                    if (handler is null)
                    {
                        result = StepResult.Error($"未注册测试项处理器：设备={manifest.DeviceFamily} Kind={step.Kind}");
                        fatal = true;
                    }
                    else
                    {
                        result = await handler.ExecuteAsync(ctx, ct);
                        serialNo = ctx.SerialNumber ?? serialNo;
                    }
                }
                catch (OperationCanceledException)   // 用户停止：本项按「已停止」收尾（发完成事件解转圈），保留数据
                {
                    _logger.LogInformation("测试项 {Step} 被用户停止", step.Key);
                    result = StepResult.Skip("测试已停止");
                    userStopped = true;
                }
                catch (DeviceCommException dce)   // 被检/共享设备通讯异常：重试用尽才终止本工位后续项
                {
                    _logger.LogError(dce, "测试项 {Step} 通讯异常（第 {N}/{Max} 次）", step.Key, attempt, maxAttempts);
                    result = StepResult.Error(dce.Message, dce.ToString(), dce.Status);
                    fatal = true;
                }
                catch (Exception ex)   // 未预期异常（原生串口 UnauthorizedAccess/IO、网络 Socket、代码 bug 等）
                {
                    _logger.LogError(ex, "测试项 {Step} 异常（第 {N}/{Max} 次）", step.Key, attempt, maxAttempts);
                    result = StepResult.Error(ex.Message, ex.ToString());
                    fatal = true;
                }

                if (result.IsPass || result.Outcome == StepOutcome.Skip || userStopped || attempt >= maxAttempts)
                {
                    // 重试用尽仍是异常类失败才判致命，避免一次抖动就掐掉整个工位
                    fatalAbort = fatal && !result.IsPass && result.Outcome != StepOutcome.Skip;
                    break;
                }

                StepReport($"本次未通过（{result.Summary}），稍后重试", RealtimeLevel.Warn);
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    userStopped = true;
                    break;
                }
            }
            }

            records.Add(new StepRecord(step, result, stepStarted, DateTime.Now)
            {
                ProcessInfos = processInfos.Count > 0 ? string.Join("\n", processInfos) : null,
                ProcessData = ctx.CollectedProcessData is { } pd
                    ? System.Text.Json.JsonSerializer.Serialize(pd, ProcessDataJson)
                    : null,
            });
            if (!result.IsPass && result.Outcome != StepOutcome.Skip)
            {
                posPassed = false;
            }

            Report(step.Key, $"=> {result.Outcome}：{result.Summary}",
                result.IsPass ? RealtimeLevel.Success : RealtimeLevel.Error);
            StepChanged?.Invoke(this, new StepProgressEventArgs { PositionIndex = pos.Index, Step = step, Phase = StepPhase.Completed, Result = result });

            if (userStopped)
            {
                break;
            }

            if (fatalAbort)
            {
                Report(null, $"[{pos.Name}] 因异常终止测试：{result.Summary}", RealtimeLevel.Error);
                break;
            }

            if (options.StopOnFail && !result.IsPass && result.Outcome != StepOutcome.Skip)
            {
                break;
            }
        }

        var endTag = userStopped ? "已停止" : fatalAbort ? "异常中止" : posPassed ? "通过" : "不通过";
        Report(null, $"=== [{pos.Name}] 结束：{endTag} ===", posPassed && !fatalAbort ? RealtimeLevel.Success : RealtimeLevel.Error);
        return new PositionResult(pos, serialNo, records, posPassed);
    }

    /// <summary>
    /// 人工确认步（<c>StepType=Manual</c>）：发布「等待人工确认」事件（携带测试项与超时配置），
    /// 暂停该号位等待操作员确认；OK → 通过，NG → 不合格，超时 → 按不合格收尾。不阻塞其他号位。
    /// 无 UI 订阅（事件无处理程序）时按异常收尾，避免挂死。
    /// </summary>
    /// <param name="ctx">当前测试项上下文。</param>
    /// <param name="step">测试项。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>确认结果。</returns>
    private async Task<StepResult> ConfirmManualAsync(TestContext ctx, StepDescriptor step, CancellationToken ct)
    {
        if (ManualConfirmRequested is null)
        {
            return StepResult.Error($"人工确认步未绑定确认 UI：{step.Key}");
        }

        var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 0;
        var tcs = new TaskCompletionSource<ManualConfirmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ManualConfirmRequested.Invoke(this, new ManualConfirmRequestedEventArgs(ctx.Position.Index, step, timeoutMs, tcs));

        ManualConfirmResult confirm;
        if (timeoutMs > 0)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, timeoutCts.Token));
                confirm = completed == tcs.Task ? await tcs.Task : ManualConfirmResult.Timeout;
                timeoutCts.Cancel();
            }
            catch (OperationCanceledException)
            {
                // 用户停止：按已停止跳过
                return StepResult.Skip("测试已停止");
            }
        }
        else
        {
            try
            {
                confirm = await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return StepResult.Skip("测试已停止");
            }
        }

        return confirm switch
        {
            ManualConfirmResult.Ok => StepResult.Pass($"人工确认通过：{step.Name}"),
            ManualConfirmResult.Ng => StepResult.Fail($"人工确认不合格：{step.Name}"),
            _ => StepResult.Fail($"人工确认超时（{timeoutMs}ms）：{step.Name}"),
        };
    }

    /// <summary>
    /// 内联人工确认：测试项处理器执行中调用 <see cref="ITestContext.ConfirmAsync"/> 时经此方法。
    /// 发布 <see cref="ManualConfirmRequested"/> 事件（携带 message/imagePath）复用 <c>ManualConfirmDialog</c>，
    /// 等待操作员 OK/NG；OK→true，NG/超时/取消→false。无 UI 订阅时返回 false（避免号位挂死）。
    /// 与 <see cref="ConfirmManualAsync"/> 不同：不参与 StepType=Manual 流程、无超时配置、不构造 StepResult。
    /// </summary>
    /// <param name="positionIndex">号位索引。</param>
    /// <param name="step">当前测试项（用作弹窗标题上下文）。</param>
    /// <param name="message">确认消息（弹窗主体）。</param>
    /// <param name="imagePath">附图路径（可空）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=操作员确认 OK；false=NG/超时/取消/无 UI。</returns>
    private async Task<bool> RequestConfirmAsync(
        int positionIndex,
        StepDescriptor step,
        string? message,
        string? imagePath,
        CancellationToken ct)
    {
        // 无 UI 订阅：按取消（false）返回，避免号位挂死
        if (ManualConfirmRequested is null)
        {
            _logger.LogWarning("内联确认无 UI 订阅，按取消返回：{Step}", step.Key);
            return false;
        }

        var tcs = new TaskCompletionSource<ManualConfirmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var args = new ManualConfirmRequestedEventArgs(positionIndex, step, 0, tcs)
        {
            Message = message,
            ImagePath = imagePath,
        };

        // 取消令牌：触发时把结论置为 Timeout（→ false），让 await 早日返回
        using var registration = ct.Register(() => tcs.TrySetResult(ManualConfirmResult.Timeout));
        ManualConfirmRequested.Invoke(this, args);

        try
        {
            var result = await tcs.Task;
            return result == ManualConfirmResult.Ok;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// 整体测试前：同一 DeviceFamily 仅首次运行前执行一次。
    /// 若生命周期处理器指定了 <see cref="IBoardLifecycleHandler.ManifestKey"/>，还须 manifest.Key 匹配才执行。
    /// </summary>
    private async Task<StepResult?> RunPreTestIfNeededAsync(JigManifest manifest, CancellationToken ct)
    {
        if (!_lifecycleHandlers.TryGetValue(manifest.DeviceFamily, out var lifecycle))
        {
            return null;
        }

        if (lifecycle.ManifestKey is not null && lifecycle.ManifestKey != manifest.Key)
        {
            return null;
        }

        if (!_preTestCompleted.Add(manifest.DeviceFamily))
        {
            return null;
        }

        return await RunLifecycleStepAsync(manifest, lifecycle.OnPreTestAsync, "整体测试前", ct);
    }

    /// <summary>
    /// 整体测试后：在应用关闭前调用。
    /// 若生命周期处理器指定了 <see cref="IBoardLifecycleHandler.ManifestKey"/>，还须 manifest.Key 匹配才执行。
    /// </summary>
    public async Task<StepResult?> RunPostTestAsync(JigManifest manifest, CancellationToken ct = default)
    {
        if (!_lifecycleHandlers.TryGetValue(manifest.DeviceFamily, out var lifecycle))
        {
            return null;
        }

        if (lifecycle.ManifestKey is not null && lifecycle.ManifestKey != manifest.Key)
        {
            return null;
        }

        return await RunLifecycleStepAsync(manifest, lifecycle.OnPostTestAsync, "整体测试后", ct);
    }

    /// <summary>
    /// 执行生命周期步骤的通用方法。使用 manifest 的第一个号位创建设备提供者。
    /// </summary>
    private async Task<StepResult?> RunLifecycleStepAsync(
        JigManifest manifest,
        Func<ITestContext, CancellationToken, Task<StepResult>> stepFunc,
        string stepName,
        CancellationToken ct)
    {
        var firstPos = manifest.Positions.FirstOrDefault();
        if (firstPos is null)
        {
            _logger.LogWarning("{Step} 跳过：清单 {Manifest} 无号位", stepName, manifest.Key);
            return null;
        }

        try
        {
            await using var provider = _deviceFactory.Create(manifest, firstPos);
            var placeholderStep = new StepDescriptor
            {
                Key = $"Lifecycle.{stepName}", Kind = stepName, Name = stepName,
                Settings = new Dictionary<string, string>(), Parameters = [], Conditions = [],
            };
            var ctx = new TestContext(provider, firstPos, placeholderStep, _evaluator, _logger,
                (msg, lvl) => Message?.Invoke(this, new RealtimeMessageEventArgs { PositionIndex = firstPos.Index, Message = msg, Level = lvl }),
                confirm: (msg, img, ct2) => RequestConfirmAsync(firstPos.Index, placeholderStep, msg, img, ct2));

            _logger.LogInformation("{Step} 开始（DeviceFamily={Family}）", stepName, manifest.DeviceFamily);
            var result = await stepFunc(ctx, ct);
            _logger.LogInformation("{Step} 结束：{Outcome}", stepName, result.Outcome);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{Step} 被取消", stepName);
            return StepResult.Skip($"{stepName}已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Step} 异常", stepName);
            return StepResult.Error($"{stepName}异常：{ex.Message}", ex.ToString());
        }
    }
}
