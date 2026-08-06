using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Core.Engine;

namespace TESTRIG.Automation;

/// <summary>
/// 整机测试编排器：协调 <see cref="TestRunner"/>（跑整机流程）并维护通过/失败/重试/平均时长计数。
/// 整机模板无 PLC 自动化（上下料/工位/压合由操作员人工完成），故只保留手动单次运行；
/// 全自动循环（RunFullAutoAsync）与 A/B/C 工位逻辑为动态模板专属，不在本模板。
/// </summary>
public sealed class AutomationOrchestrator
{
    /// <summary>
    /// 测试流程引擎。
    /// </summary>
    private readonly TestRunner _runner;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<AutomationOrchestrator> _logger;

    /// <summary>
    /// 构造整机测试编排器。
    /// </summary>
    /// <param name="runner">测试流程引擎。</param>
    /// <param name="logger">日志。</param>
    public AutomationOrchestrator(TestRunner runner, ILogger<AutomationOrchestrator> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    /// <summary>
    /// 手动单次：跑整机流程 → 记录计数。不接 PLC、不循环，操作员人工上下料。
    /// </summary>
    /// <param name="manifest">整机清单。</param>
    /// <param name="counters">产线计数器。</param>
    /// <param name="options">运行选项（null=默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>本次测试会话结果（供 UI 落库/提示）。</returns>
    public async Task<TestSessionResult> RunManualAsync(JigManifest manifest, AutomationCounters counters, RunOptions? options = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await _runner.RunAsync(manifest, options, ct);
        sw.Stop();
        counters.Record(result.Passed, sw.Elapsed.TotalSeconds);
        _logger.LogInformation("整机手动测试完成：{Board} 通过={Passed} 耗时={Sec:0.0}s",
            manifest.Key, result.Passed, sw.Elapsed.TotalSeconds);
        return result;
    }
}
