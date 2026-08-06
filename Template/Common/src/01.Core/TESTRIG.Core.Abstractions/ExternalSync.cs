namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 外部上报适配器契约：把本地已落库的测试会话结果同步到远程（正式环境 MySQL；后续可扩展 MES/U8/AEM）。
/// 实现应保证远程失败不影响本地流程（由调用方吞异常并记日志）。
/// </summary>
public interface IExternalSync
{
    /// <summary>
    /// 是否启用远程上报（未配置连接串时为 false，调用方可跳过）。
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// 上报一次测试会话结果到远程。
    /// </summary>
    /// <param name="result">测试会话结果（与本地落库同一份数据）。</param>
    /// <param name="ct">取消令牌。</param>
    Task PushAsync(TestSessionResult result, CancellationToken ct = default);
}

/// <summary>
/// 空实现：远程上报未配置时使用，不做任何事。
/// </summary>
public sealed class NullExternalSync : IExternalSync
{
    /// <summary>
    /// 单例。
    /// </summary>
    public static readonly NullExternalSync Instance = new();

    /// <inheritdoc/>
    public bool Enabled => false;

    /// <inheritdoc/>
    public Task PushAsync(TestSessionResult result, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
