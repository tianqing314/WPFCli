using System.Diagnostics;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Core.Engine;

/// <summary>
/// 过程等待原语（整机温控等分钟级动态过程）：轮询读取过程值并实时上报曲线，
/// 直到条件满足或超时。供 <c>StepType=Process</c> 的处理器使用（如高温炉升温/稳定判定）。
/// </summary>
public static class ProcessWaiter
{
    /// <summary>
    /// 过程等待结果。
    /// </summary>
    public enum WaitOutcome
    {
        /// <summary>
        /// 条件已满足。
        /// </summary>
        Satisfied,

        /// <summary>
        /// 超时未满足。
        /// </summary>
        TimedOut,

        /// <summary>
        /// 被取消（用户停止）。
        /// </summary>
        Cancelled,
    }

    /// <summary>
    /// 轮询等待：以 <paramref name="pollInterval"/> 间隔读取过程值并上报采样点（实时曲线），
    /// 直到 <paramref name="isSatisfied"/> 返回 true 或超过 <paramref name="timeout"/>。
    /// </summary>
    /// <param name="ctx">测试项上下文（用于采样上报与消息）。</param>
    /// <param name="valueName">过程量名称（如"炉内温度"）。</param>
    /// <param name="unit">单位（如 ℃）。</param>
    /// <param name="readValue">读取当前过程值。</param>
    /// <param name="isSatisfied">条件判定（入参=当前值）。</param>
    /// <param name="timeout">最长等待时间。</param>
    /// <param name="pollInterval">轮询间隔。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>等待结果。</returns>
    public static async Task<WaitOutcome> WaitUntilAsync(
        ITestContext ctx,
        string valueName,
        string unit,
        Func<double> readValue,
        Func<double, bool> isSatisfied,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        ctx.BeginSampling(unit, valueName);
        var sw = Stopwatch.StartNew();
        var t = 0.0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var v = readValue();
            ctx.ReportSample(t, v);

            if (isSatisfied(v))
            {
                return WaitOutcome.Satisfied;
            }

            if (sw.Elapsed >= timeout)
            {
                return WaitOutcome.TimedOut;
            }

            await Task.Delay(pollInterval, ct);
            t = sw.Elapsed.TotalSeconds;
        }
    }
}
