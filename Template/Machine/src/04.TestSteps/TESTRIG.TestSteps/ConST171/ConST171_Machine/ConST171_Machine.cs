using System.Globalization;
using TESTRIG.Core.Abstractions;
using TESTRIG.Core.Engine;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.TestSteps.ConST171.ConST171_Machine;

/// <summary>
/// ConST171 整机（设备族 ConST171，整机 ConST171A）测试**设备特有**处理器集合。
/// **人工填充**自旧 <c>SelfAutoTest.cs</c>（P27 整机脚本）：基础信息写入、版本验证、屏幕自测
/// （<see cref="IConST171Dut.ChangeScreenAsync"/> + 轮询结果）、风扇转速、正压/真空测试
/// （造压 + 泄漏判定 + <see cref="ProcessWaiter"/> 过程曲线）、吹扫、双模块校准。
/// 被检用 <see cref="IConST171Dut"/>（Xmas11 ConST171Base）。正压/真空泵（DPSEX）驱动
/// 待接入：造压由 ConST171 内部泵实现，外部标准模块比对在 TODO 中标注。
/// </summary>
internal sealed class ConST171Ops
{
    /// <summary>
    /// 测试项上下文。
    /// </summary>
    private readonly ITestContext _ctx;

    /// <summary>
    /// 被检 ConST171 专属驱动。
    /// </summary>
    public readonly IConST171Dut Dut;

    /// <summary>
    /// 从上下文解析被检驱动。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    public ConST171Ops(ITestContext ctx, CancellationToken ct)
    {
        _ctx = ctx;
        Dut = ctx.GetDevice<IConST171Dut>();
    }

    /// <summary>
    /// 按实例键取标准模块（Tool 设备，如 DPSEX1 正压 / DPSEX2 真空标准模块）。
    /// </summary>
    /// <param name="deviceKey">manifest ToolDevices 的 Key。</param>
    /// <returns>标准模块驱动。</returns>
    public IStandardModule Standard(string deviceKey)
        => _ctx.GetDevice<IStandardModule>(deviceKey);

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
    /// 按精确名取参数值（找不到用默认值）。
    /// </summary>
    public double Param(string name, double def)
        => double.TryParse(_ctx.Parameter(name)?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;

    /// <summary>
    /// 按片段匹配参数名取值（旧 JSON 参数名带单位描述，如"压力下限"）。
    /// </summary>
    public double ParamContains(string fragment, double def)
    {
        var p = _ctx.Step.Parameters.FirstOrDefault(p => p.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        return p is not null && double.TryParse(p.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
    }

    /// <summary>
    /// 按片段匹配条件（旧条件名如"转速低值范围"/"泄漏指标"/"精度等级"）。
    /// </summary>
    public ConditionDescriptor? Cond(string fragment)
        => _ctx.Step.Conditions.FirstOrDefault(c => c.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 区间判定并上报结果。
    /// </summary>
    /// <param name="fragment">条件名片段。</param>
    /// <param name="value">实测值。</param>
    /// <returns>是否通过（条件缺失视为通过并警告）。</returns>
    public bool InCond(string fragment, double value)
    {
        var c = Cond(fragment);
        if (c is null)
        {
            Report($"条件「{fragment}」缺失，跳过判定", RealtimeLevel.Warn);
            return true;
        }
        var ok = value >= c.Min && value <= c.Max;
        Report($"判定「{c.Name}」：{value:0.###}{c.Unit} ∈ [{c.Min}, {c.Max}]{c.Unit} → {(ok ? "通过" : "不合格")}",
            ok ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return ok;
    }

    /// <summary>
    /// 排压停泵复位（正压/真空测试结束后调用）。
    /// </summary>
    public async Task VentAndStop(string module)
    {
        await Dut.SetPressureVentAsync(true, default);
        await Dut.SetControlStateAsync(module, false, default);
        await Dut.SetPumpStatusAsync(module, false, default);
        await Dut.SetPressureVentAsync(false, default);
    }

    /// <summary>
    /// 屏幕自测（坏点/颜色、触摸、亮度、扬声器）：设备侧切屏自测 + 轮询结果。
    /// </summary>
    /// <param name="screen">屏幕项名（BadPointTest/TouchTest/LightTest/SpeakerTest）。</param>
    /// <param name="label">测试项名。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ScreenTest(string screen, string label, CancellationToken ct)
    {
        if (!await Dut.ChangeScreenAsync(screen, ct))
        {
            return StepResult.Fail($"{label}：启动设备自测失败", screen);
        }
        Report($"{label}：设备自测已启动，请观察屏幕…");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = "NotRunning";
        while (sw.Elapsed < TimeSpan.FromSeconds(60))
        {
            await Task.Delay(2000, ct);
            result = await Dut.GetScreenResultAsync(screen, ct);
            if (result == "Pass" || result == "Fail")
            {
                break;
            }
        }
        return result == "Pass"
            ? StepResult.Pass($"{label}通过", result)
            : sw.Elapsed >= TimeSpan.FromSeconds(60)
                ? StepResult.Fail($"{label}超时（60s）", result)
                : StepResult.Fail($"{label}不合格", result);
    }
}

/// <summary>
/// 基础信息写入：写 SN 与设备类型并回读核对。
/// </summary>
public sealed class SelfTestWriteSNAndTypeConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "SelfTestWriteSNAndType";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var sn = ctx.SerialNumber ?? "SN" + DateTime.Now.ToString("yyyyMMddHHmmss");
        if (!await op.Dut.SetSerialNumberAsync(sn, ct))
        {
            return StepResult.Fail("写入/回读序列号不一致", sn);
        }
        var type = ctx.Setting("DeviceType") ?? "ConST171A";
        if (!await op.Dut.SetDeviceTypeAsync(type, ct))
        {
            return StepResult.Fail("写入/回读设备类型不一致", type);
        }
        op.Report($"基础信息写入完成：SN={sn}，设备类型={type}", RealtimeLevel.Success);
        return StepResult.Pass($"SN={sn}，类型={type}", sn);
    }
}

/// <summary>
/// 版本验证：读控制板软件/硬件版本与 UI 版本并记录。
/// </summary>
public sealed class TestVersionsConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "TestVersions";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var sw = await op.Dut.GetControlSoftVersionAsync(ct);
        var hw = await op.Dut.GetControlHardVersionAsync(ct);
        var ui = await op.Dut.GetUiVersionAsync(ct);
        op.Report($"版本验证：控制板软件 v{sw} / 硬件 v{hw} / UI v{ui}", RealtimeLevel.Success);
        return StepResult.Pass($"软件 v{sw} / 硬件 v{hw} / UI v{ui}");
    }
}

/// <summary>
/// 设备参数设置：正压气源静音模式 + 真空气源开机排水模式。
/// </summary>
public sealed class SetDeviceParameterConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "SetDeviceParameter";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        if (!await op.Dut.SetPressureMuteAsync(true, ct))
        {
            return StepResult.Fail("正压气源静音模式设置失败");
        }
        if (!await op.Dut.SetPressureVacuumVentAsync(true, ct))
        {
            return StepResult.Fail("真空气源开机排水模式设置失败");
        }
        op.Report("设备参数设置完成（正压静音 + 真空开机排水）", RealtimeLevel.Success);
        return StepResult.Pass("设备参数设置完成");
    }
}

