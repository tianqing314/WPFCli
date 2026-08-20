using System.Globalization;
using System.Text.RegularExpressions;

namespace WPFCli.Engine;

/// <summary>
/// 旧 Bots.TestBench 脚本翻译器 —— 把旧 <c>public dynamic 方法(dynamic item)</c> 方法体逐行
/// 模式匹配转译为新 TESTRIG 体系语句（<c>await op.Dut.*</c> / <c>await op.Relay</c> / <c>op.Judge</c> 等），
/// 并提取可回放的旧平台设备调用。无法映射的语句转成 TODO 注释由调用方汇总。
///
/// 所有正则均为预编译（<see cref="RegexOptions.Compiled"/>），中文标识符范围 \u4e00-\u9fff。
/// </summary>
internal static class LegacyScriptTranslator
{
    // ===== 旧体系解析正则（逐字字符串，中文标识符 \u4e00-\u9fff）=====

    /// <summary>脚本测试方法声明：public dynamic 方法名(dynamic item)。</summary>
    internal static readonly Regex ScriptMethodPattern = new(
        @"public\s+dynamic\s+(\w+)\s*\(dynamic\s+item\)", RegexOptions.Compiled);

    /// <summary>条件绑定：var 变量 = DC.Conditions.FirstOrDefault(o => o.Name == "条件名") as RangeCondition;</summary>
    private static readonly Regex ConditionBindPattern = new(
        @"var\s+(\w+)\s*=\s*DC\.Conditions\.FirstOrDefault\(o\s*=>\s*o\.Name\s*==\s*""([^""]+)""\)\s*as\s+RangeCondition;",
        RegexOptions.Compiled);

    /// <summary>被检命令：ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.X));</summary>
    private static readonly Regex P22CommandPattern = new(
        @"ScriptHelper\.AddNewRange\(DC\.P22\.ExecuteAnyCommand_NoResponse\(SimpleCommandEnum\.([\w\u4e00-\u9fff]+)\)\);",
        RegexOptions.Compiled);

    /// <summary>继电器命令：ScriptHelper.AddNewRange(DC.DSTB.RespondToCommand(DynamicStandardTestBench.CommandEnum.X));</summary>
    private static readonly Regex RelayCommandPattern = new(
        @"ScriptHelper\.AddNewRange\(DC\.DSTB\.RespondToCommand\(DynamicStandardTestBench\.CommandEnum\.([\w\u4e00-\u9fff]+)\)\);",
        RegexOptions.Compiled);

    /// <summary>网络继电器全通道复位：ScriptHelper.AddNewRange(DC.DSTB.NetSwitchACloseAllChannels());</summary>
    private static readonly Regex NetSwitchPattern = new(
        @"ScriptHelper\.AddNewRange\(DC\.DSTB\.NetSwitch([ABC])CloseAllChannels\((false|true)?\)\);",
        RegexOptions.Compiled);

    /// <summary>补充连接：var res = DC.P22.ReplenishLink();</summary>
    private static readonly Regex ReplenishLinkPattern = new(
        @"var\s+(\w+)\s*=\s*DC\.P22\.ReplenishLink\(\);", RegexOptions.Compiled);

    /// <summary>双参结果记录：ScriptHelper.AddEasyResult(new ScriptHelperKVP("建立连接", res));</summary>
    private static readonly Regex EasyResult2Pattern = new(
        @"ScriptHelper\.AddEasyResult\(new\s+ScriptHelperKVP\(""([^""]+)"",\s*(\w+)\)\);", RegexOptions.Compiled);

    /// <summary>单参结果记录：ScriptHelper.AddEasyResult(new ScriptHelperKVP("建立连接失败,重试中"));</summary>
    private static readonly Regex EasyResult1Pattern = new(
        @"ScriptHelper\.AddEasyResult\(new\s+ScriptHelperKVP\(""([^""]+)""\)\);", RegexOptions.Compiled);

    /// <summary>电压判定（首次声明变量）：AddNewIsRangeJudge(DSTB.GetVoltageMeasureValue(0, out double CDPVoltage), cond.Lower, cond.Upper);</summary>
    private static readonly Regex VoltJudgeDeclPattern = new(
        @"ScriptHelper\.AddNewIsRangeJudge\(DC\.DSTB\.GetVoltageMeasureValue\((\d+),\s*out\s+double\s+(\w+)\),\s*(\w+)\.Lower,\s*(\w+)\.Upper\);",
        RegexOptions.Compiled);

    /// <summary>电压判定（复用变量）：AddNewIsRangeJudge(DSTB.GetVoltageMeasureValue(0, out CDPVoltage), cond.Lower, cond.Upper);</summary>
    private static readonly Regex VoltJudgeUsePattern = new(
        @"ScriptHelper\.AddNewIsRangeJudge\(DC\.DSTB\.GetVoltageMeasureValue\((\d+),\s*out\s+(\w+)\),\s*(\w+)\.Lower,\s*(\w+)\.Upper\);",
        RegexOptions.Compiled);

    /// <summary>功耗均值判定：AddNewIsRangeJudge(new ScriptHelperKVP(...) { JudgeObject = currents.Average(...) }, cond.Lower, cond.Upper);</summary>
    private static readonly Regex CurrentJudgePattern = new(
        @"ScriptHelper\.AddNewIsRangeJudge\(new\s+ScriptHelperKVP\(currents\.FirstOrDefault\(\)\.Content\)\s*\{\s*JudgeObject\s*=\s*currents\.Average\(o\s*=>\s*double\.Parse\(o\.JudgeObject\.ToString\(\)\)\)\s*\},\s*(\w+)\.Lower,\s*(\w+)\.Upper\);",
        RegexOptions.Compiled);

    /// <summary>延时：ScriptHelper.Thread_Sleep(new ScriptHelperKVP(5 * 1000));</summary>
    private static readonly Regex SleepPattern = new(
        @"ScriptHelper\.Thread_Sleep\(new\s+ScriptHelperKVP\(([^)]+)\)\);", RegexOptions.Compiled);

    /// <summary>电流采样：ScriptHelperKVP scriptKvp = DC.DSTB.GetCurrentMeasureValue(false, 1, out double value);</summary>
    private static readonly Regex CurrentSamplePattern = new(
        @"ScriptHelperKVP\s+scriptKvp\s*=\s*DC\.DSTB\.GetCurrentMeasureValue\(false,\s*(\d+),\s*out\s+double\s+value\);",
        RegexOptions.Compiled);

    /// <summary>采集列表声明：List&lt;ScriptHelperKVP&gt; currents = new List&lt;ScriptHelperKVP&gt;();</summary>
    private static readonly Regex CurrentsDeclPattern = new(
        @"List<ScriptHelperKVP>\s+currents\s*=\s*new\s+List<ScriptHelperKVP>\(\);", RegexOptions.Compiled);

    /// <summary>采集列表重新赋值：currents = new List&lt;ScriptHelperKVP&gt;();</summary>
    private static readonly Regex CurrentsAssignPattern = new(
        @"^\s*currents\s*=\s*new\s+List<ScriptHelperKVP>\(\);", RegexOptions.Compiled);

    /// <summary>采集循环：while (currents.Count &lt; 30)</summary>
    private static readonly Regex WhilePattern = new(
        @"while\s*\(currents\.Count\s*<\s*(\d+)\)", RegexOptions.Compiled);

    /// <summary>NaN 过滤：if (value != double.NaN) currents.Add(scriptKvp);</summary>
    private static readonly Regex NaNGuardPattern = new(
        @"if\s*\(value\s*!=\s*double\.NaN\)\s*currents\.Add\(scriptKvp\);", RegexOptions.Compiled);

    /// <summary>线程延时：Thread.Sleep(500);</summary>
    private static readonly Regex ThreadSleepPattern = new(
        @"Thread\.Sleep\((\d+)\);", RegexOptions.Compiled);

    /// <summary>掐头去尾：currents = ScriptHelperKVP.TrimCurrents(currents);</summary>
    private static readonly Regex TrimCurrentsPattern = new(
        @"currents\s*=\s*ScriptHelperKVP\.TrimCurrents\(currents\);", RegexOptions.Compiled);

    /// <summary>旧脚本设备调用（回放层只接管 P21/GZP21/P06，其他旧扩展设备保留报告）。</summary>
    private static readonly Regex LegacyDeviceCallPattern = new(
        @"GetDevice\(""(?<device>P21|GZP21|P06)""\)\.(?<method>\w+)\((?<args>[^()]*)\)",
        RegexOptions.Compiled);

    // ===== ConST811A 整机脚本转译规则（G1-G15）=====

    /// <summary>G7: Task.Delay(N).Wait(); → await Task.Delay(N, ct);</summary>
    private static readonly Regex TaskDelayWaitPattern = new(
        @"Task\.Delay\((\d+)\)\.Wait\(\)\s*;", RegexOptions.Compiled);

    /// <summary>G9: RealTimeWatch var = new RealTimeWatch(); → Stopwatch</summary>
    private static readonly Regex RealTimeWatchPattern = new(
        @"RealTimeWatch\s+(\w+)\s*=\s*new\s+RealTimeWatch\(\)\s*;", RegexOptions.Compiled);

    /// <summary>G3: ScriptHelper.OpenInfoConfirmWindow(item, msg) / item.Parent.VM.OpenInfoConfirmWindow(msg)</summary>
    private static readonly Regex ConfirmWindowPattern = new(
        @"(?:bool\?\s+\w+\s*=\s*)?(?:ScriptHelper\.OpenInfoConfirmWindow\(item,\s*|item\.Parent\.VM\.OpenInfoConfirmWindow\()(.+?)\)\s*;?\s*$",
        RegexOptions.Compiled);

    /// <summary>G4: ScriptHelper.OpenInfoImgConfirmWindow(msg, img, ...)</summary>
    private static readonly Regex ImgConfirmWindowPattern = new(
        @"(?:bool\?\s+\w+\s*=\s*)?ScriptHelper\.OpenInfoImgConfirmWindow\((.+?)\)\s*;?\s*$",
        RegexOptions.Compiled);

    /// <summary>G2: item.GetDevice("GZP21").Set*State(OpenCloseState.X)</summary>
    private static readonly Regex Gzp21SetStatePattern = new(
        @"item\.GetDevice\(""GZP21""\)\.Set(\w+)State\(OpenCloseState\.(\w+)\)\s*;?\s*$",
        RegexOptions.Compiled);

    /// <summary>G2: if (item.GetDevice("GZP21") != null && item.GetDevice("GZP21").Gett27VState(out var))</summary>
    private static readonly Regex Gzp21NullCheck27VPattern = new(
        @"if\s*\(item\.GetDevice\(""GZP21""\)\s*!=\s*null\s*&&\s*item\.GetDevice\(""GZP21""\)\.Gett27VState\(out\s+(\w+)\)\)",
        RegexOptions.Compiled);

