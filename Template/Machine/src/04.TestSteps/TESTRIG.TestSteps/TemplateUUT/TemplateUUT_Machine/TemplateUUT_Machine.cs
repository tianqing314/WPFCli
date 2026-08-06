using System.Globalization;
using TESTRIG.Core.Abstractions;
using TESTRIG.Core.Engine;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.TestSteps.TemplateUUT.TemplateUUT_Machine;

/// <summary>
/// TemplateUUT 整机（设备族 TemplateUUT）测试**设备特有**处理器集合（内置占位示例）：
/// 整机流程 = 人工确认步（LCD/扬声器，<c>StepType=Manual</c>，由引擎弹确认框，无处理器）+ 温控过程步
/// （<c>StepType=Process</c>，<see cref="OvenProcessHandler"/> 用 <see cref="ProcessWaiter"/> 轮询温度并实时上报曲线）。
/// 真实整机产品（如 ConST171A）接入时，整机专属处理器放 <c>TestSteps/&lt;设备族&gt;/</c> 下、通用处理器放 <c>Shared/</c>。
/// </summary>
internal sealed class TemplateUUTMachineOps
{
    /// <summary>
    /// 测试项上下文。
    /// </summary>
    private readonly ITestContext _ctx;

    /// <summary>
    /// 取消令牌。
    /// </summary>
    private readonly CancellationToken _ct;

    /// <summary>
    /// 从上下文构建操作助手。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    public TemplateUUTMachineOps(ITestContext ctx, CancellationToken ct)
    {
        _ctx = ctx;
        _ct = ct;
    }

    /// <summary>
    /// 推送实时消息。
    /// </summary>
    /// <param name="m">消息。</param>
    /// <param name="l">级别。</param>
    public void Report(string m, RealtimeLevel l = RealtimeLevel.Info)
    {
        _ctx.Report(m, l);
    }

    /// <summary>
    /// 取消令牌。
    /// </summary>
    public CancellationToken Ct => _ct;
}

/// <summary>
/// 整机准备（内置占位）：连接共享标准盒并上电就绪。真实产品在此做接线/上电/通讯准备。
/// </summary>
public sealed class MachinePrepTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "MachinePrep";
    /// <summary>限定设备家族（仅 TemplateUUT 的整机使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTMachineOps(ctx, ct);
        op.Report("整机准备：接线检查、共享标准盒连接");
        try
        {
            var box = ctx.GetDevice<IDynamicStandardBox>();
            await box.ConnectAsync(ct);
            op.Report($"标准盒已连接：{box.IsConnected}");
        }
        catch (Exception ex)
        {
            op.Report($"标准盒连接异常（仿真可忽略）：{ex.Message}", RealtimeLevel.Warn);
        }
        return StepResult.Pass("整机准备完成");
    }
}

/// <summary>
/// 温控过程项（内置占位）：模拟高温炉升温——用 <see cref="ProcessWaiter"/> 轮询炉温并实时上报曲线，
/// 直到温度进入「目标 ± 波动度」区间或超时（<c>StepType=Process</c>，超时来自 manifest 的 TimeoutMs）。
/// 真实产品接入时改为读真实炉温传感器（可复用 Xmas11 通讯库）。
/// </summary>
public sealed class OvenProcessTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "OvenProcess";
    /// <summary>限定设备家族（仅 TemplateUUT 的整机使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTMachineOps(ctx, ct);

        // 目标温度与波动度（默认 660℃ ±5，可在 manifest Settings 覆盖）
        var target = double.TryParse(ctx.Setting("TargetTemp"), NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 660;
        var tol = double.TryParse(ctx.Setting("Tolerance"), NumberStyles.Any, CultureInfo.InvariantCulture, out var o) ? o : 5;
        var pollMs = int.TryParse(ctx.Setting("PollMs"), out var p) ? p : 1000;
        var timeout = TimeSpan.FromMilliseconds(ctx.Step.TimeoutMs > 0 ? ctx.Step.TimeoutMs : 120_000);

        op.Report($"高温炉设定目标温度 {target}℃（波动度 ±{tol}℃），开始升温…");

        // 模拟炉温：从室温指数逼近目标（真机改为读传感器）
        double temp = 25;
        var simulated = new Random(Environment.TickCount);
        var outcome = await ProcessWaiter.WaitUntilAsync(
            ctx,
            "炉内温度", "℃",
            () =>
            {
                temp += (target - temp) * 0.05 + (simulated.NextDouble() - 0.5) * 4;
                return temp;
            },
            v => Math.Abs(v - target) <= tol,
            timeout,
            TimeSpan.FromMilliseconds(pollMs),
            ct);

        return outcome switch
        {
            ProcessWaiter.WaitOutcome.Satisfied => StepResult.Pass($"炉温已达目标 {target}℃±{tol}", temp.ToString("0.#")),
            ProcessWaiter.WaitOutcome.TimedOut => StepResult.Fail($"炉温未达目标区间（{timeout.TotalSeconds:0}s 超时，当前 {temp:0.#}℃）", temp.ToString("0.#")),
            _ => StepResult.Skip("测试已停止"),
        };
    }
}

/// <summary>
/// 整机结束（内置占位）：断电、复位、汇总提示。
/// </summary>
public sealed class MachineFinishTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "MachineFinish";
    /// <summary>限定设备家族（仅 TemplateUUT 的整机使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        ctx.Report("整机测试结束：断电复位", RealtimeLevel.Success);
        return Task.FromResult(StepResult.Pass("整机测试完成"));
    }
}
