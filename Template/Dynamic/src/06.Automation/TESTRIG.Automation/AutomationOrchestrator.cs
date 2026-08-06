using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Core.Engine;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Automation;

/// <summary>
/// 产线自动化编排器：协调 <see cref="TestRunner"/>（跑治具）+ <see cref="IPlcController"/>（上下料/压合/分拣）。
/// 不改引擎；UI 仍直接订阅 TestRunner 的进度/日志事件。取代旧 AutoMultiTestTaskRunViewModel 的自动循环。
/// </summary>
public sealed class AutomationOrchestrator
{
    /// <summary>
    /// 测试流程引擎。
    /// </summary>
    private readonly TestRunner _runner;

    /// <summary>
    /// 自动化 PLC 控制器。
    /// </summary>
    private readonly IPlcController _plc;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<AutomationOrchestrator> _logger;

    /// <summary>
    /// 构造自动化编排器。
    /// </summary>
    /// <param name="runner">测试流程引擎。</param>
    /// <param name="plc">PLC 控制器。</param>
    /// <param name="logger">日志。</param>
    public AutomationOrchestrator(TestRunner runner, IPlcController plc, ILogger<AutomationOrchestrator> logger)
    {
        _runner = runner;
        _plc = plc;
        _logger = logger;
    }

    /// <summary>
    /// 全自动循环（一个拼版=整体一次上/下料）：等就绪(即已压合)→（生成新 SN/刷新界面）→号位并行测试→
    /// 不过则重新压合+等就绪重测一次→回送 OK/NG(PLC 自动打开针床下料)→循环。
    /// 压合/打开非独立指令：就绪即已压合，回送结果即自动打开下料。
    /// <paramref name="onSchedule"/> 上报 PLC 调度动作（状态栏日志）；
    /// <paramref name="onCycleStart"/> 在新一块板压合后给出各号位新 SN（UI 重置界面+显示新 SN）；
    /// <paramref name="onResult"/> 每次测试结果落库。由 <see cref="IPlcController.IsWatching"/> 与取消令牌控制启停。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="counters">产线计数器。</param>
    /// <param name="options">运行选项（null=默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="onResult">每次测试结果回调（落库）。</param>
    /// <param name="onCycleStart">新一块板压合后回调（各号位新 SN）。</param>
    /// <param name="onSchedule">PLC 调度动作回调（状态栏日志）。</param>
    /// <param name="confirmCleanupOnStop">
    /// 测试中途被停止、且当前工位仍有已压合板时的清料确认回调（UI 弹窗）。返回 true=清理（按 NG 下料），false=保持压合待手动取出。
    /// 停止发生在「等待就绪」阶段（工位无板）时不会触发。
    /// </param>
    public async Task RunFullAutoAsync(JigManifest manifest, AutomationCounters counters, RunOptions? options = null,
        CancellationToken ct = default, Func<TestSessionResult, Task>? onResult = null,
        Action<IReadOnlyDictionary<int, string>>? onCycleStart = null, Action<string>? onSchedule = null,
        Func<TestSessionResult, Task<bool>>? confirmCleanupOnStop = null)
    {
        options ??= new RunOptions();
        if (!_plc.IsConnected)
        {
            await _plc.ConnectAsync(ct);
        }

        _plc.IsWatching = true;
        _logger.LogInformation("全自动测试开始：{Board}", manifest.Key);

        // 参与的号位（用于每循环生成新 SN）
        var positionIndexes = (options.Positions ?? manifest.Positions.Select(p => p.Index).ToList()).ToList();

        while (!ct.IsCancellationRequested && _plc.IsWatching)
        {
            int station;
            try
            {
                onSchedule?.Invoke("等待拼版就绪（上料压合）…");
                station = await _plc.WaitForStationReadyAsync(ct);
            }
            catch (OperationCanceledException) { break; }

            var st = (char)('A' + station);
            var sw = Stopwatch.StartNew();

            // 就绪即已压合。每块板（每循环）生成新 SN，并通知 UI 重置界面 + 显示新 SN（清掉上一块板的过程信息/曲线）
            var sns = SerialNumberFactory.Generate(positionIndexes);
            onCycleStart?.Invoke(sns);
            var cycleOptions = options with { SerialNumbers = sns };

            onSchedule?.Invoke($"工位{st} 测试中…");
            // 引擎对「停止」优雅返回已跑数据（不抛），故停止时 result 为不完整半测数据
            var result = await _runner.RunAsync(manifest, cycleOptions, ct);
            if (onResult is not null)
            {
                await onResult(result);
            }

            // 测试进行中被停止：不重测、不按 result.Passed 回送（半测板 Passed 可能为真）；
            // 询问是否清料，据「不完整=NG」下料，然后退出循环
            if (ct.IsCancellationRequested)
            {
                await HandleStopWithBoardAsync(st, result, confirmCleanupOnStop);
                break;
            }

            if (!result.Passed)
            {
                // 重测一次：发重新压合指令→再等就绪，仅重跑未通过的号位（沿用本循环 SN 与元数据）
                var failed = result.Positions.Where(p => !p.Passed).Select(p => p.Position.Index).ToList();
                onSchedule?.Invoke($"工位{st} 存在不合格，重新压合→重测");
                try
                {
                    await _plc.RePressAsync(ct);
                    await _plc.WaitForStationReadyAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // 重压/等再次就绪期间被停止：板仍在工位，询问清料
                    await HandleStopWithBoardAsync(st, result, confirmCleanupOnStop);
                    break;
                }

                // 标记为重压重测：主表对应 SN 记「已重压」
                result = (await _runner.RunAsync(manifest, cycleOptions with { Positions = failed }, ct)) with { IsRePress = true };
                if (onResult is not null)
                {
                    await onResult(result);
                }

                counters.AddRetry();

                if (ct.IsCancellationRequested)
                {
                    await HandleStopWithBoardAsync(st, result, confirmCleanupOnStop);
                    break;
                }
            }

            sw.Stop();
            onSchedule?.Invoke($"工位{st} 测试完成：{(result.Passed ? "OK ✓" : "NG ✗")}，回送结果（PLC 自动打开针床下料）");
            await _plc.SendResultAsync(result.Passed ? UnloadResult.Ok : UnloadResult.Ng, ct);
            counters.Record(result.Passed, sw.Elapsed.TotalSeconds);
        }

        onSchedule?.Invoke("全自动已停止");
        _logger.LogInformation("全自动测试停止：通过 {P} 失败 {F} 重试 {R}", counters.Passed, counters.Failed, counters.Retried);
    }

