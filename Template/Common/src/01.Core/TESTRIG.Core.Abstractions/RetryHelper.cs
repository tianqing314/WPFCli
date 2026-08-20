namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 重试工具（封装旧脚本 <c>goto tryagain</c> + <c>trynum</c> 计数器 + <c>OpenInfoConfirmWindow("重试？")</c> 模式）。
/// 不直接转 do-while / 保留 goto，而是以 RetryHelper.RetryAsync 集中维护重试次数与询问逻辑。
/// 典型翻译：<c>goto tryagain;</c> + <c>OpenInfoConfirmWindow("重试？")</c> →
/// <c>await RetryHelper.RetryAsync(attempt => action(attempt), () => ctx.ConfirmAsync("重试？"), maxAttempts)</c>。
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// 重试异步操作直到成功或用尽次数。
    /// </summary>
    /// <param name="action">动作（参数为第几次尝试，从 1 起）；返回 true=成功停止，false=失败重试。</param>
    /// <param name="shouldRetry">失败后是否重试的询问（参数为已失败次数；返回 true=重试，false=停止）。
    /// null = 无条件重试到用尽次数（对应旧脚本无用户确认的 <c>goto tryagain</c>）。</param>
    /// <param name="maxAttempts">最大尝试次数（含首次）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=某次成功；false=用尽次数或用户取消重试。</returns>
    public static async Task<bool> RetryAsync(
        Func<int, Task<bool>> action,
        Func<int, Task<bool>>? shouldRetry = null,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (await action(attempt))
            {
                return true;
            }

            // 用尽次数：停止
            if (attempt >= maxAttempts)
            {
                return false;
            }

            // 询问是否重试（shouldRetry=null=无条件重试；用户取消=停止）
            if (shouldRetry is not null && !await shouldRetry(attempt))
            {
                return false;
            }
        }

        return false;
    }
}
