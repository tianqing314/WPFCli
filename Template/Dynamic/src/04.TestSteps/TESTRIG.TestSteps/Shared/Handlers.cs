using System.Globalization;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.TestSteps;

/// <summary>
/// 处理器公用小工具：从设置读整数、数值格式化。
/// </summary>
internal static class Num
{
    /// <summary>
    /// 从 Settings 读整数，缺失/非法回退默认值。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="key">设置键。</param>
    /// <param name="fallback">默认值。</param>
    /// <returns>整数值。</returns>
    public static int Int(ITestContext ctx, string key, int fallback)
    {
        return int.TryParse(ctx.Setting(key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    /// <summary>
    /// 数值格式化（保留三位有效小数，不变文化）。
    /// </summary>
    /// <param name="v">数值。</param>
    /// <returns>格式串。</returns>
    public static string Fmt(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// 工装准备：连接标准盒与被检板，按真值表切换供电档位、上电。PORT: Entry=PreparationAP。
/// </summary>
public sealed class PreparationHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "Preparation";

    /// <summary>
    /// 连标准盒、切档上电、连被检。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var box = ctx.GetDevice<IStandardBox>();
        var dut = ctx.GetDevice<IDutDevice>();
        await box.ConnectAsync(ct);
        ctx.Report("标准盒已连接");
        await box.SwitchGearAsync(ctx.Setting("Relay") ?? "A", Num.Int(ctx, "Gear", 4), ct);
        await box.PowerOnAsync(Num.Int(ctx, "PowerChannel", ctx.Position.Index), ct);
        await dut.ConnectAsync(ct);
        ctx.Report($"被检板已上电并连接（{ctx.Position.Name}）", RealtimeLevel.Success);
        return StepResult.Pass("工装准备完成");
    }
}

/// <summary>
/// 初始信息写入：根据编号写板卡类型与初始量程。PORT: Entry=TestWriteType。
/// </summary>
public sealed class WriteInitInfoHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "WriteInitInfo";

    /// <summary>
    /// 写板卡类型；SN 为空则回读。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var dut = ctx.GetDevice<IDutDevice>();
        var boardType = ctx.Parameter("BoardType")?.Value ?? ctx.Setting("BoardType") ?? "Default";
        await dut.WriteInitInfoAsync(boardType, ct);
        if (string.IsNullOrEmpty(ctx.SerialNumber))
        {
            ctx.SerialNumber = await dut.ReadSerialNumberAsync(ct);
        }

        ctx.Report($"已写入 {boardType}，SN={ctx.SerialNumber}");
        return StepResult.Pass("初始信息写入完成", ctx.SerialNumber);
    }
}

/// <summary>
/// 版本验证：读固件版本并按 Text 条件核对。PORT: Entry=TestSoftVersionsDT。
/// </summary>
public sealed class FirmwareVersionHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "FirmwareVersion";

    /// <summary>
    /// 读版本并逐条 Text 条件核对。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var dut = ctx.GetDevice<IDutDevice>();
        var version = await dut.ReadFirmwareVersionAsync(ct);
        ctx.Report($"读取版本 {version}");
        foreach (var cond in ctx.Conditions)
        {
            var r = ctx.Evaluator.Evaluate(cond, version);
            if (!r.Passed)
            {
                return StepResult.Fail(r.Message, version);
            }
        }
        return StepResult.Pass($"版本核对通过：{version}", version);
    }
}

/// <summary>
/// 通用功能检查：报一次动作即视为通过；若配 Settings["MeasurePoint"] + 条件则做回读判定。
/// 用于实体按键/RTC/蓝牙/温度芯片/外供电检测等数字功能项。
/// PORT: Entry=TestDynamicKEY/TestRTCHost/TestBLEHost/TestValveTerminalHost/TestOutVoltage。
/// </summary>
public sealed class FunctionCheckHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "FunctionCheck";

    /// <summary>
    /// 无测量点时报动作通过；有测量点+条件时回读判定。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var point = ctx.Setting("MeasurePoint");
        if (point is null || ctx.Conditions.Count == 0)
        {
            await Task.Delay(40, ct);
            ctx.Report("功能动作完成");
            return StepResult.Pass($"{ctx.Step.Name} 通过");
        }

        var dut = ctx.GetDevice<IDutDevice>();
        var value = await dut.MeasureAsync(point, ct);
        var r = ctx.Evaluator.EvaluateAll(ctx.Conditions, value);
        ctx.Report($"回读 {point}={Num.Fmt(value)}：{r.Message}");
        return r.Passed ? StepResult.Pass(r.Message, Num.Fmt(value)) : StepResult.Fail(r.Message, Num.Fmt(value));
    }
}

/// <summary>
/// 模拟量测量：读被检板某点，按 Range 条件判定。PORT: Entry=TestADValueHost/TestBatterVoltageDy。
/// </summary>
public sealed class MeasurementHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "Measurement";

    /// <summary>
    /// 读某点测量值并按 Range 条件判定。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var dut = ctx.GetDevice<IDutDevice>();
        var point = ctx.Setting("Point") ?? "Default";
        var value = await dut.MeasureAsync(point, ct);
        var r = ctx.Evaluator.EvaluateAll(ctx.Conditions, value);
        ctx.Report($"测量 {point}={Num.Fmt(value)}：{r.Message}", r.Passed ? RealtimeLevel.Info : RealtimeLevel.Warn);
        return r.Passed ? StepResult.Pass(r.Message, Num.Fmt(value)) : StepResult.Fail(r.Message, Num.Fmt(value));
    }
}

/// <summary>
/// 整机功耗测试：由标准盒电流计读电流，按 Range 条件判定。
/// PORT: Entry=OutEngerTest/EngerTestHostByStable/Low/SuperLow/PowerOff。
/// </summary>
public sealed class PowerConsumptionHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "PowerConsumption";

    /// <summary>
    /// 读电流计电流，同步采两通道电压曲线，按 Range 条件判定。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var box = ctx.GetDevice<IStandardBox>();
        var current = await box.ReadCurrentAsync(Num.Int(ctx, "Channel", 1), ct);
        // 功耗测试期间同步采集两通道电压曲线（仿真，逐点实时推 UI + 落 test_process_data）
        await ProcessDataSimulator.StreamTwoChannelVoltageAsync(ctx, ct: ct);
        var r = ctx.Evaluator.EvaluateAll(ctx.Conditions, current);
        ctx.Report($"功耗电流={Num.Fmt(current)}mA：{r.Message}");
        return r.Passed ? StepResult.Pass($"{Num.Fmt(current)}mA 合格", Num.Fmt(current)) : StepResult.Fail(r.Message, Num.Fmt(current));
    }
}

/// <summary>
/// 测试完成：断电、复位。PORT: Entry=FinishAP。
/// </summary>
public sealed class FinishHandler : IStepHandler
{
    /// <summary>
    /// 处理的测试项类型。
    /// </summary>
    public string Kind => "Finish";

    /// <summary>
    /// 断电、工装复位。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var box = ctx.GetDevice<IStandardBox>();
        await box.PowerOffAsync(Num.Int(ctx, "PowerChannel", ctx.Position.Index), ct);
        ctx.Report("已断电，工装复位", RealtimeLevel.Success);
        return StepResult.Pass("测试完成");
    }
}