/// <summary>
/// 供电测试：旧体系为人工项（ManualTestItem），转换器已标 StepType=Manual——
/// 引擎弹人工确认框由操作员观察确认，本处理器不执行（保留占位）。
/// </summary>
public sealed class TestBatteryTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "TestBatteryTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项（Manual 步不进入）。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
        => Task.FromResult(StepResult.Pass("供电测试由人工确认（Manual 步）"));
}

/// <summary>
/// 屏幕颜色（坏点）测试：设备自测 + 操作员观察。
/// </summary>
public sealed class ScreenGeneralTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ScreenGeneralTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
        => new ConST171Ops(ctx, ct).ScreenTest("BadPointTest", "屏幕颜色", ct);
}

/// <summary>
/// 屏幕触摸测试。
/// </summary>
public sealed class ScreenTouchTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ScreenTouchTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
        => new ConST171Ops(ctx, ct).ScreenTest("TouchTest", "屏幕触摸", ct);
}

/// <summary>
/// 屏幕亮度测试。
/// </summary>
public sealed class ScreenLightTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ScreenLightTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
        => new ConST171Ops(ctx, ct).ScreenTest("LightTest", "屏幕亮度", ct);
}

/// <summary>
/// 扬声器测试。
/// </summary>
public sealed class ScreenSoundTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ScreenSoundTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
        => new ConST171Ops(ctx, ct).ScreenTest("SpeakerTest", "扬声器", ct);
}

