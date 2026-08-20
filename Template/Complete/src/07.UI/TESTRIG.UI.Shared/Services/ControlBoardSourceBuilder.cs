using System.Text;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 控制板处理器源码生成器 —— 由「测试项维护页」导出功能使用。
/// 从 <see cref="JigManifest"/>（基本信息 + 号位 + 测试项）生成 <c>{设备族}_ControlBoard.cs</c>
/// 处理器骨架源码：可编译的 <c>{设备族}Ops</c> 辅助类 + 每个测试项一个 <see cref="IStepHandler"/>，
/// 测试逻辑留 TODO 待后续填充。JSON 侧直接复用 <see cref="ManifestWriter.ToJson"/>。
/// </summary>
public static class ControlBoardSourceBuilder
{
    /// <summary>
    /// 从清单生成 <c>{设备族}_ControlBoard.cs</c> 处理器骨架源码（可编译，测试逻辑留 TODO 待填充）。
    /// </summary>
    /// <param name="m">清单。</param>
    /// <returns>源码文本。</returns>
    public static string Build(JigManifest m)
    {
        var nsPrefix = ResolveNamespacePrefix();
        // 类名/命名空间用清单 Key（同一 DeviceFamily 下多份清单须各自独立，避免同名冲突）
        var dut = SanitizeIdentifier(m.Key);
        var sb = new StringBuilder();
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine($"using {nsPrefix}.Core.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {nsPrefix}.TestSteps.{dut}.{dut}_ControlBoard;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 控制板（设备族 {dut}）测试处理器骨架 —— 由「测试项维护页」导出，供后续实现测试逻辑。");
        sb.AppendLine($"/// 导出自清单：{m.Key}（{m.BoardName}）。请按 manifest 号位/测试项补充实现。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal sealed class {dut}Ops");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ITestContext _ctx;");
        sb.AppendLine("    private readonly CancellationToken _ct;");
        sb.AppendLine();
        sb.AppendLine($"    public {dut}Ops(ITestContext ctx, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        _ctx = ctx;");
        sb.AppendLine("        _ct = ct;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>数值格式化（保留三位有效小数）。</summary>");
        sb.AppendLine("    public static string F(double v) => v.ToString(\"0.###\", CultureInfo.InvariantCulture);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>推送实时消息（UI 过程日志）。</summary>");
        sb.AppendLine("    public void Report(string msg, RealtimeLevel level = RealtimeLevel.Info) => _ctx.Report(msg, level);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>稳定延时（真机/仿真都等待；如需仿真跳过请按设备状态调整）。</summary>");
        sb.AppendLine("    public Task Sleep(int ms)");
        sb.AppendLine("    {");
        sb.AppendLine("        Report($\"等待 {ms}ms\");");
        sb.AppendLine("        return Task.Delay(ms, _ct);");
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
        sb.AppendLine("    /// <summary>对测量值按条件名判定，报「读回+区间+结论」并返回是否通过（条件缺失记为不通过）。</summary>");
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
        sb.AppendLine("}");
        sb.AppendLine();
        foreach (var s in m.Steps)
        {
            var handlerName = $"{SanitizeIdentifier(s.Key)}{dut}Handler";
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {s.Name}。测试项 Key：{s.Key}。");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public sealed class {handlerName} : IStepHandler");
            sb.AppendLine("{");
            sb.AppendLine($"    public string Kind => \"{Cs(s.Key)}\";");
            // DeviceFamily 输出清单 Key（转换生成 handler 亦为 Key，运行时按 manifest.Key 解析）
            sb.AppendLine($"    public string? DeviceFamily => \"{Cs(m.Key)}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>执行本测试项。</summary>");
            sb.AppendLine("    public Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var op = new {dut}Ops(ctx, ct);");
            sb.AppendLine("        // TODO(维护页导出): 在此实现测试逻辑（按需调用 op.Report / op.Sleep / op.Judge 与设备驱动）。");
            if (s.Conditions.Count > 0)
            {
                sb.AppendLine("        // 判定条件：" + string.Join("；", s.Conditions.Select(c => $"{c.Name} [{c.Min}~{c.Max}]{c.Unit}")));
            }
            if (s.Parameters.Count > 0)
            {
                sb.AppendLine("        // 参数：" + string.Join("；", s.Parameters.Select(p => $"{p.Name}={p.Value}{p.Unit}")));
            }
            sb.AppendLine("        // 骨架默认直接通过，请替换为真实测量/判定。");
            sb.AppendLine($"        return Task.FromResult(StepResult.Pass(\"{Cs(s.Name)}（骨架未实现）\"));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// 解析当前项目的命名空间前缀：程序集名形如 <c>{代号}.UI.Shared</c>，取 <c>{代号}</c>（模板态为 TESTRIG，
    /// 生成态为实际产品代号），保证导出的 cs 使用与宿主项目一致的 using/namespace。
    /// </summary>
    /// <returns>命名空间前缀。</returns>
    private static string ResolveNamespacePrefix()
    {
        var name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "TESTRIG";
        const string suffix = ".UI.Shared";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    /// <summary>
    /// 转为合法 C# 标识符（非法字符替换为下划线；首字符为数字时加下划线前缀）。
    /// </summary>
    /// <param name="value">原始字符串。</param>
    /// <returns>合法标识符。</returns>
    public static string SanitizeIdentifier(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in string.IsNullOrWhiteSpace(value) ? "Board" : value.Trim())
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        if (sb.Length == 0) return "Board";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>
    /// 转义为可嵌入 C# 字符串字面量的文本（反斜杠与双引号）。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>转义后文本。</returns>
    private static string Cs(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
