using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.TestSteps.TemplateUUT.TemplateUUT_Inspect;

/// <summary>
/// TemplateUUT 出厂检验（设备族 TemplateUUT）测试**设备特有**处理器集合（内置占位示例）：
/// 出厂流程 = 检验准备 → 外观检查 → 功能复检 → 证书生成（独立「证书/合格证」窗口，见 Inspect 模板 UI）。
/// 真实出厂产品接入时，出厂专属处理器放 <c>TestSteps/&lt;设备族&gt;/</c> 下、通用处理器放 <c>Shared/</c>。
/// </summary>
internal sealed class TemplateUUTInspectOps
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
    public TemplateUUTInspectOps(ITestContext ctx, CancellationToken ct)
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
}

/// <summary>
/// 出厂检验准备（内置占位）：接线检查、共享标准盒连接。
/// </summary>
public sealed class InspectPrepTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "InspectPrep";
    /// <summary>限定设备家族（仅 TemplateUUT 的出厂使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTInspectOps(ctx, ct);
        op.Report("出厂检验准备：接线检查、共享标准盒连接");
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
        return StepResult.Pass("出厂检验准备完成");
    }
}

/// <summary>
/// 出厂检验项（内置占位）：外观/附件/功能复检，Settings 列出检查点。
/// </summary>
public sealed class InspectCheckTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "InspectCheck";
    /// <summary>限定设备家族（仅 TemplateUUT 的出厂使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new TemplateUUTInspectOps(ctx, ct);
        var checks = ctx.Step.Settings.Keys.ToList();
        op.Report(checks.Count == 0
            ? "检验项为空（manifest Settings 可配置检查点）"
            : $"检验 {checks.Count} 项：{string.Join("、", checks)}");
        return Task.FromResult(StepResult.Pass("出厂检验项通过"));
    }
}

/// <summary>
/// 出厂检验结束（内置占位）：断电复位，提示生成证书。
/// </summary>
public sealed class InspectFinishTemplateUUTHandler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "InspectFinish";
    /// <summary>限定设备家族（仅 TemplateUUT 的出厂使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        ctx.Report("出厂检验结束：断电复位。合格产品可在「证书 / 合格证」窗口生成出厂证书", RealtimeLevel.Success);
        return Task.FromResult(StepResult.Pass("出厂检验完成"));
    }
}