/// <summary>
/// 风扇测试：低/中/高三档 PWM，逐档读转速按范围判定。
/// </summary>
public sealed class FanTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "FanTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var stages = new (double Pwm, string Cond)[]
        {
            (op.ParamContains("低值", 0.2), "低值范围"),
            (op.ParamContains("中值", 0.3), "中值范围"),
            (op.ParamContains("高值", 0.6), "高值范围"),
        };
        var results = new List<string>();
        foreach (var (pwm, cond) in stages)
        {
            if (!await op.Dut.SetFanSpeedAsync("Pressure", pwm, ct))
            {
                await op.Dut.SetFanSpeedAsync("Pressure", 0.1, ct);
                return StepResult.Fail("风扇转速设置失败", pwm.ToString(CultureInfo.InvariantCulture));
            }
            await Task.Delay(3000, ct);   // 转速稳定
            var rpm = await op.Dut.GetFanSpeedAsync("Pressure", ct);
            results.Add($"{pwm:0.0}→{rpm:0}rpm");
            if (!op.InCond(cond, rpm))
            {
                await op.Dut.SetFanSpeedAsync("Pressure", 0.1, ct);
                return StepResult.Fail($"风扇测试不合格（{pwm:0.0} PWM 档）", string.Join("，", results));
            }
        }
        await op.Dut.SetFanSpeedAsync("Pressure", 0.1, ct);
        op.Report($"风扇测试通过：{string.Join("，", results)}", RealtimeLevel.Success);
        return StepResult.Pass("风扇测试通过", string.Join("；", results));
    }
}

/// <summary>
/// 传感器测试：正压/负压传感器读数按范围判定。
/// </summary>
public sealed class SensorTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "SensorTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var pos = await op.Dut.GetPressureAsync("Pressure", ct);
        if (!op.InCond("正压", pos))
        {
            return StepResult.Fail("正压传感器超差", pos.ToString("0.###"));
        }
        var vac = await op.Dut.GetPressureAsync("Vacuum", ct);
        if (!op.InCond("负压", vac))
        {
            return StepResult.Fail("负压传感器超差", vac.ToString("0.###"));
        }
        op.Report($"传感器测试通过：正压 {pos:0.###}kPa，负压 {vac:0.###}kPa", RealtimeLevel.Success);
        return StepResult.Pass("传感器测试通过", $"正压 {pos:0.###}kPa / 负压 {vac:0.###}kPa");
    }
}

/// <summary>
/// 正压测试：设定 8000~8500kPa 造压，保压测泄漏（≤100kPa）。
/// </summary>
public sealed class PositiveTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PositiveTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var lo = op.ParamContains("下限", 8000);
        var hi = op.ParamContains("上限", 8500);
        var leakMax = op.Cond("泄漏指标")?.Max ?? 100;

        op.Report($"正压测试：造压 {lo}~{hi} kPa，泄漏指标 ≤{leakMax} kPa");
        if (!await op.Dut.SetPressureRangeAsync("Pressure", lo, hi, ct)
            || !await op.Dut.SetPumpStatusAsync("Pressure", true, ct)
            || !await op.Dut.SetControlStateAsync("Pressure", true, ct))
        {
            await op.VentAndStop("Pressure");
            return StepResult.Fail("正压造压启动失败");
        }
        var outcome = await ProcessWaiter.WaitUntilAsync(ctx, "正压压力", "kPa",
            () => op.Dut.GetPressureAsync("Pressure", ct),
            v => v >= lo && v <= hi,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), ct);
        if (outcome != ProcessWaiter.WaitOutcome.Satisfied)
        {
            await op.VentAndStop("Pressure");
            return StepResult.Fail("正压未达目标区间（60s 超时）");
        }

        // 保压 5s 测泄漏：两次读数差
        await Task.Delay(5000, ct);
        var p1 = await op.Dut.GetPressureAsync("Pressure", ct);
        await Task.Delay(5000, ct);
        var p2 = await op.Dut.GetPressureAsync("Pressure", ct);
        var leak = Math.Abs(p2 - p1);
        await op.VentAndStop("Pressure");

        op.Report($"正压保压 {p2:0.0} kPa，泄漏 {leak:0.00} kPa（≤{leakMax}）",
            leak <= leakMax ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return leak <= leakMax
            ? StepResult.Pass($"正压测试通过（{p2:0.0} kPa，泄漏 {leak:0.00}）", leak.ToString("0.00"))
            : StepResult.Fail($"正压泄漏超标（{leak:0.00} > {leakMax} kPa）", leak.ToString("0.00"));
    }
}