    /// <summary>
    /// 测试中途停止且工位仍有已压合板时的清料处理：中途停止即测试不完整，一律按 NG 处理。
    /// 通过回调让 UI 弹窗询问是否清理物料——确认则回送 NG 触发 PLC 松开针床下料；取消则板保持压合，待人工取出。
    /// </summary>
    /// <param name="station">工位标签（A/B/C）。</param>
    /// <param name="result">停止时的半测结果（仅供 UI 展示，判定固定为 NG）。</param>
    /// <param name="confirmCleanupOnStop">清料确认回调（null=无回调，板保持压合）。</param>
    private async Task HandleStopWithBoardAsync(char station, TestSessionResult result,
        Func<TestSessionResult, Task<bool>>? confirmCleanupOnStop)
    {
        if (confirmCleanupOnStop is null)
        {
            _logger.LogInformation("工位{Station} 测试中止且仍有压合板，但未提供清料回调，板保持压合", station);
            return;
        }

        var doCleanup = await confirmCleanupOnStop(result);
        if (doCleanup)
        {
            // 停止令牌已取消，清料握手必须照发，故用 None
            _logger.LogInformation("工位{Station} 操作员确认清料，测试不完整按 NG 下料", station);
            await _plc.SendResultAsync(UnloadResult.Ng, CancellationToken.None);
        }
        else
        {
            _logger.LogInformation("工位{Station} 操作员取消清料，板保持压合，需人工取出", station);
        }
    }

    /// <summary>
    /// 手动单次：板已就绪压合 → 跑治具 → 回送结果(PLC 自动打开针床下料)。不循环。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="counters">产线计数器。</param>
    /// <param name="options">运行选项（null=默认）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RunManualAsync(JigManifest manifest, AutomationCounters counters, RunOptions? options = null, CancellationToken ct = default)
    {
        if (!_plc.IsConnected)
        {
            await _plc.ConnectAsync(ct);
        }

        var sw = Stopwatch.StartNew();
        var result = await _runner.RunAsync(manifest, options, ct);
        sw.Stop();
        await _plc.SendResultAsync(result.Passed ? UnloadResult.Ok : UnloadResult.Ng, ct);
        counters.Record(result.Passed, sw.Elapsed.TotalSeconds);
    }
}
