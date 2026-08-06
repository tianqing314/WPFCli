using System.Globalization;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.TestSteps.TemplateUUT.TemplateUUT_Complete;

/// <summary>
/// TemplateUUT 组件（设备族 TemplateUUT）测试**设备特有**处理器集合（内置占位示例）：
/// 组件测试流程 = 组件准备 → 装配检查 → 功能检查 → 工装绑定记录 → 结束。
/// 组件模板专属 UI 是「工装/治具管理」（ToolingWindow），测试项内可用 <c>ToolingBind</c> 绑定本次使用的工装。
/// 真实组件产品接入时，组件专属处理器放 <c>TestSteps/&lt;设备族&gt;/</c> 下、通用处理器放 <c>Shared/</c>。
/// </summary>
internal sealed class TemplateUUTCompleteOps
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
    public TemplateUUTCompleteOps(ITestContext ctx, CancellationToken ct)
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
/// 组件准备（内置占位）：接线检查、共享标准盒连接并上电就绪。
/// </summary>
public sealed class CompletePrepTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "CompletePrep";
    /// <summary>限定设备家族（仅 TemplateUUT 的组件使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTCompleteOps(ctx, ct);
        op.Report("组件准备：接线检查、共享标准盒连接");
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
        return StepResult.Pass("组件准备完成");
    }
}

/// <summary>
/// 装配检查（内置占位）：核对组件装配（螺丝/线缆/标签），读取 Settings 期望项逐一确认。
/// </summary>
public sealed class CompleteCheckTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "CompleteCheck";
    /// <summary>限定设备家族（仅 TemplateUUT 的组件使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTCompleteOps(ctx, ct);
        var checks = ctx.Step.Settings.Keys.ToList();
        op.Report(checks.Count == 0
            ? "装配检查项为空（manifest Settings 可配置检查点）"
            : $"装配检查 {checks.Count} 项：{string.Join("、", checks)}");
        return Task.FromResult(StepResult.Pass("装配检查通过"));
    }
}

/// <summary>
/// 工装绑定（内置占位）：把本次使用的工装/治具 SN 记录进测试项（结合 ToolingWindow 维护的工装台账）。
/// </summary>
public sealed class ToolingBindTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ToolingBind";
    /// <summary>限定设备家族（仅 TemplateUUT 的组件使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTCompleteOps(ctx, ct);
        var toolingSn = ctx.Setting("ToolingSn") ?? "未指定";
        op.Report($"本次使用工装 SN：{toolingSn}");
        return Task.FromResult(StepResult.Pass($"工装绑定完成（{toolingSn}）", toolingSn));
    }
}

/// <summary>
/// 组件结束（内置占位）：断电复位。
/// </summary>
public sealed class CompleteFinishTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "CompleteFinish";
    /// <summary>限定设备家族（仅 TemplateUUT 的组件使用）。</summary>
    public string? DeviceFamily => "TemplateUUT";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        ctx.Report("组件测试结束：断电复位", RealtimeLevel.Success);
        return Task.FromResult(StepResult.Pass("组件测试完成"));
    }
}