/// <summary>
/// 真空测试：设定 0~10 kPa 抽真空，保压测泄漏率（≤0.5 kPa）。
/// </summary>
public sealed class VacuumTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "VacuumTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var lo = op.ParamContains("下限", 0);
        var hi = op.ParamContains("上限", 10);
        var leakMax = op.Cond("泄漏率")?.Max ?? 0.5;

        op.Report($"真空测试：抽真空 {lo}~{hi} kPa，泄漏率 ≤{leakMax} kPa");
        if (!await op.Dut.SetPressureRangeAsync("Vacuum", lo, hi, ct)
            || !await op.Dut.SetPumpStatusAsync("Vacuum", true, ct)
            || !await op.Dut.SetControlStateAsync("Vacuum", true, ct))
        {
            await op.VentAndStop("Vacuum");
            return StepResult.Fail("真空抽气启动失败");
        }
        var outcome = await ProcessWaiter.WaitUntilAsync(ctx, "真空压力", "kPa",
            () => op.Dut.GetPressureAsync("Vacuum", ct),
            v => v >= lo && v <= hi,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), ct);
        if (outcome != ProcessWaiter.WaitOutcome.Satisfied)
        {
            await op.VentAndStop("Vacuum");
            return StepResult.Fail("真空未达目标区间（60s 超时）");
        }

        await Task.Delay(5000, ct);
        var p1 = await op.Dut.GetPressureAsync("Vacuum", ct);
        await Task.Delay(5000, ct);
        var p2 = await op.Dut.GetPressureAsync("Vacuum", ct);
        var leak = Math.Abs(p2 - p1);
        await op.VentAndStop("Vacuum");

        op.Report($"真空保压 {p2:0.00} kPa，泄漏率 {leak:0.00} kPa（≤{leakMax}）",
            leak <= leakMax ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return leak <= leakMax
            ? StepResult.Pass($"真空测试通过（{p2:0.00} kPa，泄漏率 {leak:0.00}）", leak.ToString("0.00"))
            : StepResult.Fail($"真空泄漏超标（{leak:0.00} > {leakMax} kPa）", leak.ToString("0.00"));
    }
}

/// <summary>
/// 吹扫测试：正压打压指定时长后检查压力 ≥ 合格下限。
/// </summary>
public sealed class BlowTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "BlowTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var seconds = (int)op.ParamContains("打压时长", 5);
        var okMin = op.Cond("合格下限")?.Min ?? 5;

        op.Report($"吹扫测试：打压 {seconds}s，合格下限 ≥{okMin} kPa");
        if (!await op.Dut.SetBlowTestAsync(true, ct)
            || !await op.Dut.SetControlStateAsync("Pressure", true, ct))
        {
            await op.VentAndStop("Pressure");
            return StepResult.Fail("吹扫打压启动失败");
        }
        await Task.Delay(seconds * 1000, ct);
        var p = await op.Dut.GetPressureAsync("Pressure", ct);
        await op.Dut.SetControlStateAsync("Pressure", false, ct);
        await op.Dut.SetBlowTestAsync(false, ct);
        await op.VentAndStop("Pressure");

        op.Report($"吹扫后压力 {p:0.0} kPa（≥{okMin}）", p >= okMin ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return p >= okMin
            ? StepResult.Pass($"吹扫测试通过（{p:0.0} kPa ≥ {okMin}）", p.ToString("0.0"))
            : StepResult.Fail($"吹扫压力不足（{p:0.0} < {okMin} kPa）", p.ToString("0.0"));
    }
}

