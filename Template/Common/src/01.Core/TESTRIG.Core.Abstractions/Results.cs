namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 设备通讯异常。真机驱动（标准盒/被检）遇通讯失败抛此，携带落库状态，
/// 引擎据此区分 <see cref="TestResultStatus.HardwareError"/>（工装/标准盒）与
/// <see cref="TestResultStatus.CommunicationError"/>（被检）。
/// </summary>
public sealed class DeviceCommException : Exception
{
    /// <summary>
    /// 落库状态（HardwareError / CommunicationError）。
    /// </summary>
    public TestResultStatus Status { get; }

    /// <summary>
    /// 构造设备通讯异常。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <param name="status">落库状态。</param>
    /// <param name="inner">内部异常（可空）。</param>
    public DeviceCommException(string message, TestResultStatus status, Exception? inner = null)
        : base(message, inner)
    {
        Status = status;
    }
}

/// <summary>
/// 测试结果状态（落库 result_status）。
/// </summary>
public enum TestResultStatus
{
    /// <summary>
    /// 测试通过。
    /// </summary>
    Success,

    /// <summary>
    /// 指标异常（测量值不在判定范围内）。
    /// </summary>
    MetricFail,

    /// <summary>
    /// 工装通讯异常（标准盒/PLC）。
    /// </summary>
    HardwareError,

    /// <summary>
    /// 被检通讯异常。
    /// </summary>
    CommunicationError,
}

/// <summary>
/// 一次完整测试会话的结果（多号位聚合）。一次执行=一个 <see cref="TaskId"/>。
/// </summary>
/// <param name="TaskKey">任务 Key（=manifest.Key）。</param>
/// <param name="Positions">各号位结果。</param>
/// <param name="Passed">整体是否通过。</param>
/// <param name="StartedAt">开始时刻。</param>
/// <param name="FinishedAt">结束时刻。</param>
public sealed record TestSessionResult(
    string TaskKey,
    IReadOnlyList<PositionResult> Positions,
    bool Passed,
    DateTime StartedAt,
    DateTime FinishedAt)
{
    /// <summary>
    /// 本次执行任务ID（同一次运行的所有号位/测试项共用）。
    /// </summary>
    public Guid TaskId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 批次号。
    /// </summary>
    public string? BatchNo { get; init; }

    /// <summary>
    /// 设备型号（来自 manifest.Dut.Model）。
    /// </summary>
    public string? DeviceModel { get; init; }

    /// <summary>
    /// 自动化工位号（A/B/C/空）。
    /// </summary>
    public string? StationNo { get; init; }

    /// <summary>
    /// 操作员。
    /// </summary>
    public string? Operator { get; init; }

    /// <summary>
    /// 是否跑了全部测试项——只有全测才写/更新主表。
    /// </summary>
    public bool FullRun { get; init; }

    /// <summary>
    /// 本次会话按所选号位应执行的测试项总数。用于区分“全选但中途停止”和真正完成全部测试。
    /// </summary>
    public int ExpectedStepCount { get; init; }

    /// <summary>
    /// 是否为重压后的重测会话（自动化异常/不合格 → 重新压合再测）。
    /// 为真时主表对应 SN 标记「已重压」，供数据追溯。
    /// </summary>
    public bool IsRePress { get; init; }
}

/// <summary>
/// 单号位/板位结果。
/// </summary>
/// <param name="Position">号位。</param>
/// <param name="SerialNumber">被检序列号。</param>
/// <param name="Steps">各测试项记录。</param>
/// <param name="Passed">该号位是否通过。</param>
public sealed record PositionResult(
    PositionDescriptor Position,
    string? SerialNumber,
    IReadOnlyList<StepRecord> Steps,
    bool Passed);

/// <summary>
/// 单测试项执行记录（落子表 pcba_test_data_details 一行）。
/// </summary>
/// <param name="Step">测试项描述。</param>
/// <param name="Result">测试项结果。</param>
/// <param name="StartedAt">开始时刻。</param>
/// <param name="FinishedAt">结束时刻。</param>
public sealed record StepRecord(StepDescriptor Step, StepResult Result, DateTime StartedAt, DateTime FinishedAt)
{
    /// <summary>
    /// 测试过程日志信息（按流程记录，落 test_process_infos）。
    /// </summary>
    public string? ProcessInfos { get; init; }

    /// <summary>
    /// 测试过程实时数据采集（JSON，落 test_process_data，曲线图用；暂无采集源时为 null）。
    /// </summary>
    public string? ProcessData { get; init; }
}

/// <summary>
/// SN 生成：一次运行（一块拼版）同一时间戳，按号位号区分（yyyyMMddHHmmss-号位号）。
/// 时间戳单调递增——同一秒内连续生成（如全自动快速换板）会进位到下一秒，
/// 保证每次调用、每块板的 SN 都唯一（避免"换板后 SN 不变"）。
/// </summary>
public static class SerialNumberFactory
{
    /// <summary>
    /// 时间戳单调递增的并发锁。
    /// </summary>
    private static readonly object Lock = new();

    /// <summary>
    /// 上次分配的时间戳。
    /// </summary>
    private static DateTime _last = DateTime.MinValue;

    /// <summary>
    /// 为一组号位生成唯一 SN（号位号 → SN）。
    /// </summary>
    /// <param name="positionIndexes">号位号集合。</param>
    /// <returns>号位号 → SN 字典。</returns>
    public static IReadOnlyDictionary<int, string> Generate(IEnumerable<int> positionIndexes)
    {
        DateTime ts;
        lock (Lock)
        {
            var now = DateTime.Now;
            ts = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            if (ts <= _last)
            {
                ts = _last.AddSeconds(1);
            }

            _last = ts;
        }
        var s = ts.ToString("yyyyMMddHHmmss");
        return positionIndexes.ToDictionary(i => i, i => $"{s}-{i}");
    }
}
