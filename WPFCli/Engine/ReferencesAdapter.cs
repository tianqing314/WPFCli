using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>References 适配注入结果汇总。</summary>
public sealed record ReferencesAdapterResult(
    bool Found,
    int DllCopied,
    int DllAdded,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<string> RemovedFiles,
    IReadOnlyList<string> TodoItems);

/// <summary>
/// References 适配器 —— 动态工装模板构建时，从 <c>References\{被检类型}\</c> 拉取旧 Bots.TestBench
/// 体系资源（Xmas11 dll / Uut 设备类 / TestSteps 脚本 / Jigs 配置），自动转换为新 TESTRIG 体系产物并
/// 注入 staging 输出目录，替换模板内置被检占位（如 ConST171）。
///
/// 四类适配规则：
///   1) Xmas11\*.dll      → 拷贝到 refdlls（同名覆盖，新名添加），新 dll 自动补 TESTRIG.Devices.csproj Reference；
///   2) Uut\*.cs          → 生成 TESTRIG.Devices.Abstractions\Dut\I{类型}Dut.cs（命令枚举+接口）
///                          与 TESTRIG.Devices\Dut\{类型}\{类型}Dut.cs（[DutDriver] 真机驱动，走 Xmas11 DPG2SCPI）；
///   3) TestSteps\*.cs    → 生成 TESTRIG.TestSteps\{类型}\{类型}_ControlBoard\{类型}_ControlBoard.cs
///                          （Ops 辅助类 + 每测试项一个 IStepHandler，自动注册）；
///   4) Jigs\*.json       → 生成 TESTRIG.Jigs\Manifests\{类型}\{类型}_ControlBoard.json（新 manifest 格式）。
/// 生成产物直接以实际被检类型命名（不依赖后续占位符替换），并删除模板内置占位对应文件。
/// 无法自动映射的语句转成 TODO 注释并汇总到 <c>_ReferencesAdapterReport.md</c>。
/// </summary>
public static class ReferencesAdapter
{
    public const string ReportFileName = "_ReferencesAdapterReport.md";

    // ===== 旧体系解析正则（逐字字符串，中文标识符 \u4e00-\u9fff）=====

    /// <summary>SimpleCommands 字典项：{SimpleCommandEnum.枚举名,"SCPI串"}。</summary>
    private static readonly Regex SimpleCommandPattern = new(
        @"\{SimpleCommandEnum\.([\w\u4e00-\u9fff]+),\s*""([^""]+)""\}", RegexOptions.Compiled);