/// <summary>
/// 真空模块校准：DPSEX2 真空标准模块作标准源，ConST171 造压至校准点后
/// 读标准压力比对设备校准回读，精度等级 ≤0.05%。
/// </summary>
public sealed class VacuumCalibrationTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "VacuumCalibrationTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var calPoint = op.ParamContains("下限", 5);
        var acc = op.Cond("精度等级")?.Max ?? 0.05;
        var std = op.Standard("DPSEX2");   // 真空标准模块

        await std.SetPressureTypeAsync("Vacuum", ct);
        if (!await op.Dut.StartCalibrationAsync("Vacuum", ct))
        {
            return StepResult.Fail("进入真空校准模式失败");
        }

        // 真空造压至校准点（±0.5 kPa）
        await op.Dut.SetPressureRangeAsync("Vacuum", Math.Min(0, calPoint), calPoint + 1, ct);
        await op.Dut.SetPumpStatusAsync("Vacuum", true, ct);
        await op.Dut.SetControlStateAsync("Vacuum", true, ct);
        var outcome = await ProcessWaiter.WaitUntilAsync(ctx, "真空压力", "kPa",
            () => op.Dut.GetPressureAsync("Vacuum", ct),
            v => Math.Abs(v - calPoint) <= 0.5,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), ct);
        if (outcome != ProcessWaiter.WaitOutcome.Satisfied)
        {
            await op.VentAndStop("Vacuum");
            await op.Dut.StopCalibrationAsync("Vacuum", ct);
            return StepResult.Fail($"真空未达校准点 {calPoint} kPa（60s 超时）");
        }

        // 标准模块读数作为标准值，写入设备校准并回读比对
        var stdVal = await std.GetPressureKpaAsync(ct);
        await op.Dut.SetCalibrationValueAsync("Vacuum", stdVal, ct);
        var read = await op.Dut.GetCalPressureAsync("Vacuum", ct);
        await op.Dut.StopCalibrationAsync("Vacuum", ct);
        await op.VentAndStop("Vacuum");

        var err = Math.Abs(read - stdVal) / Math.Max(Math.Abs(stdVal), 1) * 100;
        op.Report($"真空校准：标准 {stdVal:0.000} kPa（DPSEX2），设备回读 {read:0.000} kPa，误差 {err:0.000}%（≤{acc}%）",
            err <= acc ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return err <= acc
            ? StepResult.Pass($"真空校准完成（误差 {err:0.000}% ≤{acc}%）", err.ToString("0.000"))
            : StepResult.Fail($"真空校准超差（{err:0.000}% > {acc}%）", err.ToString("0.000"));
    }
}

/// <summary>
/// 正压模块校准：DPSEX1 正压标准模块作标准源，ConST171 逐点造压（0 / 上限）
/// 读标准压力比对设备校准回读，精度等级 ≤0.1%。
/// </summary>
public sealed class PositiveCalibrationTestConST171Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PositiveCalibrationTest";
    /// <summary>限定设备家族（仅 ConST171 整机使用）。</summary>
    public string? DeviceFamily => "ConST171";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new ConST171Ops(ctx, ct);
        var lo = op.ParamContains("下限", 0);
        var hi = op.ParamContains("上限", 8500);
        var acc = op.Cond("精度等级")?.Max ?? 0.1;
        var std = op.Standard("DPSEX1");   // 正压标准模块

        await std.SetPressureTypeAsync("Pressure", ct);
        if (!await op.Dut.StartCalibrationAsync("Pressure", ct))
        {
            return StepResult.Fail("进入正压校准模式失败");
        }

        var maxErr = 0d;
        foreach (var pt in new[] { lo, hi })
        {
            // 正压造压至校准点（±50 kPa）
            await op.Dut.SetPressureRangeAsync("Pressure", Math.Min(0, pt), pt + 100, ct);
            await op.Dut.SetPumpStatusAsync("Pressure", true, ct);
            await op.Dut.SetControlStateAsync("Pressure", true, ct);
            var outcome = await ProcessWaiter.WaitUntilAsync(ctx, "正压压力", "kPa",
                () => op.Dut.GetPressureAsync("Pressure", ct),
                v => Math.Abs(v - pt) <= 50,
                TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), ct);
            if (outcome != ProcessWaiter.WaitOutcome.Satisfied)
            {
                await op.VentAndStop("Pressure");
                await op.Dut.StopCalibrationAsync("Pressure", ct);
                return StepResult.Fail($"正压未达校准点 {pt} kPa（60s 超时）");
            }

            // 标准模块读数作为标准值，写入设备校准并回读比对
            var stdVal = await std.GetPressureKpaAsync(ct);
            await op.Dut.SetCalibrationValueAsync("Pressure", stdVal, ct);
            var read = await op.Dut.GetCalPressureAsync("Pressure", ct);
            var err = Math.Abs(read - stdVal) / Math.Max(Math.Abs(stdVal), 1) * 100;
            maxErr = Math.Max(maxErr, err);
            op.Report($"正压校准点 {pt:0} kPa：标准 {stdVal:0.000}（DPSEX1），设备回读 {read:0.000}，误差 {err:0.000}%");
        }
        await op.Dut.StopCalibrationAsync("Pressure", ct);
        await op.VentAndStop("Pressure");

        op.Report($"正压校准：最大误差 {maxErr:0.000}%（≤{acc}%）",
            maxErr <= acc ? RealtimeLevel.Success : RealtimeLevel.Warn);
        return maxErr <= acc
            ? StepResult.Pass($"正压校准完成（最大误差 {maxErr:0.000}% ≤{acc}%）", maxErr.ToString("0.000"))
            : StepResult.Fail($"正压校准超差（{maxErr:0.000}% > {acc}%）", maxErr.ToString("0.000"));
    }
}