    /// <summary>G2: if (item.GetDevice("GZP21") != null) → 丢弃 null 检查</summary>
    private static readonly Regex Gzp21NullCheckOnlyPattern = new(
        @"if\s*\(item\.GetDevice\(""GZP21""\)\s*!=\s*null\)\s*\{?\s*$", RegexOptions.Compiled);

    /// <summary>G1: if (!item.GetDevice("P21").Method(args)) { / if (item.GetDevice("P21").Method(args)) {</summary>
    private static readonly Regex P21IfCallPattern = new(
        @"if\s*\(\s*(!?)item\.GetDevice\(""P21""\)\.(\w+)\(([^)]*)\)\s*\)\s*\{?\s*$",
        RegexOptions.Compiled);

    /// <summary>G1: item.GetDevice("P21").Method(args); 独立调用</summary>
    private static readonly Regex P21StandaloneCallPattern = new(
        @"^item\.GetDevice\(""P21""\)\.(\w+)\(([^)]*)\)\s*;?\s*$", RegexOptions.Compiled);

    /// <summary>G1: Type var = item.GetDevice("P21").Method(args);</summary>
    private static readonly Regex P21VarCallPattern = new(
        @"^(\w+)\s+(\w+)\s*=\s*item\.GetDevice\(""P21""\)\.(\w+)\(([^)]*)\)\s*;?\s*$",
        RegexOptions.Compiled);

    /// <summary>G6: var = item.Conditions[N] as (Value|Range)Condition; //注释</summary>
    private static readonly Regex ConditionWithCommentPattern = new(
        @"(\w+)\s*=\s*item\.Conditions\[(\d+)\]\s*as\s+(Value|Range)Condition;\s*//(.+)$",
        RegexOptions.Compiled);

    /// <summary>G6: var = item.Conditions[N] as (Value|Range)Condition;</summary>
    private static readonly Regex ConditionNoCommentPattern = new(
        @"(\w+)\s*=\s*item\.Conditions\[(\d+)\]\s*as\s+(Value|Range)Condition;\s*$",
        RegexOptions.Compiled);

    /// <summary>B1: 条件变量 IsTrue 调用：condVar.IsTrue(valueExpr) → op.Judge（ConditionDescriptor 无 IsTrue 方法）。</summary>
    private static readonly Regex ConditionIsTruePattern = new(
        @"(\w+)\.IsTrue\(([^)]+)\)", RegexOptions.Compiled);

    /// <summary>G5+G12: (rData[N] as TextData).Value = X;</summary>
    private static readonly Regex TextDataAssignPattern = new(
        @"\(rData\[(\d+)\]\s*as\s+TextData\)\.Value\s*=\s*(.+);", RegexOptions.Compiled);

    /// <summary>
    /// G5+G12: rData[N].Value = X;（不带 as TextData，可能为 chained 赋值 msg.Content = rData[N].Value = X;）。
    /// 把 rData 结果赋值折进 op.Report，名按 rData 索引推断。
    /// </summary>
    private static readonly Regex RDataDirectAssignPattern = new(
        @"rData\[(\d+)\]\.Value\s*=\s*(.+);", RegexOptions.Compiled);

    /// <summary>G5+G12: new TextData("名") 声明（跳过）</summary>
    private static readonly Regex TextDataNewPattern = new(
        @"new\s+TextData\(""([^""]+)""\)", RegexOptions.Compiled);

    /// <summary>G5+G12: tdata = new TextData("名") 赋值声明（跳过）</summary>
    private static readonly Regex TextDataAssignDeclPattern = new(
        @"\w+\s*=\s*new\s+TextData\(""([^""]+)""\)\s*;?\s*$", RegexOptions.Compiled);

    /// <summary>G11: ListValueData var = new ListValueData("名");</summary>
    private static readonly Regex ListValueDataDeclPattern = new(
        @"ListValueData\s+(\w+)\s*=\s*new\s+ListValueData\(""([^""]+)""\)\s*;", RegexOptions.Compiled);

    /// <summary>G11: var.AppendAsync(point.WithDateTime());</summary>
    private static readonly Regex AppendAsyncPattern = new(
        @"(\w+)\.AppendAsync\((\w+)\.WithDateTime\(\)\)\s*;", RegexOptions.Compiled);

    /// <summary>G8: goto label;</summary>
    private static readonly Regex GotoPattern = new(
        @"goto\s+(\w+)\s*;", RegexOptions.Compiled);

    /// <summary>G8: retry label: (tryagain/tryagainN)</summary>
    private static readonly Regex RetryLabelPattern = new(
        @"^(tryagain\w*)\s*:\s*$", RegexOptions.Compiled);

    /// <summary>G8: 重试确认行（if (!(await ctx.ConfirmAsync(...))) pass = false;）→ 迁移时改 取消=break</summary>
    private static readonly Regex RetryConfirmPattern = new(
        @"^(if \(!\(await ctx\.ConfirmAsync\(.*\)\)\)) pass = false;$", RegexOptions.Compiled);