    /// <summary>脚本测试方法声明：public dynamic 方法名(dynamic item)。</summary>
    private static readonly Regex ScriptMethodPattern = new(
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

    /// <summary>按被检类型解析 References 根目录（默认工作区根\References）。</summary>
    public static string ResolveReferencesRoot(BuildOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ReferencesRoot))
            return Path.GetFullPath(opts.ReferencesRoot);
        var templateRoot = Path.GetDirectoryName(Path.GetFullPath(opts.TemplatePath)) ?? "";
        return Path.Combine(templateRoot, "References");
    }

    /// <summary>
    /// 向 staging 输出目录注入 References 适配产物。找不到 References\{被检类型} 时仅警告并继续。
    /// </summary>
    public static ReferencesAdapterResult Inject(
        BuildOptions opts, string dutValue, string outputDir, Action<string>? onProgress = null)
    {
        var report = new List<string> { $"# References 适配报告（被检类型 {dutValue}）", "" };
        var generated = new List<string>();
        var removed = new List<string>();
        var todos = new List<string>();

        var refDir = Path.Combine(ResolveReferencesRoot(opts), dutValue);
        if (!Directory.Exists(refDir))
        {
            var msg = $"未找到 References\\{dutValue}，跳过 References 适配（保留模板内置占位 {opts.BusinessTemplate.DutPlaceholder}）";
            onProgress?.Invoke(msg);
            report.Add($"> {msg}");
            WriteReport(outputDir, report, generated, removed, todos);
            return new ReferencesAdapterResult(false, 0, 0, generated, removed, todos);
        }
        onProgress?.Invoke($"找到 References\\{dutValue}，开始适配注入");

        var placeholder = opts.BusinessTemplate.DutPlaceholder;
        var dllCopied = 0;
        var dllAdded = 0;

        // ---- 1. Xmas11 dll → refdlls（同名覆盖，新名添加）+ csproj 联动 ----
        var x11Dir = Path.Combine(refDir, "Xmas11");
        var addedDlls = new List<string>();
        if (Directory.Exists(x11Dir))
        {
            foreach (var dll in Directory.GetFiles(x11Dir, "*.dll"))
            {
                var name = Path.GetFileName(dll);
                var dest = Path.Combine(outputDir, "refdlls", name);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                var existed = File.Exists(dest);
                File.Copy(dll, dest, overwrite: true);
                if (!existed)
                {
                    addedDlls.Add(name);
                    dllAdded++;
                }
                else
                {
                    dllCopied++;
                }
                report.Add($"- dll [{(existed ? "覆盖" : "新增")}] `{name}` → `refdlls\\{name}`");
            }
            AddReferencesToDevicesCsproj(outputDir, addedDlls, report);
        }

        // ---- 2. Uut 设备类 → 接口 + 驱动（替换内置占位）----
        var uutFile = FirstFile(refDir, "Uut", "*.cs");
        if (uutFile != null)
        {
            var (ifaceRel, driverRel, uutTodos) = GenerateDutFiles(uutFile, dutValue, outputDir);
            generated.AddRange(ifaceRel);
            generated.AddRange(driverRel);
            todos.AddRange(uutTodos);
            RemovePlaceholderDut(outputDir, placeholder, removed);
            report.Add("- Uut：生成接口与驱动，删除内置占位驱动");
            report.AddRange(ifaceRel.Select(p => $"  - 生成 `{p}`"));
            report.AddRange(driverRel.Select(p => $"  - 生成 `{p}`"));
        }
        else
        {
            report.Add("- Uut：无设备类文件，跳过");
        }

        // ---- 3. TestSteps 脚本 + Jigs 配置 → 处理器 + manifest（替换内置占位）----
        var stepsFile = FirstFile(refDir, "TestSteps", "*.cs");
        var jigFile = FirstFile(refDir, "Jigs", "*.json");
        if (stepsFile != null && jigFile != null)
        {
            var (handlerRel, manifestRel, stepsTodos) = GenerateTestStepsAndManifest(stepsFile, jigFile, dutValue, outputDir);
            generated.AddRange(handlerRel);
            generated.AddRange(manifestRel);
            todos.AddRange(stepsTodos);
            RemovePlaceholderTestStepsAndJigs(outputDir, placeholder, removed);
            report.Add("- TestSteps+Jigs：生成处理器与 manifest，删除内置占位文件");
            report.AddRange(handlerRel.Select(p => $"  - 生成 `{p}`"));
            report.AddRange(manifestRel.Select(p => $"  - 生成 `{p}`"));
        }
        else
        {
            report.Add("- TestSteps/Jigs：缺少脚本或配置，跳过（保留内置占位）");
        }

        if (todos.Count > 0)
        {
            report.Add("");
            report.Add("## 待人工核对（自动转换 TODO）");
            report.AddRange(todos.Distinct().Select(t => $"- [ ] {t}"));
        }

        WriteReport(outputDir, report, generated, removed, todos);
        onProgress?.Invoke($"References 适配完成：dll {dllAdded + dllCopied}（新增 {dllAdded}）、生成 {generated.Count}、删除 {removed.Count}、TODO {todos.Count}");
        return new ReferencesAdapterResult(true, dllCopied, dllAdded, generated, removed, todos);
    }

    private static string? FirstFile(string refDir, string subDir, string pattern)
    {
        var dir = Path.Combine(refDir, subDir);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern).FirstOrDefault() : null;
    }

    // =====================================================================
    // 1. Xmas11 → csproj 联动
    // =====================================================================

    private static void AddReferencesToDevicesCsproj(string outputDir, IReadOnlyList<string> addedDlls, List<string> report)
    {
        if (addedDlls.Count == 0) return;
        var csproj = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices", "TESTRIG.Devices.csproj");
        if (!File.Exists(csproj))
        {
            report.Add($"> 警告：未找到 TESTRIG.Devices.csproj，新增 dll 引用未写入");
            return;
        }
        var content = File.ReadAllText(csproj, Encoding.UTF8);
        var sb = new StringBuilder(content);
        foreach (var dll in addedDlls)
        {
            var asm = Path.GetFileNameWithoutExtension(dll);
            if (content.Contains($"Include=\"{asm}\"", StringComparison.OrdinalIgnoreCase))
            {
                report.Add($"  - csproj 已引用 `{asm}`，跳过");
                continue;
            }
            var line = $"  <Reference Include=\"{asm}\"><HintPath>$(X11)\\{asm}.dll</HintPath><Private>true</Private></Reference>";
            var itemGroup = $"  <ItemGroup>\r\n{line}\r\n  </ItemGroup>\r\n";
            var insertAt = sb.ToString().LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            if (insertAt >= 0)
            {
                sb.Insert(insertAt, itemGroup);
                report.Add($"  - csproj 新增引用 `{asm}`");
            }
            else
            {
                report.Add($"  - 警告：csproj 无 </Project> 结尾，`{asm}` 引用未写入");
            }
        }
        File.WriteAllText(csproj, sb.ToString(), new UTF8Encoding(false));
    }

    // =====================================================================
    // 2. Uut 设备类 → I{类型}Dut 接口 + {类型}Dut 驱动
    // =====================================================================

    private static (List<string> ifaceFiles, List<string> driverFiles, List<string> todos) GenerateDutFiles(
        string sourceFile, string dut, string outputDir)
    {
        var todos = new List<string>();
        var text = File.ReadAllText(sourceFile);

        // 提取 SimpleCommands 字典（枚举名 → SCPI 串）
        var commands = new List<(string Name, string Scpi)>();
        foreach (Match m in SimpleCommandPattern.Matches(text))
            commands.Add((m.Groups[1].Value, m.Groups[2].Value));

        var ifaceDir = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut");
        var driverDir = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices", "Dut", dut);
        Directory.CreateDirectory(ifaceDir);
        Directory.CreateDirectory(driverDir);

        var ifaceRel = Path.Combine("src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut", $"I{dut}Dut.cs");
        var driverRel = Path.Combine("src", "03.Devices", "TESTRIG.Devices", "Dut", dut, $"{dut}Dut.cs");
        File.WriteAllText(Path.Combine(outputDir, ifaceRel), BuildInterfaceSource(dut, commands), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, driverRel), BuildDriverSource(dut, commands), new UTF8Encoding(false));

        if (commands.Count == 0)
            todos.Add($"{Path.GetFileName(sourceFile)}：未解析到 SimpleCommands 字典，{dut}Command 枚举为空");

        return (new List<string> { ifaceRel }, new List<string> { driverRel }, todos);
    }

    private static string BuildInterfaceSource(string dut, IReadOnlyList<(string Name, string Scpi)> commands)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace TESTRIG.Devices.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板被检命令层。**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2.SimpleCommandEnum</c>");
        sb.AppendLine("/// （SCPI 指令转发）。执行失败抛 <see cref=\"DeviceCommException\"/>（由引擎按异常收尾并落盘）。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public enum {dut}Command");
        sb.AppendLine("{");
        foreach (var (name, scpi) in commands)
            sb.AppendLine($"    /// <summary>SCPI {scpi}</summary>");
        foreach (var (name, _) in commands)
            sb.AppendLine($"    {name},");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板（设备族 {dut}）被检命令接口。**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2</c>");
        sb.AppendLine("/// （旧平台驱动，内部转调 Xmas11 <c>DPG2SCPI</c>，返回 <c>iResponse</c>）。");
        sb.AppendLine("/// 读值方法返回值、通讯/执行失败抛 <see cref=\"DeviceCommException\"/>（由引擎按异常收尾并落盘）。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public interface I{dut}Dut : IDutDevice");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 补充连接（重连），返回是否已连接。PORT: 旧 ConST221_2.ReplenishLink（针床设备逻辑简化为直接建连）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    /// <returns>是否连接成功。</returns>");
        sb.AppendLine("    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 执行一条主板动态测试 SCPI 指令（无回值）。PORT: 旧 ConST221_2.ExecuteAnyCommand_NoResponse。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"command\">指令（电源开/关、RTC/铁电/FLASH 自检等）。</param>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine($"    Task ExecuteAnyCommandNoResponseAsync({dut}Command command, CancellationToken ct = default);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildDriverSource(string dut, IReadOnlyList<(string Name, string Scpi)> commands)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.IO.Ports;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine("using TESTRIG.Devices.Abstractions;");
        sb.AppendLine("using Xmas11.Comm.Data.Common;");
        sb.AppendLine("using Xmas11.Comm.Devices;");
        sb.AppendLine("using Xmas11.Comm.Devices.DPG2;");
        sb.AppendLine();
        sb.AppendLine($"namespace TESTRIG.Devices.Dut.{dut};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板（设备族 {dut}）被检**真机驱动**：走 Xmas11 <see cref=\"DPG2SCPI\"/> 通讯库，");
        sb.AppendLine($"/// 命令层**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2</c>（内部转调 <c>DPG2SCPI.*</c>，返回 <c>iResponse</c>）。");
        sb.AppendLine("/// 连接按 manifest 号位 <see cref=\"CommEndpoint\"/>（默认串口，对齐旧 Open 的 Board 分支）建连。");
        sb.AppendLine("/// 每条命令 <c>iResponse.IsCorrect=false</c> 即抛 <see cref=\"DeviceCommException\"/>，交引擎按异常收尾。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[DutDriver(\"{dut}\")]");
        sb.AppendLine($"public sealed class {dut}Dut : I{dut}Dut");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>日志。</summary>");
        sb.AppendLine("    private readonly ILogger _logger;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>连接端点（号位 Comm）。</summary>");
        sb.AppendLine("    private readonly CommEndpoint? _comm;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>{dut} 通讯实例（连接后有值）。</summary>");
        sb.AppendLine("    private DPG2SCPI? _dev;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>设备键。</summary>");
        sb.AppendLine("    public string Key { get; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>设备型号名。</summary>");
        sb.AppendLine("    public string Model { get; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>是否已连接。</summary>");
        sb.AppendLine("    public bool IsConnected { get; private set; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 取 {dut} 实例，未连接抛 <see cref=\"DeviceCommException\"/>（CommunicationError）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    private DPG2SCPI Dev => _dev ?? throw new DeviceCommException(\"{dut} 未连接\", TestResultStatus.CommunicationError);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 用设备描述符构造真机被检（端点取号位 Comm）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"descriptor\">设备描述符（含号位 Comm）。</param>");
        sb.AppendLine("    /// <param name=\"logger\">日志。</param>");
        sb.AppendLine($"    public {dut}Dut(DeviceDescriptor descriptor, ILogger logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("        Key = descriptor.Model;");
        sb.AppendLine("        Model = descriptor.Model;");
        sb.AppendLine("        _comm = descriptor.Comm;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 连接被检：按端点（网络/串口）建 DPG2SCPI，Open 探活。PORT: 旧 ConST221_2.Open()。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    public Task ConnectAsync(CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            try { _dev?.Close(); } catch { }");
        sb.AppendLine("            _dev = Build(_comm);");
        sb.AppendLine("            var opened = _dev.Open();");
        sb.AppendLine("            IsConnected = opened && _dev.IsExist();");
        sb.AppendLine($"            _logger.LogInformation(IsConnected ? $\"{dut} 真机连接成功\" : $\"{dut} 连接未就绪（将重试）\");");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 按端点构造 DPG2SCPI（网络/串口）。PORT: 旧默认串口（Board 模式 19200/Two）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ep\">连接端点。</param>");
        sb.AppendLine("    /// <returns>通讯实例。</returns>");
        sb.AppendLine("    private static DPG2SCPI Build(CommEndpoint? ep)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (ep is null || ep.Link == LinkType.Ethernet)");
        sb.AppendLine("        {");
        sb.AppendLine("            var ip = ep?.Ip ?? Environment.GetEnvironmentVariable(\"TESTRIG_DUT_IP\") ?? \"192.168.40.107\";");
        sb.AppendLine("            var port = ep?.Port ?? int.Parse(Environment.GetEnvironmentVariable(\"TESTRIG_DUT_PORT\") ?? \"1030\", CultureInfo.InvariantCulture);");
        sb.AppendLine("            return new DPG2SCPI(IPAddress.Parse(ip), port);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (ep.Link == LinkType.Serial)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sp = ep.Serial ?? new SerialParams();");
        sb.AppendLine("            var portName = string.IsNullOrWhiteSpace(ep.PhysicalLink) ? \"COM1\" : ep.PhysicalLink!;");
        sb.AppendLine("            var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;");
        sb.AppendLine("            var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;");
        sb.AppendLine("            return new DPG2SCPI(portName, sp.Baud, sp.DataBits, stopBits, parity);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        throw new DeviceCommException(\"{dut} 不支持 USB 连接（旧平台默认串口扫描）\", TestResultStatus.CommunicationError);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 补充连接（重连）。PORT: 旧 ConST221_2.ReplenishLink。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    /// <returns>是否连接成功。</returns>");
        sb.AppendLine("    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        await ConnectAsync(ct);");
        sb.AppendLine("        return IsConnected;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 执行一条主板动态测试 SCPI 指令（无回值）。PORT: 旧 ExecuteAnyCommand_NoResponse。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"command\">指令。</param>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine($"    public Task ExecuteAnyCommandNoResponseAsync({dut}Command command, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var scpi = command switch");
        sb.AppendLine("            {");
        foreach (var (name, scpi) in commands)
            sb.AppendLine($"                {dut}Command.{name} => \"{scpi}\",");
        sb.AppendLine($"                _ => throw new DeviceCommException($\"未知指令 {{command}}\", TestResultStatus.HardwareError),");
        sb.AppendLine("            };");
        sb.AppendLine("            var res = Dev.ExecuteAnyCommand_NoResponse(scpi);");
        sb.AppendLine("            if (!res.IsCorrect)");
        sb.AppendLine($"                throw new DeviceCommException($\"执行指令 {{command}} 失败\", TestResultStatus.HardwareError);");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // ===== IDutDevice 必需实现 ===== ");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读整机序列号。PORT: DPG2.GetSerialNumber。</summary>");
        sb.AppendLine("    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)");
        sb.AppendLine("        => Str(() => Dev.GetSerialNumber(), \"读取SN\", ct);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读固件版本。PORT: DPG2.GetVersion。</summary>");
        sb.AppendLine("    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)");
        sb.AppendLine("        => Str(() => Dev.GetVersion(), \"读取版本\", ct);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>写板卡类型/初始信息（旧体系无对应命令，留空）。</summary>");
        sb.AppendLine("    public Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)");
        sb.AppendLine("        => Task.CompletedTask;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>按测量点名测量（旧体系无通用测量入口，返回 0）。</summary>");
        sb.AppendLine("    public Task<double> MeasureAsync(string point, CancellationToken ct = default)");
        sb.AppendLine("        => Task.FromResult(0d);");
        sb.AppendLine();
        sb.AppendLine("    // ===== iResponse 包装：失败抛 DeviceCommException =====");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>执行一条返回字符串的命令，失败抛通讯异常。</summary>");
        sb.AppendLine("    private Task<string> Str(Func<iResponse<string>> call, string what, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var r = call();");
        sb.AppendLine("            if (!r.IsCorrect)");
        sb.AppendLine("                throw new DeviceCommException($\"{what}失败\", TestResultStatus.CommunicationError);");
        sb.AppendLine("            return r.Result;");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>释放连接。</summary>");
        sb.AppendLine("    public ValueTask DisposeAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        try { _dev?.Close(); } catch { }");
        sb.AppendLine("        _dev = null;");
        sb.AppendLine("        IsConnected = false;");
        sb.AppendLine("        return ValueTask.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // =====================================================================
    // 3. TestSteps 脚本 + Jigs 配置 → 处理器 + manifest
    // =====================================================================

    private static (List<string> handlerFiles, List<string> manifestFiles, List<string> todos) GenerateTestStepsAndManifest(
        string stepsFile, string jigFile, string dut, string outputDir)
    {
        var todos = new List<string>();
        var script = File.ReadAllText(stepsFile);

        // 解析旧 JSON（允许注释/尾逗号）
        using var doc = JsonDocument.Parse(
            File.ReadAllBytes(jigFile),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;

        // 任务序列（Entry 为权威来源）
        var tasks = new List<JigTask>();
        if (root.TryGetProperty("TaskCollection", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tc.EnumerateArray())
            {
                var entry = GetString(item, "Location", "Entry");
                if (string.IsNullOrWhiteSpace(entry)) continue;
                tasks.Add(new JigTask(
                    entry,
                    GetString(item, "Name") ?? entry,
                    GetString(item, "TestDesc") ?? "",
                    GetGuid(item),
                    ReadParameters(item),
                    ReadConditions(item)));
            }
        }

        var handlerDir = Path.Combine(outputDir, "src", "04.TestSteps", "TESTRIG.TestSteps", dut, $"{dut}_ControlBoard");
        var manifestDir = Path.Combine(outputDir, "src", "05.Jigs", "TESTRIG.Jigs", "Manifests", dut);
        Directory.CreateDirectory(handlerDir);
        Directory.CreateDirectory(manifestDir);

        var handlerRel = Path.Combine("src", "04.TestSteps", "TESTRIG.TestSteps", dut, $"{dut}_ControlBoard", $"{dut}_ControlBoard.cs");
        var manifestRel = Path.Combine("src", "05.Jigs", "TESTRIG.Jigs", "Manifests", dut, $"{dut}_ControlBoard.json");
        File.WriteAllText(Path.Combine(outputDir, handlerRel), BuildHandlerSource(script, tasks, dut, todos), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, manifestRel), BuildManifestSource(root, tasks, dut), new UTF8Encoding(false));

        return (new List<string> { handlerRel }, new List<string> { manifestRel }, todos);
    }

    private sealed record JigTask(string Entry, string Name, string Description, string Guid,
        List<(string Name, string Value, string? Unit)> Parameters, List<(string Name, double Min, double Max, string Unit)> Conditions);

    private static string? GetString(JsonElement obj, params string[] path)
    {
        JsonElement cur = obj;
        foreach (var seg in path)
        {
            if (!cur.TryGetProperty(seg, out cur)) return null;
        }
        return cur.ValueKind == JsonValueKind.String ? cur.GetString() : cur.ToString();
    }

    private static string GetGuid(JsonElement item)
    {
        var g = GetString(item, "GUID");
        return string.IsNullOrWhiteSpace(g) ? Guid.NewGuid().ToString() : g;
    }

    private static List<(string Name, string Value, string? Unit)> ReadParameters(JsonElement item)
    {
        var list = new List<(string, string, string?)>();
        if (!item.TryGetProperty("Parameters", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var p in arr.EnumerateArray())
        {
            var name = GetString(p, "Name") ?? "";
            var value = GetString(p, "Value") ?? "";
            var unit = GetString(p, "Unit");
            if (name.Length > 0) list.Add((name, value, unit));
        }
        return list;
    }

    private static List<(string Name, double Min, double Max, string Unit)> ReadConditions(JsonElement item)
    {
        var list = new List<(string, double, double, string)>();
        if (!item.TryGetProperty("Conditions", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var c in arr.EnumerateArray())
        {
            var name = GetString(c, "Name") ?? "";
            if (name.Length == 0) continue;
            var min = c.TryGetProperty("Lower", out var lo) && lo.TryGetDouble(out var lv) ? lv : 0;
            var max = c.TryGetProperty("Upper", out var hi) && hi.TryGetDouble(out var hv) ? hv : 0;
            var unit = GetString(c, "Unit") ?? "";
            list.Add((name, min, max, unit));
        }
        return list;
    }

    /// <summary>生成处理器源码：Ops 辅助类 + 每任务一个 IStepHandler（Kind = Entry，DeviceFamily = 被检类型）。</summary>
    private static string BuildHandlerSource(string script, IReadOnlyList<JigTask> tasks, string dut, List<string> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.IO.Ports;");
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine("using TESTRIG.Devices.Abstractions;");
        sb.AppendLine("using R = TESTRIG.Devices.Abstractions.BoxRelayCommand;");
        sb.AppendLine();
        sb.AppendLine($"namespace TESTRIG.TestSteps.{dut}.{dut}_ControlBoard;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板（设备族 {dut}）测试**设备特有**处理器集合。**自动转换**自旧");
        sb.AppendLine("/// <c>ConST221_MainBoard_Auto.cs</c> 的测试方法与 <c>.distributed.json</c> 任务配置：继电器指令序列");
        sb.AppendLine("/// （<see cref=\"BoxRelayCommand\"/>）、DAM6803D 通道电压、2 路电流表读数、被检 SCPI 指令与 Range 判定。");
        sb.AppendLine($"/// 工装用 <see cref=\"IConST326StandardBox\"/>，被检用 <see cref=\"I{dut}Dut\"/>。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal sealed class {dut}Ops");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ITestContext _ctx;");
        sb.AppendLine("    private readonly CancellationToken _ct;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>标准盒（含继电器/DAM6803D/电流表）。</summary>");
        sb.AppendLine("    public readonly IConST326StandardBox Box;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>被检 {dut} 专属驱动。</summary>");
        sb.AppendLine($"    public readonly I{dut}Dut Dut;");
        sb.AppendLine();
        sb.AppendLine($"    public {dut}Ops(ITestContext ctx, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        _ctx = ctx;");
        sb.AppendLine("        _ct = ct;");
        sb.AppendLine("        Box = ctx.GetDevice<IConST326StandardBox>();");
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
        sb.AppendLine("    public Task Sleep(int ms) => Box.IsRealHardware ? Task.Delay(ms, _ct) : Task.CompletedTask;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>发继电器指令。PORT: DSTB.RespondToCommand。</summary>");
        sb.AppendLine("    public Task Relay(R cmd) => Box.RelayCommandAsync(cmd, _ct);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读 DAM6803D 某通道电压。PORT: DSTB.GetVoltageMeasureValue。</summary>");
        sb.AppendLine("    public Task<double> ReadVolt(int channel) => Box.GetVoltageMeasureValueAsync(channel, false, _ct);");
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
        sb.AppendLine("        Report($\"{label} {F(value)}{unit}：{r.Message}\");");
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
            sb.AppendLine($"    public string? DeviceFamily => \"{dut}\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>执行本测试项。</summary>");
            sb.AppendLine("    /// <param name=\"ctx\">测试项上下文。</param>");
            sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
            sb.AppendLine("    /// <returns>测试项结果。</returns>");
            sb.AppendLine("    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var op = new {dut}Ops(ctx, ct);");
            sb.AppendLine("        var pass = true;");
            var body = ExtractScriptBody(script, task.Entry);
            if (body != null)
            {
                var (lines, entryTodos) = TranslateBody(body, task.Conditions, dut);
                todos.AddRange(entryTodos);
                foreach (var line in lines)
                    sb.AppendLine($"        {line}");
            }
            sb.AppendLine($"        return pass ? StepResult.Pass(\"{task.Name}通过\") : StepResult.Fail(\"{task.Name}未通过\");");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>提取脚本方法 WatchAndProcessIntergrade lambda 体内语句（大括号配对）。</summary>
    private static List<string>? ExtractScriptBody(string script, string methodName)
    {
        var m = ScriptMethodPattern.Match(script, IndexOfMethod(script, methodName));
        if (!m.Success) return null;
        // 找到 WatchAndProcessIntergrade(...) 后的第一个 '{'（lambda 体）
        var lambdaMarker = script.IndexOf("WatchAndProcessIntergrade", m.Index);
        if (lambdaMarker < 0) return null;
        var lambdaBrace = script.IndexOf('{', lambdaMarker);
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
    private static (List<string> Lines, List<string> Todos) TranslateBody(
        List<string> body, IReadOnlyList<(string Name, double Min, double Max, string Unit)> conditions, string dut)
    {
        var lines = new List<string>();
        var todos = new List<string>();
        var unitByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in conditions)
            unitByName[c.Name] = c.Unit;
        var condVars = new Dictionary<string, string>(StringComparer.Ordinal); // 脚本变量 → 条件名

        for (var i = 0; i < body.Count; i++)
        {
            var line = body[i].Trim();
            if (line.Length == 0) { lines.Add(""); continue; }
            if (line.StartsWith("//", StringComparison.Ordinal)) { lines.Add(line); continue; }

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
                lines.Add($"await op.Relay(R.{m.Groups[1].Value});");
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
                lines.Add(string.IsNullOrEmpty(arg)
                    ? $"await op.Box.{method}(ct);"
                    : $"await op.Box.{method}({arg}, ct);");
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
                lines.Add($"op.Report($\"{m.Groups[1].Value} {{{m.Groups[2].Value}}}\");");
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
                lines.Add($"var value = await op.Box.GetCurrentMeasureValueAsync(false, {m.Groups[1].Value}, ct);");
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

            if (line == "var DC = GetDC(item as AutoTestItem);") continue;
            if (line.StartsWith("ScriptHelper.SetDisplayer(", StringComparison.Ordinal)) continue;
            if (line.StartsWith("return ScriptHelper.WatchAndProcessIntergrade", StringComparison.Ordinal)) continue;
            if (line == ");") continue;
            if (line == "if (!res)") { lines.Add(line); continue; }
            if (line == "{") { lines.Add("{"); continue; }
            if (line == "}") { lines.Add("}"); continue; }
            if (line == "};") continue;

            // 无法识别：TODO 标注
            lines.Add($"// TODO(自动转换): {line}");
            todos.Add($"脚本：无法自动映射语句 `{line}`");
        }
        return (lines, todos);
    }

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

    // =====================================================================
    // manifest 生成
    // =====================================================================

    private static string BuildManifestSource(JsonElement root, IReadOnlyList<JigTask> tasks, string dut)
    {
        var boardName = GetString(root, "Name") ?? $"{dut}系统板动态测试";
        var deviceName = $"{dut} 被检";

        // 号位 Comm（旧 Devices[0].CommConfigs[0] SerialPortConfig）
        string? baud = null, stopBits = null, parity = null;
        if (root.TryGetProperty("Devices", out var devices) && devices.ValueKind == JsonValueKind.Array && devices.GetArrayLength() > 0)
        {
            deviceName = GetString(devices[0], "DeviceName") ?? deviceName;
            if (devices[0].TryGetProperty("CommConfigs", out var cfg) &&
                cfg.ValueKind == JsonValueKind.Array && cfg.GetArrayLength() > 0)
            {
                baud = GetString(cfg[0], "Bauds");
                stopBits = GetString(cfg[0], "StopBits");
                parity = GetString(cfg[0], "Parity");
            }
        }

        var json = new Dictionary<string, object?>
        {
            ["Key"] = $"{dut}_ControlBoard",
            ["DeviceFamily"] = dut,
            ["BoardName"] = boardName,
            ["Description"] = $"{dut} 主板动态测试（自动转换自 References\\{dut}\\Jigs，旧 {boardName}）",
            ["Dut"] = new Dictionary<string, object?> { ["Name"] = deviceName, ["Model"] = dut },
            ["Positions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["Index"] = 1,
                    ["Name"] = "1号位",
                    ["Comm"] = new Dictionary<string, object?>
                    {
                        ["Link"] = "Serial",
                        ["PhysicalLink"] = "COM1",
                        ["Serial"] = new Dictionary<string, object?>
                        {
                            ["Baud"] = int.TryParse(baud, out var b) ? b : 19200,
                            ["DataBits"] = 8,
                            ["StopBits"] = stopBits ?? "One",
                            ["Parity"] = parity ?? "None",
                        },
                    },
                },
            },
            ["Steps"] = tasks.Select(t => (object)new Dictionary<string, object?>
            {
                ["Key"] = t.Entry,
                ["Kind"] = t.Entry,
                ["Name"] = t.Name,
                ["Description"] = t.Description,
                ["Settings"] = new Dictionary<string, object?>(),
                ["Parameters"] = t.Parameters.Select(p => (object)new Dictionary<string, object?>
                {
                    ["Name"] = p.Name,
                    ["Value"] = p.Value,
                    ["Unit"] = p.Unit,
                }).ToArray(),
                ["Conditions"] = t.Conditions.Select(c => (object)new Dictionary<string, object?>
                {
                    ["Kind"] = "Range",
                    ["Name"] = c.Name,
                    ["Min"] = c.Min,
                    ["Max"] = c.Max,
                    ["Unit"] = c.Unit,
                }).ToArray(),
                ["Guid"] = t.Guid,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(json, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    // =====================================================================
    // 内置占位删除
    // =====================================================================

    private static void RemovePlaceholderDut(string outputDir, string placeholder, List<string> removed)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) return;
        var iface = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut", $"I{placeholder}Dut.cs");
        if (File.Exists(iface)) { File.Delete(iface); removed.Add(iface); }
        var dutDir = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices", "Dut", placeholder);
        if (Directory.Exists(dutDir)) { Directory.Delete(dutDir, true); removed.Add(dutDir); }
    }

    private static void RemovePlaceholderTestStepsAndJigs(string outputDir, string placeholder, List<string> removed)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) return;
        var stepsDir = Path.Combine(outputDir, "src", "04.TestSteps", "TESTRIG.TestSteps", placeholder);
        if (Directory.Exists(stepsDir)) { Directory.Delete(stepsDir, true); removed.Add(stepsDir); }
        var jigDir = Path.Combine(outputDir, "src", "05.Jigs", "TESTRIG.Jigs", "Manifests", placeholder);
        if (Directory.Exists(jigDir)) { Directory.Delete(jigDir, true); removed.Add(jigDir); }
    }

    private static void WriteReport(string outputDir, List<string> report, List<string> generated, List<string> removed, List<string> todos)
    {
        try
        {
            var path = Path.Combine(outputDir, ReportFileName);
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(path, string.Join("\r\n", report), new UTF8Encoding(true));
        }
        catch
        {
            // 报告写入失败不影响构建
        }
    }
}
