using System.Text;

namespace WPFCli.Engine;

/// <summary>
/// 测试处理器源码生成器 —— 从旧脚本方法体生成新体系处理器集合：
///   1) <c>{dut}Ops</c> 辅助类（封装工装/被检/标准盒操作、延时、判定等）
///   2) 每个测试任务一个 <c>IStepHandler</c>（<see cref="Kind"/> = Entry，<see cref="DeviceFamily"/> = 被检类型）
/// 方法体转译委托 <see cref="LegacyScriptTranslator"/>；ConST811A 整机任务有专用 APC2 回读分支。
/// </summary>
internal static class TestStepSourceGenerator
{
    /// <summary>生成处理器源码：Ops 辅助类 + 每任务一个 IStepHandler（Kind = Entry，DeviceFamily = 被检类型）。</summary>
    internal static string BuildHandlerSource(string script, IReadOnlyList<JigTask> tasks, string dut, string suffix, string deviceFamily, List<string> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.IO.Ports;");
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine("using TESTRIG.Devices.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace TESTRIG.TestSteps.{dut}.{dut}_{suffix};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板（设备族 {dut}）测试**设备特有**处理器集合。**自动转换**自旧");
        sb.AppendLine($"/// <c>{dut}_MainBoard_Auto.cs</c> 的测试方法与 <c>.distributed.json</c> 任务配置：继电器指令序列");
        sb.AppendLine("/// （GZP21/P06 共享设备）、电压/电流读数、被检指令与 Range 判定。");
        sb.AppendLine($"/// 工装用 <see cref=\"IMachineTestTool\"/>，被检用 <see cref=\"I{dut}Dut\"/>。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal sealed class {dut}Ops");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ITestContext _ctx;");
        sb.AppendLine("    private readonly CancellationToken _ct;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>GZP21 共享工装（继电器输出）。</summary>");
        sb.AppendLine("    public readonly IMachineTestTool Gzp21;");
        sb.AppendLine("    /// <summary>P06/ConST810 共享设备（电压/电流采样）。</summary>");
        sb.AppendLine("    public readonly IMachineTestTool P06;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>被检 {dut} 专属驱动。</summary>");
        sb.AppendLine($"    public readonly I{dut}Dut Dut;");
        sb.AppendLine();
        sb.AppendLine($"    public {dut}Ops(ITestContext ctx, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        _ctx = ctx;");
        sb.AppendLine("        _ct = ct;");
        sb.AppendLine("        Gzp21 = ctx.GetDevice<IMachineTestTool>(\"GZP21\");");
        sb.AppendLine("        P06 = ctx.GetDevice<IMachineTestTool>(\"P06\");");
        sb.AppendLine($"        Dut = ctx.GetDevice<I{dut}Dut>();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>数值格式化（保留三位有效小数）。</summary>");
        sb.AppendLine("    public static string F(double v) => v.ToString(\"0.###\", CultureInfo.InvariantCulture);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>推送实时消息。</summary>");
        sb.AppendLine("    public void Report(string m, RealtimeLevel l = RealtimeLevel.Info) => _ctx.Report(m, l);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>真机稳定延时（继电器切档/设值后需等待）。PORT: 旧 Thread.Sleep / ScriptHelper.Thread_Sleep。</summary>");
        sb.AppendLine("    public Task Sleep(int ms)");
        sb.AppendLine("    {");
        sb.AppendLine("        Report(P06.IsRealHardware ? $\"等待 {ms}ms\" : $\"等待 {ms}ms（仿真跳过）\");");
        sb.AppendLine("        return P06.IsRealHardware ? Task.Delay(ms, _ct) : Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>发共享工装输出指令（按名称映射到 GZP21 通道）。</summary>");
        sb.AppendLine("    public Task Relay(string cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        Report($\"工装输出指令：{cmd}\");");
        sb.AppendLine("        return Gzp21.SetOutputAsync(cmd, true, _ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读 DAM6803D 某通道电压。PORT: DSTB.GetVoltageMeasureValue。</summary>");
        sb.AppendLine("    public Task<double> ReadVolt(int channel) => P06.ReadVoltageAsync(channel, _ct);");
        sb.AppendLine("    public Task<double> ReadCurrent(int channel) => P06.ReadCurrentAsync(channel, _ct);");
        sb.AppendLine("    /// <summary>回放旧平台中可直接映射的 P21/GZP21/P06 调用；复杂上下文参数不在此层猜测。</summary>");
        sb.AppendLine("    public async Task ExecuteLegacyAsync(IReadOnlyList<string> calls, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var call in calls)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = call.Split('|', 3);");
        sb.AppendLine("            if (p.Length < 2) continue;");
        sb.AppendLine("            var device = p[0];");
        sb.AppendLine("            var method = p[1];");
        sb.AppendLine("            var arg = p.Length == 3 ? p[2] : \"\";");
        sb.AppendLine("            IReadOnlyList<string>? args = string.IsNullOrWhiteSpace(arg) ? null : new[] { arg.Trim() };");
        sb.AppendLine("            if (device == \"GZP21\")");
        sb.AppendLine("            {");
        sb.AppendLine("                var open = !arg.Contains(\"Close\", StringComparison.OrdinalIgnoreCase);");
        sb.AppendLine("                var outputName = method.Replace(\"Set\", \"\").Replace(\"State\", \"\");");
        sb.AppendLine("                await Gzp21.SetOutputAsync(outputName, open, ct);");
        sb.AppendLine("                continue;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (device == \"P21\")");
        sb.AppendLine("            {");
        sb.AppendLine("                if (method.StartsWith(\"Get\", StringComparison.OrdinalIgnoreCase) || method.StartsWith(\"Is\", StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine("                    _ = await Dut.QueryTextAsync(method, args, ct);");
        sb.AppendLine("                else");
        sb.AppendLine("                    await Dut.CommandAsync(method, args, ct);");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (device == \"P06\")");
        sb.AppendLine("            {");
        sb.AppendLine("                if (method.Contains(\"Voltage\", StringComparison.OrdinalIgnoreCase)) _ = await P06.ReadVoltageAsync(0, ct);");
        sb.AppendLine("                else if (method.Contains(\"Current\", StringComparison.OrdinalIgnoreCase)) _ = await P06.ReadCurrentAsync(0, ct);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>按名取条件（找不到返回 null）。</summary>");
        sb.AppendLine("    public ConditionDescriptor? Cond(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var c in _ctx.Conditions)");
        sb.AppendLine("            if (c.Name == name) return c;");
        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>对某测量值按指定条件名判定，报「读回+区间+结论」并返回是否通过（条件缺失记为不通过）。</summary>");
        sb.AppendLine("    public bool Judge(string condName, double value, string label, string unit)");
        sb.AppendLine("    {");
        sb.AppendLine("        var cond = Cond(condName);");
        sb.AppendLine("        if (cond is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            Report($\"{label} {F(value)}{unit}：缺少判定条件 {condName}\", RealtimeLevel.Warn);");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine("        var r = _ctx.Evaluator.Evaluate(cond, value);");
        sb.AppendLine("        Report($\"{label} {F(value)}{unit}：{r.Message}\", r.Passed ? RealtimeLevel.Info : RealtimeLevel.Warn);");
        sb.AppendLine("        return r.Passed;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>掐头去尾各 5 点（旧 ScriptHelperKVP.TrimCurrents 语义）。</summary>");
        sb.AppendLine("    public static List<double> TrimCurrents(List<double> values)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (values.Count <= 10) return values;");
        sb.AppendLine("        return values.Skip(5).Take(values.Count - 10).ToList();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var task in tasks)
        {
            var handlerName = $"{task.Entry}{dut}Handler";
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {task.Name}。PORT: 旧脚本方法 {task.Entry}（JSON Entry: {task.Entry}）。");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public sealed class {handlerName} : IStepHandler");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>处理的测试项类型。</summary>");
            sb.AppendLine($"    public string Kind => \"{task.Entry}\";");
            sb.AppendLine($"    /// <summary>限定设备家族（仅 {dut} 的板使用）。</summary>");
            sb.AppendLine($"    public string? DeviceFamily => \"{deviceFamily}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>执行本测试项。</summary>");
            sb.AppendLine("    /// <param name=\"ctx\">测试项上下文。</param>");
            sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
            sb.AppendLine("    /// <returns>测试项结果。</returns>");
            sb.AppendLine("    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var op = new {dut}Ops(ctx, ct);");
            sb.AppendLine("        var pass = true;");
            var body = LegacyScriptTranslator.ExtractScriptBody(script, task.Entry);
            var conStProbe = dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase)
                ? task.Entry switch
                {
                    "TestStorageCardPrincipal" => "GetStorageCardState",
                    "ModuleConnectStateTest" => "GetPressureModelOnlineState",
                    "ElectricalPowerTest" => "GetPowerSupplyCheck",
                    "TestControllerBroadPower" => "GetControllerBroadPowerCheckState",
                    "NTCTest" => "GetMotor_Temperature",
                    "AtmosSensorTest" => "GetAtmos",
                    _ => null,
                }
                : null;
            if (dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase) && task.Entry.Equals("TestDeviceWriteSN", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("        var requestedSn = ctx.Parameter(\"写入SN\")?.Value?.Trim() ?? ctx.SerialNumber ?? \"\";");
                sb.AppendLine("        if (string.IsNullOrWhiteSpace(requestedSn)) pass = false;");
                sb.AppendLine("        else pass &= await op.Dut.SetSerialNumberAsync(requestedSn, ct);");
                sb.AppendLine("        if (pass) ctx.SerialNumber = await op.Dut.ReadSerialNumberAsync(ct);");
            }
            else if (dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase) && task.Entry.Equals("TestDeviceWriteType", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("        var productModel = ctx.Setting(\"ProductModel\") ?? \"ConST811A\";");
                sb.AppendLine("        pass &= await op.Dut.SetPrimaryDeviceTypeAsync(productModel, ct);");
            }
            else if (dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase) && task.Entry.Equals("TestSoftVersions", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("        var firmware = await op.Dut.ReadFirmwareVersionAsync(ct);");
                sb.AppendLine("        op.Report($\"固件版本：{firmware}\");");
                sb.AppendLine("        pass &= !string.IsNullOrWhiteSpace(firmware);");
            }
            else if (dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase) && task.Entry.Equals("TestLAN", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("        var ip = await op.Dut.QueryTextAsync(\"GetStaticETHemetIPAddress\", null, ct);");
                sb.AppendLine("        op.Report($\"设备网口地址：{ip}\");");
                sb.AppendLine("        pass &= !string.IsNullOrWhiteSpace(ip);");
            }
            else if (dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase) && task.Entry.Equals("FANTest", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("        await op.Dut.CommandAsync(\"SetFANOn\", null, ct);");
                sb.AppendLine("        await op.Sleep(500);");
                sb.AppendLine("        await op.Dut.CommandAsync(\"SetFANClose\", null, ct);");
            }
            else if (conStProbe is not null)
            {
                sb.AppendLine($"        var probe = await op.Dut.QueryTextAsync(\"{conStProbe}\", null, ct);");
                sb.AppendLine("        op.Report($\"设备回读：{probe}\");");
                sb.AppendLine("        pass &= !string.IsNullOrWhiteSpace(probe);");
            }
            else if (body != null)
            {
                var (lines, entryTodos) = LegacyScriptTranslator.TranslateBody(body, task.Conditions, dut);
                todos.AddRange(entryTodos);
                foreach (var line in lines)
                {
                    // 旧脚本控制块依赖 dynamic/UI 对象，条件本身已转为设备回放或判定；
                    // 丢弃残留裸大括号，避免被注释掉的 if/else 造成生成源码结构失衡。
                    var trimmed = line.Trim();
                    if (trimmed is "{" or "}" || trimmed.Equals("if (!res)", StringComparison.Ordinal))
                        continue;
                    sb.AppendLine($"        {line}");
                }
                // 旧脚本通常夹杂大量 WPF/UI 和结果包装代码。对其中可识别的真实设备调用
                // 追加回放，避免这些非业务语句阻断硬件测试；复杂参数仍在报告中明确列出。
                var legacyCalls = LegacyScriptTranslator.ExtractLegacyCalls(body);
                if (entryTodos.Count > 0 && legacyCalls.Count > 0)
                {
                    var literals = string.Join(", ", legacyCalls.Select(c => $"\"{LegacyScriptTranslator.EscapeCSharp(c)}\""));
                    sb.AppendLine($"        await op.ExecuteLegacyAsync(new[] {{ {literals} }}, ct);");
                    sb.AppendLine($"        op.Report(\"{task.Name} 旧平台设备调用已按真实驱动回放，仍有 {entryTodos.Count} 条非设备语句待核对\", RealtimeLevel.Warn);");
                }
                else if (entryTodos.Count > 0)
                {
                    sb.AppendLine($"        op.Report(\"{task.Name} 未发现可回放的设备调用，需人工迁移\", RealtimeLevel.Error);");
                    sb.AppendLine("        pass = false;");
                }
            }
            else
            {
                todos.Add($"任务 {task.Entry}：未找到脚本方法体，无法执行");
                sb.AppendLine($"        op.Report(\"{task.Name} 未找到脚本方法体，无法执行\", RealtimeLevel.Error);");
                sb.AppendLine("        pass = false;");
            }
            sb.AppendLine($"        op.Report(pass ? \"✓ {task.Name}通过\" : \"✗ {task.Name}未通过\", pass ? RealtimeLevel.Success : RealtimeLevel.Error);");
            sb.AppendLine($"        return pass ? StepResult.Pass(\"{task.Name}通过\") : StepResult.Fail(\"{task.Name}未通过\");");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