    /// <summary>枚举参数转字符串字面量：EnumName.Member → "Member"</summary>
    private static readonly Regex EnumArgPattern = new(
        @"(\w+)\.(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// 提取旧脚本方法体。旧动态工装使用 WatchAndProcessIntergrade lambda，
    /// ConST811A 整机脚本则直接在方法体内执行；两种形态都保留完整大括号范围。
    /// </summary>
    internal static List<string>? ExtractScriptBody(string script, string methodName)
    {
        var m = ScriptMethodPattern.Match(script, IndexOfMethod(script, methodName));
        if (!m.Success) return null;
        // 优先取旧 lambda 体；没有 lambda 时取方法声明后的第一个大括号。
        var lambdaMarker = script.IndexOf("WatchAndProcessIntergrade", m.Index);
        var lambdaBrace = lambdaMarker >= 0 ? script.IndexOf('{', lambdaMarker) : script.IndexOf('{', m.Index);
        if (lambdaBrace < 0) return null;
        // 大括号配对
        var depth = 0;
        var i = lambdaBrace;
        for (; i < script.Length; i++)
        {
            if (script[i] == '{') depth++;
            else if (script[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }
        var body = script.Substring(lambdaBrace + 1, i - lambdaBrace - 1);
        return body.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }

    private static int IndexOfMethod(string script, string methodName)
    {
        var idx = script.IndexOf($"public dynamic {methodName}(", StringComparison.Ordinal);
        return idx < 0 ? 0 : idx;
    }

    /// <summary>转译脚本体为新体系语句；返回 (语句行, TODO 项)。</summary>
    internal static (List<string> Lines, List<string> Todos) TranslateBody(
        List<string> body, IReadOnlyList<(string Name, double Min, double Max, string Unit)> conditions, string dut)
    {
        var lines = new List<string>();
        var todos = new List<string>();
        var unitByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in conditions)
            unitByName[c.Name] = c.Unit;
        var condVars = new Dictionary<string, string>(StringComparer.Ordinal); // 脚本变量 → 条件名
        var declaredCondVars = new HashSet<string>(StringComparer.Ordinal); // 已声明的条件变量名（G6 防重复声明）
        var declaredNonCondVars = new HashSet<string>(StringComparer.Ordinal); // 已声明的非条件变量名（G9+G10 防 CS0128）
        // B4: 遗留变量 fallback 预声明——先在已声明集合中占位，使正文中同名声明（若被保留）去重为赋值，避免 CS0128/CS1023
        foreach (var fallbackVar in LegacyFallbackVars.Keys)
            declaredNonCondVars.Add(fallbackVar);
        var varTypes = new Dictionary<string, string>(StringComparer.Ordinal); // 变量 → 类型（G1 out 推断）
        var textDataNames = new Dictionary<int, string>(); // rData 索引 → TextData 名（G5+G12）
        var textDataIdx = 0; // TextData 声明计数（用于 rData 索引映射）
        // G11: ListValueData 采集点收集：变量名 → (曲线名, 采集点列表)
        var listValueSeries = new Dictionary<string, (string Name, List<string> Points)>(StringComparer.Ordinal);
        // G8: 重试结构收集：标签 → (标签深度, 扁平行索引, 旧脚本行索引)；goto → 同类列表；确认弹窗扁平索引
        var retryLabels = new Dictionary<string, (int LabelDepth, int OpenIdx, int BodyIdx)>(StringComparer.Ordinal);
        var retryGotos = new Dictionary<string, List<(int GotoDepth, int GotoIdx, int BodyIdx)>>(StringComparer.Ordinal);
        var confirmFlatIndices = new List<int>();

        // 预合并多行语句：旧脚本常把 if\n(\n!cond\n) 拆成多行，需先合并才能匹配模式
        body = MergeContinuationLines(body);

        // G8: 旧脚本块深度前缀（在合并后的 body 上计算；净花括号数，字符串插值 {..} 成对平衡不影响计数）
        var depthBefore = new int[body.Count + 1];
        for (var k = 0; k < body.Count; k++)
            depthBefore[k + 1] = depthBefore[k] + CountNetBraces(body[k]);

        for (var i = 0; i < body.Count; i++)
        {
            var line = body[i].Trim();
            if (line.Length == 0) { lines.Add(""); continue; }
            if (line.StartsWith("//", StringComparison.Ordinal)) { lines.Add(line); continue; }

            // G1: 跟踪局部变量类型（用于 out 参数推断），在模式匹配之前执行
            RecordVarType(line, varTypes);

            // B1: 条件变量表达式翻译（在模式匹配之前执行，避免被其他规则吞掉）：
            //   - condVar.Value → double.Parse(condVar.Expected ?? "0")（ConditionDescriptor 无 Value）
            //   - condVar.IsTrue(x) → op.Judge("条件名", x, "变量名", "单位")（无 IsTrue 方法）
            line = TranslateCondExpressions(line, condVars, unitByName);

            var m = ConditionBindPattern.Match(line);
            if (m.Success) { condVars[m.Groups[1].Value] = m.Groups[2].Value; continue; }

            m = P22CommandPattern.Match(line);
            if (m.Success)
            {
                lines.Add($"await op.Dut.ExecuteAnyCommandNoResponseAsync({dut}Command.{m.Groups[1].Value}, ct);");
                continue;
            }

            m = RelayCommandPattern.Match(line);
            if (m.Success)
            {
                lines.Add($"await op.Relay(\"{m.Groups[1].Value}\");");
                continue;
            }

            m = NetSwitchPattern.Match(line);
            if (m.Success)
            {
                var relay = m.Groups[1].Value;
                var arg = m.Groups[2].Value;
                var method = relay switch
                {
                    "A" => "CloseAllAChannelsAsync",
                    "B" => "CloseAllBChannelsAsync",
                    _ => "CloseAllCChannelsAsync",
                };
                lines.Add($"await op.Gzp21.SetOutputAsync(\"{relay}\", true, ct);");
                continue;
            }

            m = ReplenishLinkPattern.Match(line);
            if (m.Success)
            {
                lines.Add($"var {m.Groups[1].Value} = await op.Dut.ReplenishLinkAsync(ct);");
                continue;
            }

            m = EasyResult2Pattern.Match(line);
            if (m.Success)
            {
                var val = ApplyEntityReplacements(m.Groups[2].Value);
                lines.Add($"op.Report($\"{m.Groups[1].Value} {{{val}}}\");");
                continue;
            }

            m = EasyResult1Pattern.Match(line);
            if (m.Success)
            {
                lines.Add($"op.Report(\"{m.Groups[1].Value}\", RealtimeLevel.Warn);");
                continue;
            }

            m = VoltJudgeDeclPattern.Match(line);
            if (m.Success)
            {
                var (condName, unit) = ResolveCond(m.Groups[3].Value, m.Groups[4].Value, condVars, unitByName, line, todos);
                lines.Add($"var {m.Groups[2].Value} = await op.ReadVolt({m.Groups[1].Value});");
                lines.Add($"pass &= op.Judge(\"{condName}\", {m.Groups[2].Value}, \"{m.Groups[2].Value}电压\", \"{unit}\");");
                continue;
            }

            m = VoltJudgeUsePattern.Match(line);
            if (m.Success)
            {
                var (condName, unit) = ResolveCond(m.Groups[3].Value, m.Groups[4].Value, condVars, unitByName, line, todos);
                lines.Add($"pass &= op.Judge(\"{condName}\", {m.Groups[2].Value}, \"{m.Groups[2].Value}电压\", \"{unit}\");");
                continue;
            }

            m = CurrentJudgePattern.Match(line);
            if (m.Success)
            {
                var (condName, unit) = ResolveCond(m.Groups[1].Value, m.Groups[2].Value, condVars, unitByName, line, todos);
                lines.Add($"pass &= op.Judge(\"{condName}\", currents.Count > 0 ? currents.Average() : 0, \"电流\", \"{unit}\");");
                continue;
            }

            m = SleepPattern.Match(line);
            if (m.Success) { lines.Add($"await op.Sleep({m.Groups[1].Value});"); continue; }

            m = CurrentSamplePattern.Match(line);
            if (m.Success)
            {
                lines.Add($"var value = await op.ReadCurrent({m.Groups[1].Value});");
                continue;
            }

            m = CurrentsDeclPattern.Match(line);
            if (m.Success) { lines.Add("var currents = new List<double>();"); continue; }

            m = CurrentsAssignPattern.Match(line);
            if (m.Success) { lines.Add("currents = new List<double>();"); continue; }

            m = WhilePattern.Match(line);
            if (m.Success) { lines.Add($"while (currents.Count < {m.Groups[1].Value})"); continue; }

            m = NaNGuardPattern.Match(line);
            if (m.Success) { lines.Add("if (!double.IsNaN(value)) currents.Add(value);"); continue; }

            // NaN 过滤两行形式：if (value != double.NaN) \n currents.Add(scriptKvp);
            if (line == "if (value != double.NaN)" && i + 1 < body.Count &&
                body[i + 1].Trim() == "currents.Add(scriptKvp);")
            {
                lines.Add("if (!double.IsNaN(value)) currents.Add(value);");
                i++;
                continue;
            }

            m = ThreadSleepPattern.Match(line);
            if (m.Success) { lines.Add($"await Task.Delay({m.Groups[1].Value}, ct);"); continue; }

            m = TrimCurrentsPattern.Match(line);
            if (m.Success) { lines.Add($"currents = {dut}Ops.TrimCurrents(currents);"); continue; }

            // ===== ConST811A 整机脚本转译规则（G1-G15）=====

            // G5+G12: new TextData("名") 声明 → 跳过（不生成代码），记录名→索引映射
            m = TextDataNewPattern.Match(line);
            if (m.Success) { textDataNames[textDataIdx++] = m.Groups[1].Value; continue; }
            m = TextDataAssignDeclPattern.Match(line);
            if (m.Success) { textDataNames[textDataIdx++] = m.Groups[1].Value; continue; }

            // G5+G12: (rData[N] as TextData).Value = X → op.Report
            m = TextDataAssignPattern.Match(line);
            if (m.Success)
            {
                var ri = int.Parse(m.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
                var name = textDataNames.TryGetValue(ri, out var n) ? n : $"结果{ri}";
                // G13+G10: 值中可能含 Util.LeakTestValueFormula / PressureUnit / ToUnit 等，统一应用实体替换
                var val = ApplyEntityReplacements(m.Groups[2].Value);
                lines.Add($"op.Report($\"{name}: {{{val}}}\");");
                continue;
            }

            // G5+G12: rData[N].Value = X;（不带 as TextData）→ op.Report
            // 覆盖 chained 形式 msg.Content = rData[N].Value = X; 与直接 rData[N].Value = X;
            m = RDataDirectAssignPattern.Match(line);
            if (m.Success)
            {
                var ri = int.Parse(m.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
                var name = textDataNames.TryGetValue(ri, out var n) ? n : $"结果{ri}";
                var val = ApplyEntityReplacements(m.Groups[2].Value);
                lines.Add($"op.Report($\"{name}: {{{val}}}\");");
                continue;
            }

            // G11: ListValueData 声明 → 改为 List<double>
            m = ListValueDataDeclPattern.Match(line);
            if (m.Success)
            {
                listValueSeries[m.Groups[1].Value] = (m.Groups[2].Value, new List<string>());
                lines.Add($"var {m.Groups[1].Value} = new List<double>();");
                continue;
            }

            // G11: AppendAsync → 攒入 List（不生成逐行代码）
            m = AppendAsyncPattern.Match(line);
            if (m.Success)
            {
                if (listValueSeries.TryGetValue(m.Groups[1].Value, out var entry))
                    entry.Points.Add(m.Groups[2].Value);
                continue;
            }

            // G13: Util.LeakTestValueFormula → LeakFormula.Compute + 枚举名修正
            // 应用完整 ApplyEntityReplacements（含 PressureUnit/ToUnit 扁平化），避免独立 LeakFormula 行 CS0103/CS1061
            if (line.Contains("Util.LeakTestValueFormula(", StringComparison.Ordinal))
            {
                lines.Add(ApplyEntityReplacements(line));
                continue;
            }

            // G3: OpenInfoConfirmWindow → ctx.ConfirmAsync(msg)
            m = ConfirmWindowPattern.Match(line);
            if (m.Success)
            {
                var msg = m.Groups[1].Value.Trim();
                msg = TranslateLegacyReferencesInMsg(msg);
                // 清理旧 goto 重试计数器引用（G8 RetryHelper 将管理重试计数）
                msg = msg.Replace("{++trynum}", "{1}").Replace("{trynum}", "{1}");
                lines.Add($"if (!(await ctx.ConfirmAsync({msg}, ct))) pass = false;");
                confirmFlatIndices.Add(lines.Count - 1);
                continue;
            }

            // G4: OpenInfoImgConfirmWindow → ctx.ConfirmAsync(msg, img)
            m = ImgConfirmWindowPattern.Match(line);
            if (m.Success)
            {
                var allArgs = SplitTopLevelArgs(m.Groups[1].Value);
                var msg = allArgs.Count > 0 ? allArgs[0] : "\"\"";
                msg = TranslateLegacyReferencesInMsg(msg);
                msg = msg.Replace("{++trynum}", "{1}").Replace("{trynum}", "{1}");
                var img = allArgs.Count > 1 ? allArgs[1] : "null";
                lines.Add($"if (!(await ctx.ConfirmAsync({msg}, {img}, ct))) pass = false;");
                confirmFlatIndices.Add(lines.Count - 1);
                continue;
            }

            // G2: GZP21 null check + Gett27VState → 独立 Get27VStateAsync
            m = Gzp21NullCheck27VPattern.Match(line);
            if (m.Success)
            {
                lines.Add($"var {m.Groups[1].Value} = await op.Gzp21.Get27VStateAsync(ct);");
                continue;
            }

            // G2: GZP21 null check only → 丢弃
            m = Gzp21NullCheckOnlyPattern.Match(line);
            if (m.Success) continue;

            // G2: GZP21 Set*State(OpenCloseState.X) → op.Gzp21.Set*Async(bool)
            m = Gzp21SetStatePattern.Match(line);
            if (m.Success)
            {
                var targetMethod = m.Groups[1].Value switch
                {
                    "PA" => "SetPaAsync",
                    "27V" => "Set27VAsync",
                    "Ele" => "SetElectricalAsync",
                    "Hart" => "SetHartAsync",
                    _ => null
                };
                if (targetMethod != null)
                {
                    var openFlag = m.Groups[2].Value == "Open";
                    lines.Add($"await op.Gzp21.{targetMethod}({(openFlag ? "true" : "false")}, ct);");
                    continue;
                }
            }

            // G1: if (!item.GetDevice("P21").Method(args)) → QueryBooleanAsync
            // 原行尾 {/} 会被 TestStepSourceGenerator 当作残留大括号丢弃，导致 if 无 body。
            // Phase C：取反分支（!）为失败分支，旧脚本 body 是 AddTestErrMsgs+return；
            // 从 if 块内自动提取 ErrMsg 消息 → op.Report(msg, Error) + pass = false，保持"失败即终止"语义。
            // 非取反分支为成功展示，body 无法重建，保留空 body + 简洁注释。
            // 含 out Type VarName 的，先发射变量声明避免后续引用 CS0103（语义丢失由 G1 out 规则接受）。
            m = P21IfCallPattern.Match(line);
            if (m.Success)
            {
                var neg = m.Groups[1].Value == "!";
                var method = m.Groups[2].Value;
                var args = m.Groups[3].Value.Trim();
                EmitOutVarDeclarations(args, varTypes, declaredNonCondVars, lines, todos, line);
                var translatedArgs = TranslateArgs(args);
                var call = $"await op.Dut.QueryBooleanAsync(\"{method}\", {translatedArgs}, ct)";
                if (neg)
                {
                    var errMsg = ExtractBlockErrorMessage(body, i);
                    var reportMsg = errMsg ?? $"{method} 调用失败";
                    lines.Add($"if (!({call})) {{ op.Report(\"{EscapeCSharp(reportMsg)}\", RealtimeLevel.Error); pass = false; }}");
                }
                else
                {
                    // 旧脚本成功分支多为展示语句，部分为 retry 循环中的 break/continue（G8 重试逻辑待人工迁移）
                    lines.Add($"if (({call})) {{ /* 旧脚本成功分支（展示/控制流）已省略 */ }}");
                }
                continue;
            }

            // G1: Type var = item.GetDevice("P21").Method(args) → 按类型推断 Query 方法
            m = P21VarCallPattern.Match(line);
            if (m.Success)
            {
                var type = m.Groups[1].Value;
                var varName = m.Groups[2].Value;
                var method = m.Groups[3].Value;
                var args = m.Groups[4].Value.Trim();
                EmitOutVarDeclarations(args, varTypes, declaredNonCondVars, lines, todos, line);
                var translatedArgs = TranslateArgs(args);
                var queryMethod = InferQueryMethod(type);
                if (queryMethod != null)
                {
                    // 同名变量已在前面声明（out 预声明等）→ 改为赋值避免 CS0128
                    var varPrefix2 = declaredNonCondVars.Contains(varName) ? "" : "var ";
                    lines.Add($"{varPrefix2}{varName} = await op.Dut.{queryMethod}(\"{method}\", {translatedArgs}, ct);");
                    if (!declaredNonCondVars.Contains(varName)) declaredNonCondVars.Add(varName);
                }
                else
                {
                    lines.Add($"// TODO(自动转换-G1type): {line}");
                    todos.Add($"G1 返回类型 {type} 无法推断 Query 方法：`{line}`");
                }
                continue;
            }

            // G1: item.GetDevice("P21").Method(args); 独立调用
            m = P21StandaloneCallPattern.Match(line);
            if (m.Success)
            {
                var method = m.Groups[1].Value;
                var args = m.Groups[2].Value.Trim();
                // 有 out 参数 → 按变量类型推断
                if (args.Contains("out ", StringComparison.Ordinal))
                {
                    var outMatch = OutVarPattern.Match(args);
                    if (outMatch.Success)
                    {
                        var outVar = outMatch.Groups[1].Value;
                        var outType = varTypes.TryGetValue(outVar, out var t) ? t : "";
                        var queryMethod = InferQueryMethod(outType);
                        if (queryMethod != null)
                        {
                            var remainingArgs = RemoveOutArg(args, outVar);
                            var translatedArgs2 = TranslateArgs(remainingArgs);
                            // 同名变量已由声明/out 预声明存在 → 改为赋值避免 CS0128
                            var varPrefixOut = declaredNonCondVars.Contains(outVar) ? "" : "var ";
                            lines.Add($"{varPrefixOut}{outVar} = await op.Dut.{queryMethod}(\"{method}\", {translatedArgs2}, ct);");
                            if (!declaredNonCondVars.Contains(outVar)) declaredNonCondVars.Add(outVar);
                            continue;
                        }
                        // 类型未知但变量名已知 → 仍发射 default 声明避免后续 CS0103
                        EmitOutVarDeclarations(args, varTypes, declaredNonCondVars, lines, todos, line);
                        var remainingArgs2 = RemoveOutArg(args, outVar);
                        var translatedArgs3 = TranslateArgs(remainingArgs2);
                        lines.Add($"await op.Dut.CommandAsync(\"{method}\", {translatedArgs3}, ct);");
                        continue;
                    }
                    lines.Add($"// TODO(自动转换-G1out): {line}");
                    todos.Add($"G1 out 参数类型无法推断：`{line}`");
                    continue;
                }
                var translatedArgs4 = TranslateArgs(args);
                lines.Add($"await op.Dut.CommandAsync(\"{method}\", {translatedArgs4}, ct);");
                continue;
            }

            // G6: 条件绑定（带注释）→ op.Cond("注释名")
            m = ConditionWithCommentPattern.Match(line);
            if (m.Success)
            {
                var condName = m.Groups[4].Value.Trim();
                var varName = m.Groups[1].Value;
                // 同一变量名重复绑定时省略 var，避免 CS0128 重复声明
                var prefix = declaredCondVars.Contains(varName) ? "" : "var ";
                lines.Add($"{prefix}{varName} = op.Cond(\"{condName}\");");
                condVars[varName] = condName;
                declaredCondVars.Add(varName);
                continue;
            }

            // G6: 条件绑定（无注释）→ 回退位置 + TODO
            m = ConditionNoCommentPattern.Match(line);
            if (m.Success)
            {
                var varName = m.Groups[1].Value;
                var ci = int.Parse(m.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
                var prefix = declaredCondVars.Contains(varName) ? "" : "var ";
                lines.Add($"{prefix}{varName} = ctx.Conditions[{ci}]; // TODO(自动转换-G6): 人工核对条件名");
                todos.Add($"G6 条件索引 {ci} 无行尾注释，按位置回退，需人工核对名称");
                declaredCondVars.Add(varName);
                continue;
            }

            // G7: Task.Delay(N).Wait() → await Task.Delay(N, ct)
            m = TaskDelayWaitPattern.Match(line);
            if (m.Success) { lines.Add($"await Task.Delay({m.Groups[1].Value}, ct);"); continue; }

            // G9: RealTimeWatch → Stopwatch
            m = RealTimeWatchPattern.Match(line);
            if (m.Success) { lines.Add($"var {m.Groups[1].Value} = System.Diagnostics.Stopwatch.StartNew();"); continue; }

            // G9: new TimeSpan() → TimeSpan.Zero
            if (line.Contains("new TimeSpan()", StringComparison.Ordinal))
            {
                lines.Add(line.Replace("new TimeSpan()", "TimeSpan.Zero"));
                continue;
            }

            // G9+G15+G10: DateTime/TimeSpan/Regex/Match/实体 record/List<T> 声明 → 原样保留 + 记录类型
            // 应用 ApplyEntityReplacements 把 PressureUnit.kPa → "kPa"、data.Name → "" 等，避免 CS0103。
            // 替换后仍引用旧框架/旧类型的声明转为 TODO。
            // 同一变量重复声明时（如多次 TimeSpan firstSpan = ...）省略类型前缀，改为赋值避免 CS0128。
            if (IsKeepAsIsDeclaration(line))
            {
                var replacedLine = ApplyEntityReplacements(line);
                if (ReferencesLegacyFramework(replacedLine) || ReferencesLegacyTypes(replacedLine))
                {
                    lines.Add($"// TODO(自动转换-G10): {line}");
                    todos.Add($"G10 实体声明引用旧框架/旧类型：`{line}`");
                    // 仍记录变量名和类型，避免后续 CS0128/CS0103
                    RecordVarType(line, varTypes);
                    var vmLegacy = VarDeclPattern.Match(line);
                    if (vmLegacy.Success) declaredNonCondVars.Add(vmLegacy.Groups[2].Value);
                    continue;
                }
                RecordVarType(line, varTypes);
                var outLine = replacedLine;
                // 检测重复声明：varName 已声明 → 去掉类型前缀，改为赋值
                var vm = VarDeclPattern.Match(line);
                if (vm.Success)
                {
                    var varName = vm.Groups[2].Value;
                    if (declaredNonCondVars.Contains(varName))
                    {
                        var eqIdx = outLine.IndexOf('=');
                        if (eqIdx > 0)
                            outLine = $"{varName} = {outLine.Substring(eqIdx + 1).Trim()}";
                    }
                    else
                    {
                        declaredNonCondVars.Add(varName);
                    }
                }
                lines.Add(outLine);
                continue;
            }

            // G8: goto label → 单 goto + 确认弹窗场景自动迁移 while(true)，否则保留 TODO
            m = GotoPattern.Match(line);
            if (m.Success)
            {
                var gotoLabel = m.Groups[1].Value;
                if (!retryGotos.TryGetValue(gotoLabel, out var gotoList))
                    retryGotos[gotoLabel] = gotoList = new List<(int, int, int)>();
                gotoList.Add((depthBefore[i], lines.Count, i));
                lines.Add($"// TODO(自动转换-G8): goto {gotoLabel} → RetryHelper 重构");
                continue;
            }

            // G8: retry label: → 记录块入口（供 while(true) 迁移），跳过
            m = RetryLabelPattern.Match(line);
            if (m.Success)
            {
                var retryLabel = m.Groups[1].Value;
                if (!retryLabels.ContainsKey(retryLabel))
                    retryLabels[retryLabel] = (depthBefore[i], lines.Count, i);
                continue;
            }

            if (line == "var DC = GetDC(item as AutoTestItem);") continue;
            if (line.StartsWith("ScriptHelper.SetDisplayer(", StringComparison.Ordinal)) continue;
            if (line.StartsWith("return ScriptHelper.WatchAndProcessIntergrade", StringComparison.Ordinal)) continue;
            if (line == ");") continue;
            if (line == "if (!res)") { lines.Add(line); continue; }
            if (line == "{") { lines.Add("{"); continue; }
            if (line == "}") { lines.Add("}"); continue; }
            if (line == "};") continue;

            // 含 Convert/Math/Parse 的类型声明 → 业务逻辑，保留（避免后续 G1 翻译引用未声明变量）
            // 同一变量重复声明时（嵌套块平铺到同一作用域）去掉类型前缀改为赋值，避免 CS0128。
            if (IsBusinessLogicDeclaration(line))
            {
                RecordVarType(line, varTypes);
                var outLineBiz = line;
                var vmBiz = VarDeclPattern.Match(line);
                if (vmBiz.Success)
                {
                    var varNameBiz = vmBiz.Groups[2].Value;
                    if (declaredNonCondVars.Contains(varNameBiz))
                    {
                        var eqIdxBiz = outLineBiz.IndexOf('=');
                        if (eqIdxBiz > 0)
                            outLineBiz = $"{varNameBiz} = {outLineBiz.Substring(eqIdxBiz + 1).Trim()}";
                    }
                    else
                    {
                        declaredNonCondVars.Add(varNameBiz);
                    }
                }
                lines.Add(outLineBiz);
                continue;
            }

            // 简单基本类型声明（double X = double.MaxValue; / int X = 0; 等）不引用旧框架/旧类型 → 保留
            // 避免被 IsLegacyPresentationOrResultLine 跳过导致后续引用 CS0103
            if (IsSimpleBasicTypeDeclaration(line))
            {
                RecordVarType(line, varTypes);
                var outLine2 = ApplyEntityReplacements(line);
                var vm3 = VarDeclPattern.Match(line);
                if (vm3.Success)
                {
                    var varName3 = vm3.Groups[2].Value;
                    if (declaredNonCondVars.Contains(varName3))
                    {
                        var eqIdx2 = outLine2.IndexOf('=');
                        if (eqIdx2 > 0)
                            outLine2 = $"{varName3} = {outLine2.Substring(eqIdx2 + 1).Trim()}";
                    }
                    else
                    {
                        declaredNonCondVars.Add(varName3);
                    }
                }
                lines.Add(outLine2);
                continue;
            }

            // 旧平台的 WPF/结果包装语句不属于设备业务调用，在新引擎中没有对应对象，
            // 直接跳过即可；设备调用和未知辅助方法仍保留 TODO，便于后续按报告补齐。
            if (IsLegacyPresentationOrResultLine(line))
            {
                // 控制语句可能把大括号写在同一行；虽然条件本身被跳过，
                // 仍需保留结构大括号，保证生成源码语法平衡。
                if (IsControlLine(line))
                {
                    foreach (var _ in line.Where(ch => ch == '{')) lines.Add("{");
                    foreach (var _ in line.Where(ch => ch == '}')) lines.Add("}");
                }
                continue;
            }

            // G9: 含 DateTime.Now/DateTime.UtcNow 的行（未被上方跳过的）→ 直接搬
            // 覆盖 timeSpan = DateTime.Now - start; / var dtgap = (ReadDT - DateTime.Now).TotalSeconds; 等
            // 引用旧框架变量的（item./result./rData/data./msg.）转为 TODO，避免 CS0103
            if (line.Contains("DateTime.Now", StringComparison.Ordinal) ||
                line.Contains("DateTime.UtcNow", StringComparison.Ordinal))
            {
                if (ReferencesLegacyFramework(line))
                {
                    lines.Add($"// TODO(自动转换-G9): {line}");
                    todos.Add($"G9 timing 行引用旧框架变量：`{line}`");
                }
                else
                {
                    lines.Add(line);
                }
                continue;
            }

            // 无法识别：TODO 标注
            lines.Add($"// TODO(自动转换): {line}");
            todos.Add($"脚本：无法自动映射语句 `{line}`");
        }

        // G8: goto 重试循环 → while(true) 自动迁移（单 goto + 标签与 goto 间有确认弹窗 + 块边界安全时）
        ApplyG8RetryMigration(lines, todos, body, depthBefore, retryLabels, retryGotos, confirmFlatIndices);

        // G11: 末尾一次性发射 RecordProcessData（每个 ListValueData 一条曲线）
        // 注意：{ 必须与 ctx.RecordProcessData 同行，避免 TestStepSourceGenerator 把裸 {/} 当作残留控制块大括号丢弃。
        foreach (var kv in listValueSeries)
        {
            var (curveName, points) = kv.Value;
            if (points.Count == 0) continue;
            lines.Add($"ctx.RecordProcessData(new ProcessDataSeries {{");
            lines.Add($"    StartedAt = DateTime.Now,");
            lines.Add($"    TimeSec = Enumerable.Range(0, {points.Count}).Select(i => (double)i).ToArray(),");
            lines.Add($"    Channels = new[] {{ new ProcessChannel(\"{curveName}\", {kv.Key}.ToArray()) }}");
            lines.Add("});");
        }

        // B4: 遗留变量 fallback 预声明——声明被跳过的遗留变量若在本方法体被实际引用，
        // 则预声明为可编译的 fallback（插到方法体最前），避免后续引用 CS0103。
        var fallbackLines = new List<string>();
        foreach (var kv in LegacyFallbackVars)
        {
            if (!IsVarReferencedInBody(body, kv.Key)) continue;
            fallbackLines.Add($"// G10 遗留变量 {kv.Key}：原始声明引用旧框架/旧类型未迁移，以下为可编译占位");
            fallbackLines.Add(kv.Value);
            todos.Add($"B4 遗留变量 {kv.Key} fallback 预声明：{kv.Value}（原始声明引用旧框架/旧类型或泛型 out 未被匹配）");
        }
        if (fallbackLines.Count > 0)
            lines.InsertRange(0, fallbackLines);

        // B5: CS1023 修复——裸控制语句（if/while/for 等无 {）后紧跟声明行时，
        // 该声明会被 C# 当作嵌入语句报 CS1023。给控制语句补空 body（{ }），
        // 让声明留在外层作用域（原 if 体通常已被 TODO 化，语义可接受）。
        // 注意：TestStepSourceGenerator 会丢弃裸 { / } 行，因此查找下一行时需跳过
        // 裸大括号；同时控制语句若本身已含 { }（行内 body）则无需处理。
        for (var i = 0; i < lines.Count - 1; i++)
        {
            var cur = lines[i].TrimEnd();
            if (!IsControlLine(cur)) continue;
            if (cur.Contains("{", StringComparison.Ordinal) ||
                cur.Contains("}", StringComparison.Ordinal)) continue; // 已有 body（行内 { }）
            // 跳过紧随的注释/空行/裸大括号，看下一个非注释非大括号行是否为声明
            var j = i + 1;
            while (j < lines.Count &&
                   (string.IsNullOrWhiteSpace(lines[j]) ||
                    lines[j].TrimStart().StartsWith("//", StringComparison.Ordinal) ||
                    lines[j].Trim() is "{" or "}"))
                j++;
            if (j < lines.Count && IsDeclarationStatement(lines[j]))
                lines[i] = cur + " { }";
        }

        return (lines, todos);
    }

    /// <summary>统计一行内净花括号数（{ 数 - } 数）；字符串插值 {..} 成对平衡不影响净计数。</summary>
    private static int CountNetBraces(string line)
    {
        var net = 0;
        foreach (var ch in line)
        {
            if (ch == '{') net++;
            else if (ch == '}') net--;
        }
        return net;
    }

    /// <summary>
    /// G8: 把「goto tryagain + OpenInfoConfirmWindow 重试确认」自动迁移为 while(true) 重试循环，
    /// 替代旧脚本的 goto 跳转（<see cref="RetryHelper"/> 的块级前提，此处用 while(true) 包裹）。
    /// 仅在结构可安全重建时执行：
    ///   1) 该标签只有一处 goto；
    ///   2) goto 所在块深度深于标签深度（排除平铺 goto，如 TestSwitch 的 trySW）；
    ///   3) 标签与 goto 之间出现过确认弹窗（排除计数器式重试，如 trynum++ &lt; 3 无确认）；
    ///   4) goto 之后块闭合回标签深度（块边界安全）。
    /// 映射：tryagain 标签 → <c>while (true)</c>；goto → <c>continue</c>；取消确认 → <c>pass=false; break</c>；
    /// 每次重试重置 pass，并用 prevPassN 保留标签之前的整体结果（多测试段时避免相互覆盖）。
    /// 不满足条件的 goto 保留 TODO(自动转换-G8)，由人工用 RetryHelper.RetryAsync 迁移。
    /// </summary>
    private static void ApplyG8RetryMigration(
        List<string> lines,
        List<string> todos,
        IReadOnlyList<string> body,
        IReadOnlyList<int> depthBefore,
        IReadOnlyDictionary<string, (int LabelDepth, int OpenIdx, int BodyIdx)> retryLabels,
        IReadOnlyDictionary<string, List<(int GotoDepth, int GotoIdx, int BodyIdx)>> retryGotos,
        IReadOnlyList<int> confirmFlatIndices)
    {
        // 收集可迁移标签
        var plan = new List<(int OpenIdx, int ConfirmIdx, int GotoIdx, string Label)>();
        foreach (var kv in retryGotos)
        {
            var label = kv.Key;
            if (!retryLabels.TryGetValue(label, out var li)) continue;
            if (kv.Value.Count != 1) continue;                    // 仅单 goto
            var (gotoDepth, gotoIdx, gotoBodyIdx) = kv.Value[0];
            if (gotoDepth <= li.LabelDepth) continue;             // 平铺 goto（如 trySW）不迁移
            // 标签与 goto 之间须有确认弹窗（重试确认）
            int? confirmIdx = null;
            for (var c = confirmFlatIndices.Count - 1; c >= 0; c--)
            {
                var ci = confirmFlatIndices[c];
                if (ci > li.OpenIdx && ci < gotoIdx) { confirmIdx = ci; break; }
            }
            if (confirmIdx is null) continue;                     // 计数器式重试不迁移
            // goto 后块边界：直到第一个真实语句只允许裸 } / 注释 / 空行 / #指令，且该语句深度 ≤ 标签深度
            var safe = true;
            var j = gotoBodyIdx + 1;
            for (; j < body.Count; j++)
            {
                var t = body[j].Trim();
                if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) ||
                    t.StartsWith("#", StringComparison.Ordinal)) continue;
                if (t == "}") continue;
                if (depthBefore[j] <= li.LabelDepth) break;       // 块闭合回标签深度 → 安全
                safe = false;
                break;
            }
            if (!safe) continue;
            plan.Add((li.OpenIdx, confirmIdx.Value, gotoIdx, label));
        }
        if (plan.Count == 0) return;

        // 重叠区间保护：嵌套重试块仅迁移最外层
        plan.Sort((a, b) => a.OpenIdx.CompareTo(b.OpenIdx));
        var final = new List<(int OpenIdx, int ConfirmIdx, int GotoIdx, string Label)>();
        foreach (var p in plan)
        {
            if (final.Any(f => f.OpenIdx < p.OpenIdx && f.GotoIdx > p.OpenIdx)) continue;
            final.Add(p);
        }
        if (final.Count == 0) return;

        // 自高 goto 索引向低处理，避免插入位移干扰（非重叠块下索引保持原坐标）
        final.Sort((a, b) => b.GotoIdx.CompareTo(a.GotoIdx));
        var prevPassCounter = 0;
        foreach (var (openIdx, confirmIdx, gotoIdx, label) in final)
        {
            // 1) 重写确认弹窗为「取消 → break」
            var confirmMatch = RetryConfirmPattern.Match(lines[confirmIdx]);
            if (!confirmMatch.Success) continue;
            lines[confirmIdx] = confirmMatch.Groups[1].Value + " { pass = false; break; }  // G8: 取消重试 → 退出循环";
            // 2) goto 标记 → continue + 闭合 while + 合并本段结果（缩进由下方循环体缩进统一 +4）
            prevPassCounter++;
            var prevPassName = $"prevPass{prevPassCounter}";
            lines[gotoIdx] = $"continue;  // G8: 原 goto {label} → 重新测试";
            lines.Insert(gotoIdx + 1, "}  // G8: while(true) 重试循环结束（原 goto " + label + " 标签）");
            lines.Insert(gotoIdx + 2, $"pass &= {prevPassName};  // G8: 合并本段结果到整体结果");
            // 3) 循环体整体缩进 + 在标签处插入 while(true) 头（{ 与 while 同行，避免被下游丢弃裸大括号）
            for (var k = openIdx; k <= gotoIdx; k++)
                if (lines[k].Length > 0) lines[k] = "    " + lines[k];
            lines.Insert(openIdx, $"var {prevPassName} = pass;  // G8: 记录本重试段之前的整体结果");
            lines.Insert(openIdx + 1, $"while (true) {{  // G8: 原 goto 标签 {label} → while(true) 重试循环");
            lines.Insert(openIdx + 2, "    pass = true;  // G8: 每次重试重置本段结果");
        }

        // 未被迁移的 goto 保留 TODO 项
        foreach (var kv in retryGotos)
        {
            var label = kv.Key;
            var migrated = final.Any(f => f.Label == label);
            if (migrated) continue;
            foreach (var (_, _, gotoBodyIdx) in kv.Value)
            {
                var t = body[gotoBodyIdx].Trim();
                todos.Add($"G8 goto {label}：{t} 未自动迁移，需人工用 RetryHelper.RetryAsync 重构（多 goto/计数器式/块边界复杂）");
            }
        }
    }

    /// <summary>B5: 判断是否为声明语句行（基本类型/var/数组声明）。</summary>
    private static bool IsDeclarationStatement(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("double ", StringComparison.Ordinal)
            || t.StartsWith("int ", StringComparison.Ordinal)
            || t.StartsWith("var ", StringComparison.Ordinal)
            || t.StartsWith("string ", StringComparison.Ordinal)
            || t.StartsWith("bool ", StringComparison.Ordinal)
            || t.StartsWith("double[] ", StringComparison.Ordinal)
            || t.StartsWith("int[] ", StringComparison.Ordinal);
    }

    /// <summary>
    /// B1: 翻译条件变量表达式（在模式匹配之前执行，避免被其他规则吞掉）：
    ///   - condVar.IsTrue(valueExpr) → op.Judge("条件名", valueExpr, "变量名", "单位")
    ///     （ConditionDescriptor 无 IsTrue 方法；op.Judge 返回 bool，与 IsTrue 语义一致）
    ///   - condVar.Value → double.Parse(condVar.Expected ?? "0")
    ///     （ConditionDescriptor 无 Value 属性，期望值在 Expected(string?) 中；仅对条件变量生效，
    ///     避免误伤 Pressure/ElectricMeasure 等实体的 .Value）
    /// </summary>
    private static string TranslateCondExpressions(
        string line,
        IReadOnlyDictionary<string, string> condVars,
        IReadOnlyDictionary<string, string> unitByName)
    {
        if (!line.Contains(".IsTrue(", StringComparison.Ordinal) &&
            !line.Contains(".Value", StringComparison.Ordinal))
            return line;

        // condVar.IsTrue(valueExpr) → op.Judge("condName", valueExpr, "condVar", "unit")
        if (line.Contains(".IsTrue(", StringComparison.Ordinal))
        {
            line = ConditionIsTruePattern.Replace(line, mm =>
            {
                var varName = mm.Groups[1].Value;
                var valueExpr = mm.Groups[2].Value.Trim();
                var condName = condVars.TryGetValue(varName, out var cn) && cn.Length > 0 ? cn : varName;
                var unit = unitByName.TryGetValue(condName, out var u) ? u : "";
                return $"op.Judge(\"{EscapeCSharp(condName)}\", {valueExpr}, \"{EscapeCSharp(varName)}\", \"{EscapeCSharp(unit)}\")";
            });
        }

        // condVar.Value → double.Parse(condVar.Expected ?? "0")（仅条件变量）
        if (condVars.Count > 0)
        {
            foreach (var kv in condVars)
            {
                line = System.Text.RegularExpressions.Regex.Replace(
                    line, $@"\b{Regex.Escape(kv.Key)}\.Value\b",
                    $@"double.Parse({kv.Key}.Expected ?? ""0"")");
            }
        }
        return line;
    }

    /// <summary>
    /// B4: 声明被旧框架/旧类型跳过的遗留变量 → 可编译 fallback 声明。
    /// Key = 变量名（用于占位去重），Value = 完整声明语句。
    /// 仅当方法体实际引用该变量时才发射（避免产生未使用变量）。
    /// </summary>
    private static readonly Dictionary<string, string> LegacyFallbackVars = new(StringComparer.Ordinal)
    {
        ["address"] = "int address = 0; // 旧声明 `int address = int.Parse(result.Data.Value...);` 引用旧框架",
        ["massage"] = "var massage = new List<(string Address, string Name)>(); // 旧声明 `List<PAMassage> massage = ...` 类型未迁移",
        ["tvalue"] = "var tvalue = new System.Text.StringBuilder(); // 旧声明 `StringBuilder tvalue = ...` 未迁移",
        ["valueDataCP"] = "Pressure valueDataCP = new Pressure(0, \"kPa\"); // 旧声明 `ValueData valueDataCP = ...` 未迁移",
        ["MainBoardCheckStata"] = "int MainBoardCheckStata = 0; // 旧声明 `CheckState MainBoardCheckStata = ...` 枚举未迁移",
        ["msg"] = "string msg = \"\"; // 旧声明 `RealTimeMsg msg = ...` 未迁移，msg.Content→msg",
        ["ModulePressure"] = "PressureRange ModulePressure = new PressureRange(0, 0, \"kPa\"); // 条件/实体声明未迁移",
    };

    /// <summary>
    /// B4: 判断方法体是否实际引用了指定变量（排除其自身声明/赋值行与注释行）。
    /// 用于决定是否发射 fallback 声明，避免产生"未使用变量"警告。
    /// </summary>
    private static bool IsVarReferencedInBody(List<string> body, string varName)
    {
        var refPattern = new Regex($@"\b{Regex.Escape(varName)}\b", RegexOptions.Compiled);
        var declPattern = new Regex($@"\b{Regex.Escape(varName)}\s*=", RegexOptions.Compiled);
        foreach (var raw in body)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (!refPattern.IsMatch(line)) continue;
            // 该行是 varName 的声明/赋值（含 =），视为声明而非使用
            if (declPattern.IsMatch(line)) continue;
            return true;
        }
        return false;
    }

    private static bool IsLegacyPresentationOrResultLine(string line)
    {
        if (line.Contains("GetDevice(\"", StringComparison.Ordinal)) return false;
        // G9: 含 DateTime.Now/DateTime.UtcNow 的行为合法 timing 代码，不应被跳过
        // （var dtgap = (ReadDT - DateTime.Now).TotalSeconds; 等）
        if (line.Contains("DateTime.Now", StringComparison.Ordinal) ||
            line.Contains("DateTime.UtcNow", StringComparison.Ordinal))
            return false;
        string[] prefixes =
        [
            "Result<", "List<", "ValueData ", "ValueParameter ", "TextParameter ", "TextData ",
            "DataBase ", "RealTimeMsg ", "ErrMsg ", "ScriptHelper.", "item.", "testItem.",
            "rData", "result", "msg", "watch", "Stopwatch", "ValueCondition ", "RangeCondition ",
            "#region", "#endregion", "return result", "return ScriptHelper", "try", "catch", "finally",
            "else", "for (", "foreach (", "while (", "if (", "switch (", "case ", "break;", "continue;",
            "throw ", "bool ", "string ", "double ", "int ", "var ", "parameter", "currentDataCollector",
            "dataArray", "dataItem", "electricMeasure", "setting", "response", "outmsg"
        ];
        return prefixes.Any(p => line.StartsWith(p, StringComparison.Ordinal));
    }

    private static bool IsControlLine(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("if ", StringComparison.Ordinal) || t.StartsWith("if(", StringComparison.Ordinal)
        || t.StartsWith("else", StringComparison.Ordinal) || t.StartsWith("for ", StringComparison.Ordinal)
        || t.StartsWith("foreach ", StringComparison.Ordinal) || t.StartsWith("while ", StringComparison.Ordinal)
        || t.StartsWith("try", StringComparison.Ordinal) || t.StartsWith("catch", StringComparison.Ordinal)
        || t.StartsWith("finally", StringComparison.Ordinal) || t.StartsWith("switch ", StringComparison.Ordinal);
    }

    /// <summary>匹配旧脚本 if 失败分支中的错误消息：result.AddTestErrMsgs(new ErrMsg(20003, "消息"))。</summary>
    private static readonly Regex ErrMsgPattern = new(
        @"new\s+ErrMsg\(\s*(?:\d+|\w+)\s*,\s*""([^""]*)""\s*\)", RegexOptions.Compiled);

    /// <summary>
    /// Phase C：从 if 语句随后的块内提取第一条 ErrMsg 消息。
    /// 旧脚本失败分支形如：if (!GetX()) { result.AddTestErrMsgs(new ErrMsg(N, "MSG")); return result; }
    /// 扫描至块结束（"}"）或 return 即停，取到则返回消息文本，取不到返回 null（调用方用通用消息兜底）。
    /// </summary>
    private static string? ExtractBlockErrorMessage(List<string> body, int startIdx)
    {
        for (var i = startIdx + 1; i < body.Count; i++)
        {
            var line = body[i].Trim();
            if (line == "}" || line.StartsWith("return", StringComparison.Ordinal)) break;
            var m = ErrMsgPattern.Match(line);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    internal static List<string> ExtractLegacyCalls(IEnumerable<string> body)
    {
        var result = new List<string>();
        foreach (var line in body)
        {
            foreach (Match m in LegacyDeviceCallPattern.Matches(line))
            {
                var args = m.Groups["args"].Value.Trim();
                // 回放无参数、枚举/数值常量调用；依赖旧上下文变量的表达式不能猜默认值。
                if (args.StartsWith("out ", StringComparison.OrdinalIgnoreCase))
                    args = "";
                if (args.Length > 0 && !IsSimpleLegacyArgument(args))
                    continue;
                var value = $"{m.Groups["device"].Value}|{m.Groups["method"].Value}" + (args.Length > 0 ? $"|{args}" : "");
                if (!result.Contains(value, StringComparer.Ordinal)) result.Add(value);
            }
        }
        return result;
    }

    private static bool IsSimpleLegacyArgument(string args)
    {
        args = args.Trim();
        return args.StartsWith("OpenCloseState.", StringComparison.Ordinal)
            || args.StartsWith("ProgramFunction.", StringComparison.Ordinal)
            || args.StartsWith("PressureModel.", StringComparison.Ordinal)
            || args.StartsWith("PressureUnit.", StringComparison.Ordinal)
            || args.StartsWith("StableModuleType.", StringComparison.Ordinal)
            || args.StartsWith("DevicePressureControlMode.", StringComparison.Ordinal)
            || bool.TryParse(args, out _)
            || double.TryParse(args, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            || (args.StartsWith("\"") && args.EndsWith("\""));
    }

    internal static string EscapeCSharp(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static (string CondName, string Unit) ResolveCond(
        string lowerVar, string upperVar,
        IReadOnlyDictionary<string, string> condVars,
        IReadOnlyDictionary<string, string> unitByName,
        string line, List<string> todos)
    {
        if (condVars.TryGetValue(lowerVar, out var name) && name.Length > 0)
            return (name, unitByName.TryGetValue(name, out var u) ? u : "");
        todos.Add($"脚本 `{line.Trim()}`：无法确定判定条件名（变量 {lowerVar}），转译时以变量名替代");
        return (lowerVar, "");
    }

    // ===== G1-G15 辅助方法 =====

    /// <summary>匹配 out 变量名：out varName</summary>
    private static readonly Regex OutVarPattern = new(@"out\s+(\w+)", RegexOptions.Compiled);

    /// <summary>已知枚举前缀（枚举参数转字符串字面量）</summary>
    private static readonly HashSet<string> KnownEnums = new(StringComparer.Ordinal)
    {
        "ProgramFunction", "OpenCloseState", "BrightnessType", "StableModuleType",
        "DevicePressureControlMode", "PressureModel", "PressureUnit", "ElectricSourceFunction",
        "PressureSwitchTripType", "PowerType", "PressureStableState",
        "PumpTestProcessState", "PumpTestResultState", "CalibrationSensorStateTest",
        "SelfTuningTestType", "ElectricMeasureFunction", "LeakDeviceModel", "LeakPosition"
    };

    /// <summary>
    /// 需原样保留的局部声明类型前缀（G9+G15+G10 实体 record）。
    /// 这些类型在新 Core.Abstractions 中已定义（Pressure/ElectricMeasure 等），
    /// 或为 C# 内置（DateTime/TimeSpan/Regex/Match），保留后通过 ApplyEntityReplacements
    /// 把旧体系 PressureUnit.kPa → "kPa" 等同步替换以避免 CS0103。
    /// </summary>
    private static readonly HashSet<string> KeepAsIsPrefixes = new(StringComparer.Ordinal)
    {
        "DateTime ", "TimeSpan ",
        "Pressure ", "PressureRange ",
        "ElectricMeasure ", "PumpTestState ",
        "IntakeSensorCalibrationData ", "SelfTuningData ",
        "Match ", "Regex ",
    };

    /// <summary>
    /// 判断行是否为 List&lt;T&gt; 类型声明（如 List&lt;double&gt; ... = null; / List&lt;ElectricMeasure&gt; ... = new List&lt;...&gt;();）。
    /// 这些声明应原样保留以避免后续 out VarName 找不到类型。
    /// </summary>
    private static bool IsListDeclaration(string line)
        => line.StartsWith("List<", StringComparison.Ordinal)
        && (line.Contains(" = new List<", StringComparison.Ordinal)
            || line.Contains(" = null;", StringComparison.Ordinal)
            || line.Contains(" = new()", StringComparison.Ordinal));

    /// <summary>转译 P21 调用参数：枚举→字符串字面量，item.Root.DUT.DeviceCode→ctx.SerialNumber，out→移除</summary>
    private static string TranslateArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return "null";
        var parts = SplitTopLevelArgs(args);
        var translated = new List<string>();
        foreach (var p in parts)
        {
            if (p.StartsWith("out ", StringComparison.Ordinal)) continue;
            translated.Add(TranslateSingleArg(p));
        }
        if (translated.Count == 0) return "null";
        return $"new[]{{ {string.Join(", ", translated)} }}";
    }

    private static string TranslateSingleArg(string arg)
    {
        // item.Root.DUT.DeviceCode → ctx.SerialNumber ?? ""
        if (arg.Contains("item.Root.DUT.DeviceCode", StringComparison.Ordinal))
            return "ctx.SerialNumber ?? \"\"";
        // EnumName.Member → "Member"（已知枚举）
        var em = EnumArgPattern.Match(arg);
        if (em.Success && KnownEnums.Contains(em.Groups[1].Value))
            return $"\"{em.Groups[2].Value}\"";
        // 已是字符串字面量 → 原样
        if (arg.StartsWith("\"", StringComparison.Ordinal)) return arg;
        // bool 字面量 → 字符串
        if (arg == "true" || arg == "false") return $"\"{arg}\"";
        // 数值字面量 → 字符串
        if (double.TryParse(arg, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return $"\"{arg}\"";
        // 其他表达式（变量/属性/方法调用）→ .ToString() 确保 args 为 IReadOnlyList<string>
        return $"{arg}.ToString()";
    }

    /// <summary>按返回类型推断 IConST811ADut Query 方法。仅处理基本类型，自定义类型返回 null 留 TODO。</summary>
    private static string? InferQueryMethod(string type)
    {
        return type switch
        {
            "string" or "String" => "QueryTextAsync",
            "double" or "Double" => "QueryDoubleAsync",
            "bool" or "Boolean" => "QueryBooleanAsync",
            _ => null // 枚举/自定义类型 → 留 TODO，G10 实体包装后续处理
        };
    }

    /// <summary>从参数列表中移除 out 变量声明</summary>
    private static string RemoveOutArg(string args, string outVar)
    {
        var parts = SplitTopLevelArgs(args);
        var remaining = parts.Where(p => p.Trim() != $"out {outVar}").ToList();
        return string.Join(", ", remaining);
    }

    /// <summary>记录局部变量类型（用于 G1 out 参数推断）</summary>
    private static void RecordVarType(string line, Dictionary<string, string> varTypes)
    {
        var m = VarDeclPattern.Match(line);
        if (m.Success) varTypes[m.Groups[2].Value] = m.Groups[1].Value;
    }

    private static readonly Regex VarDeclPattern = new(
        @"^(\w+(?:<[^>]+>)?)\s+(\w+)\s*[=;]", RegexOptions.Compiled);

    /// <summary>
    /// 旧体系遗留类型（未迁移到新 Core.Abstractions），保留声明会导致 CS0246。
    /// 遇到这些类型的声明应转为 TODO，但仍记录变量名避免后续 CS0128/CS0103。
    /// 注意：PressureUnit/OpenCloseState 等枚举值（如 PressureUnit.kPa）由 ApplyEntityReplacements
    /// 翻译为字符串字面量，不在此列；只有作为类型声明时才视为遗留。
    /// </summary>
    private static readonly HashSet<string> LegacyTypes = new(StringComparer.Ordinal)
    {
        "PAMassage", "CheckState", "PowerType", "ProgramFunctionCheckResult",
        "PressureStableState",
        "RealTimeWatch", "TimeRange", "ErrMsg", "RealTimeMsg",
        "ScriptHelperKVP", "ListValueData", "TextData", "ValueCondition", "RangeCondition",
        "ValueData", "ValueParameter", "TextParameter", "DataBase",
    };

    /// <summary>判断行是否引用旧体系类型（PAMassage/CheckState/PowerType 等）。</summary>
    private static bool ReferencesLegacyTypes(string line)
    {
        foreach (var t in LegacyTypes)
            if (line.Contains(t, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>判断行是否为需原样保留的声明（DateTime/TimeSpan/Regex/Match/G10 实体 record/List&lt;T&gt;）</summary>
    private static bool IsKeepAsIsDeclaration(string line)
        => KeepAsIsPrefixes.Any(p => line.StartsWith(p, StringComparison.Ordinal)) || IsListDeclaration(line);

    /// <summary>
    /// G10 单位语义替换：旧体系 PressureUnit 枚举 → 新 Pressure record 的 string Unit 参数。
    /// 同步处理 LeakFormula 替换、无参构造函数补全、ToUnit 调用扁平化，
    /// 使保留的实体声明与 rData 赋值能直接通过编译。
    /// 注意：PressureUnit.kPa → "kPa" 必须在 ToUnit 扁平化之前执行，
    /// 否则 ToUnit(PressureUnit.kPa).Value 因无字符串字面量而无法被 ToUnit 正则匹配。
    /// </summary>
    private static string ApplyEntityReplacements(string value)
    {
        value = ApplyLeakFormulaReplacements(value);
        // PressureUnit 枚举 → 字符串字面量（必须先于 ToUnit 扁平化）
        value = value.Replace("PressureUnit.kPa", "\"kPa\"", StringComparison.Ordinal);
        value = value.Replace("PressureUnit.MPa", "\"MPa\"", StringComparison.Ordinal);
        value = value.Replace("PressureUnit.Pa", "\"Pa\"", StringComparison.Ordinal);
        value = value.Replace("PressureUnit.bar", "\"bar\"", StringComparison.Ordinal);
        value = value.Replace("PressureUnit.mbar", "\"mbar\"", StringComparison.Ordinal);
        // PressureUnit.Parse(X) → X（unit 在新体系已是 string）
        value = System.Text.RegularExpressions.Regex.Replace(
            value, @"PressureUnit\.Parse\(([^)]+)\)", "$1");
        // 无参构造函数补全：旧体系 ElectricMeasure/Pressure/SelfTuningData 有无参构造，新体系 record 必填参数
        value = value.Replace("new ElectricMeasure()", "new ElectricMeasure(0, \"\", ElectricMeasureFunction.None)", StringComparison.Ordinal);
        value = value.Replace("new Pressure()", "new Pressure(0, \"kPa\")", StringComparison.Ordinal);
        value = value.Replace("new PressureRange()", "new PressureRange(0, 0, \"kPa\")", StringComparison.Ordinal);
        value = value.Replace("new SelfTuningData()", "new SelfTuningData(SelfTuningTestType.Unknown, 0, 0, 0, 0)", StringComparison.Ordinal);
        value = value.Replace("new IntakeSensorCalibrationData()", "new IntakeSensorCalibrationData(CalibrationSensorStateTest.UnKnown, 0)", StringComparison.Ordinal);
        value = value.Replace("new PumpTestState()", "new PumpTestState(PumpTestProcessState.UnKnown, PumpTestResultState.UnKnown, PumpTestResultState.UnKnown, 0, PumpTestResultState.UnKnown, PumpTestResultState.UnKnown, 0)", StringComparison.Ordinal);
        // ToUnit 扁平化：Pressure.ToUnit("kPa").Value → Pressure.Value（新 Pressure 已有 Unit）
        // 此时 PressureUnit.kPa 已变为 "kPa"，正则可正确匹配
        value = System.Text.RegularExpressions.Regex.Replace(
            value, @"(\w+)\.ToUnit\(""[^""]*""\)\.Value", "$1.Value");
        value = System.Text.RegularExpressions.Regex.Replace(
            value, @"(\w+)\.ToUnit\(""[^""]*""\)", "$1");
        // 剥离链式 msgXXX.Content = 前缀（chained 赋值中只保留最终值）
        value = System.Text.RegularExpressions.Regex.Replace(
            value, @"msg\w+\.Content\s*=\s*", "");
        // B4: msg.Content（无数字后缀）→ msg（msg 已 fallback 为 string，避免 CS0103/CS1061）
        value = value.Replace("msg.Content", "msg", StringComparison.Ordinal);
        // data.Name → ""（旧 TextData.Name 在新体系无对应，用空字面量占位）
        value = value.Replace("data.Name", "\"\"", StringComparison.Ordinal);
        return value;
    }

    /// <summary>
    /// 判断行是否引用旧框架变量（item./testItem./result./rData/msgN.Content/data.Name），
    /// 这些变量在新体系中不存在，保留会导致 CS0103。
    /// 同时捕获裸 item 引用（如 TestSelfTuningMH(item)），避免 CS0103。
    /// </summary>
    private static bool ReferencesLegacyFramework(string line)
    {
        if (line.Contains("item.", StringComparison.Ordinal)) return true;
        if (line.Contains("testItem.", StringComparison.Ordinal)) return true;
        if (line.Contains("result.", StringComparison.Ordinal)) return true;
        if (line.Contains("rData", StringComparison.Ordinal)) return true;
        if (line.Contains("data.Name", StringComparison.Ordinal)) return true;
        if (line.Contains("msg.", StringComparison.Ordinal)) return true;
        // 裸 item 引用（如 TestSelfTuningMH(item)、return item 等），用 \b 词边界匹配
        if (BareItemReferencePattern.IsMatch(line)) return true;
        // msgN.Content / msgN.Name 引用（N 为数字）
        if (MsgFieldPattern.IsMatch(line)) return true;
        return false;
    }

    /// <summary>匹配裸 item 引用（item 作为独立标识符，不跟随 . 也不在标识符内部）。</summary>
    private static readonly Regex BareItemReferencePattern = new(
        @"\bitem\b(?!\.)", RegexOptions.Compiled);

    /// <summary>匹配 msgN.Content / msgN.Name 引用（N 为数字）。</summary>
    private static readonly Regex MsgFieldPattern = new(
        @"msg\d+\.(Content|Name)", RegexOptions.Compiled);

    /// <summary>
    /// 翻译消息字符串中的旧框架引用：
    /// - item.Root.DUT.DeviceCode → ctx.SerialNumber ?? ""
    /// - item.Root.DUT.DeviceMode 等其他 item.* → TODO 占位符（避免 CS0103）
    /// - msgN.Content / msgN.Name → 空字符串（旧 RealTimeMsg 字段在新体系无对应）
    /// </summary>
    private static string TranslateLegacyReferencesInMsg(string msg)
    {
        // item.Root.DUT.DeviceCode → ctx.SerialNumber ?? ""
        msg = msg.Replace("item.Root.DUT.DeviceCode", "(ctx.SerialNumber ?? \"\")", StringComparison.Ordinal);
        // 其他 item.* 引用 → 替换为占位 TODO 字符串（避免 CS0103）
        msg = System.Text.RegularExpressions.Regex.Replace(
            msg, @"item\.Root\.DUT\.\w+", "\"TODO\"", RegexOptions.Compiled);
        msg = msg.Replace("item.Parent.VM.", "", StringComparison.Ordinal);
        // msgN.Content / msgN.Name → 空字符串（旧 RealTimeMsg 字段在新体系无对应）
        msg = System.Text.RegularExpressions.Regex.Replace(
            msg, @"msg\d+\.(Content|Name)", "\"\"", RegexOptions.Compiled);
        // B4: msg.Content（无数字后缀）→ msg（msg 已 fallback 为 string，避免 CS0103/CS1061）
        msg = msg.Replace("msg.Content", "msg", StringComparison.Ordinal);
        return msg;
    }

    /// <summary>按顶层逗号分割参数列表（尊重字符串字面量和括号嵌套）</summary>
    private static List<string> SplitTopLevelArgs(string args)
    {
        var result = new List<string>();
        var current = "";
        var inString = false;
        var escape = false;
        var depth = 0;
        foreach (var ch in args)
        {
            if (escape) { current += ch; escape = false; continue; }
            if (ch == '\\' && inString) { current += ch; escape = true; continue; }
            if (ch == '"') { inString = !inString; current += ch; continue; }
            if (!inString)
            {
                if (ch == '(') { depth++; current += ch; continue; }
                if (ch == ')') { depth--; current += ch; continue; }
                if (ch == ',' && depth == 0)
                {
                    result.Add(current.Trim());
                    current = "";
                    continue;
                }
            }
            current += ch;
        }
        if (current.Length > 0) result.Add(current.Trim());
        return result;
    }

    // ===== 多行语句预合并辅助 =====

    /// <summary>预合并多行语句：当累计行有未闭合的括号或为裸控制关键字时，持续向下合并。</summary>
    private static List<string> MergeContinuationLines(List<string> body)
    {
        var result = new List<string>(body.Count);
        var i = 0;
        while (i < body.Count)
        {
            var line = body[i].Trim();
            // 空行和注释行不参与合并
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                result.Add(body[i]);
                i++;
                continue;
            }
            var merged = line;
            var depth = ComputeParenDepth(merged);
            // 括号未闭合 或 裸控制关键字（if/else/for/while/...）→ 继续合并下一行
            while (i + 1 < body.Count && (depth > 0 || IsBareControlKeyword(merged)))
            {
                i++;
                merged += " " + body[i].Trim();
                depth = ComputeParenDepth(merged);
            }
            result.Add(merged);
            i++;
        }
        return result;
    }

    /// <summary>计算括号深度（尊重字符串字面量），&gt;0 表示有未闭合的左括号。</summary>
    private static int ComputeParenDepth(string line)
    {
        var depth = 0;
        var inString = false;
        var escape = false;
        foreach (var ch in line)
        {
            if (escape) { escape = false; continue; }
            if (ch == '\\' && inString) { escape = true; continue; }
            if (ch == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
        }
        return depth;
    }

    /// <summary>判断是否为裸控制关键字（无括号的 if/else/for/while/switch/try/catch/finally/do/else if）。</summary>
    private static bool IsBareControlKeyword(string line)
        => line == "if" || line == "else" || line == "for" || line == "while"
        || line == "switch" || line == "try" || line == "catch" || line == "finally"
        || line == "do" || line == "else if";

    /// <summary>
    /// 判断是否为含 Convert/Math/Parse 的业务逻辑类型声明（应保留，避免后续 G1 引用未声明变量）。
    /// 仅匹配基本类型声明 + 转换/数学/解析方法，排除纯赋值和旧体系包装语句。
    /// 若行引用旧框架变量（item./result./rData/data./msg.），返回 false 让其转为 TODO，
    /// 因为这些变量在新体系中不存在，保留会导致 CS0103。
    /// </summary>
    private static bool IsBusinessLogicDeclaration(string line)
    {
        if (!line.StartsWith("double ", StringComparison.Ordinal) &&
            !line.StartsWith("int ", StringComparison.Ordinal) &&
            !line.StartsWith("var ", StringComparison.Ordinal) &&
            !line.StartsWith("string ", StringComparison.Ordinal) &&
            !line.StartsWith("bool ", StringComparison.Ordinal))
            return false;
        // 引用旧框架变量的不保留（会 CS0103）
        if (ReferencesLegacyFramework(line)) return false;
        return line.Contains("Convert.", StringComparison.Ordinal)
            || line.Contains("Math.", StringComparison.Ordinal)
            || line.Contains(".Parse(", StringComparison.Ordinal)
            || line.Contains("TryParse(", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断是否为简单基本类型声明（double X = ...; / int X = ...; / var X = ...; / double[] X = ...; 等），
    /// 不引用旧框架变量（item./result./rData/data./msg.）也不引用旧类型（PAMassage 等）。
    /// 这些声明应保留以避免后续引用 CS0103（如 double X = double.MaxValue; / double[] PowerCheck = new double[3];）。
    /// </summary>
    private static bool IsSimpleBasicTypeDeclaration(string line)
    {
        if (!line.StartsWith("double ", StringComparison.Ordinal) &&
            !line.StartsWith("int ", StringComparison.Ordinal) &&
            !line.StartsWith("var ", StringComparison.Ordinal) &&
            !line.StartsWith("string ", StringComparison.Ordinal) &&
            !line.StartsWith("bool ", StringComparison.Ordinal) &&
            // B3: 数组声明（double[] / int[]），旧脚本数组分配/初始化也应保留
            !line.StartsWith("double[] ", StringComparison.Ordinal) &&
            !line.StartsWith("int[] ", StringComparison.Ordinal))
            return false;
        if (ReferencesLegacyFramework(line)) return false;
        if (ReferencesLegacyTypes(line)) return false;
        // 必须是赋值声明（含 =），排除仅声明 `int X;` 等少见情况
        return line.Contains(" = ", StringComparison.Ordinal);
    }

    /// <summary>G13: 应用 LeakFormula 替换（Util.LeakTestValueFormula → LeakFormula.Compute + 枚举名修正）。</summary>
    private static string ApplyLeakFormulaReplacements(string value)
        => value
            .Replace("Util.LeakTestValueFormula(", "LeakFormula.Compute(", StringComparison.Ordinal)
            .Replace("LeakDeviceModel.MP_DP_LLP", "LeakDeviceModel.MpDpLlp", StringComparison.Ordinal)
            .Replace("LeakDeviceModel.HMP", "LeakDeviceModel.Hmp", StringComparison.Ordinal);

    /// <summary>
    /// 解析 out 参数（含类型）的正则：out Type VarName 或 out VarName。
    /// 第 1 组为可选类型，第 2 组为变量名。
    /// </summary>
    private static readonly Regex OutTypedVarPattern = new(
        @"out\s+(?:(\w+(?:<[^>]+>)?)\s+)?(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// G1 out 变量预声明：扫描参数列表中的 out Type VarName（或 out VarName），
    /// 在调用前发射变量声明（用默认值），避免后续引用 CS0103。
    /// 已声明的变量跳过，避免重复。
    /// </summary>
    private static void EmitOutVarDeclarations(
        string args,
        Dictionary<string, string> varTypes,
        HashSet<string> declaredNonCondVars,
        List<string> lines,
        List<string> todos,
        string originalLine)
    {
        if (string.IsNullOrEmpty(args)) return;
        foreach (Match m in OutTypedVarPattern.Matches(args))
        {
            var type = m.Groups[1].Value;
            var varName = m.Groups[2].Value;
            if (varName.Length == 0) continue;
            if (declaredNonCondVars.Contains(varName)) continue;
            // 推断类型：out 中已带 → 直接用；否则查 varTypes
            if (type.Length == 0 && varTypes.TryGetValue(varName, out var t))
                type = t;
            var defaultExpr = GetDefaultExpression(type);
            if (defaultExpr == null)
            {
                // 类型未知且无默认值 → 留 TODO，不发射声明（保留 G1out 处理路径）
                todos.Add($"G1 out 变量 {varName} 类型未知，未发射预声明：`{originalLine}`");
                continue;
            }
            lines.Add($"{(type.Length > 0 ? type : "var")} {varName} = {defaultExpr}; // TODO(自动转换-G1out): 旧 out 语义丢失");
            declaredNonCondVars.Add(varName);
            varTypes[varName] = type;
        }
    }

    /// <summary>
    /// 根据类型返回默认值表达式；无法安全构造的返回 null（调用方应留 TODO，避免 CS8716/CS0023）。
    /// 对实体 record 用其默认构造参数；对 List&lt;T&gt; 用 new List&lt;T&gt;()。
    /// 旧体系遗留类型（PAMassage/CheckState 等）返回 null，避免引用未定义类型。
    /// </summary>
    private static string? GetDefaultExpression(string type)
    {
        if (type.Length == 0) return null;
        // 旧体系遗留类型 → 不构造（避免 CS0246）
        if (LegacyTypes.Contains(type)) return null;
        // List<LegacyType> → 不构造
        if (type.StartsWith("List<", StringComparison.Ordinal))
        {
            var innerType = type.Substring(5, type.Length - 6);
            if (LegacyTypes.Contains(innerType)) return null;
            return $"new {type}()";
        }
        return type switch
        {
            "DateTime" => "default(DateTime)",
            "TimeSpan" => "default(TimeSpan)",
            "double" or "Double" => "0.0",
            "int" or "Int32" => "0",
            "bool" or "Boolean" => "false",
            "string" or "String" => "\"\"",
            "Pressure" => "new Pressure(0, \"kPa\")",
            "PressureRange" => "new PressureRange(0, 0, \"kPa\")",
            "ElectricMeasure" => "new ElectricMeasure(0, \"\", ElectricMeasureFunction.None)",
            _ => null, // 未知类型 → 不构造，调用方留 TODO
        };
    }
}
