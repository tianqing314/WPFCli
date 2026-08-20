using System.Text;
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
/// References 适配器 —— 按业务类型从 <c>References\{业务类型}\{被检类型}\</c> 拉取旧 Bots.TestBench
/// 体系资源（Xmas11 dll / Uut 设备类 / TestSteps 脚本 / Jigs 配置），自动转换为新 TESTRIG 体系产物并
/// 注入 staging 输出目录，替换模板内置被检占位（如 TemplateUUT）。
/// 业务类型取自模板配置的 businessType：动态工装 → References\Dynamic\{dut}（产物后缀 _ControlBoard），
/// 整机 → References\Machine\{dut}（产物后缀 _Machine）。
///
/// 四类适配规则：
///   1) Xmas11\*.dll      → 拷贝到 refdlls（同名覆盖，新名添加），新 dll 自动补 TESTRIG.Devices.csproj Reference；
///   2) Uut\*.cs          → 生成 TESTRIG.Devices.Abstractions\Dut\I{类型}Dut.cs（命令枚举+接口）
///                          与 TESTRIG.Devices\Dut\{类型}\{类型}Dut.cs（[DutDriver] 真机驱动，走 Xmas11 DPG2SCPI）；
///   3) TestSteps\*.cs    → 生成 TESTRIG.TestSteps\{类型}\{类型}_{后缀}\{类型}_{后缀}.cs
///                          （Ops 辅助类 + 每测试项一个 IStepHandler，自动注册）；
///   4) Jigs\*.json       → 生成 TESTRIG.Jigs\Manifests\{类型}\{类型}_{后缀}.json（新 manifest 格式）。
/// 生成产物直接以实际被检类型命名（不依赖后续占位符替换），并删除模板内置占位对应文件。
/// 无法自动映射的语句转成 TODO 注释并汇总到 <c>_ReferencesAdapterReport.md</c>。
///
/// 实现已拆分为四个专职类，本类仅保留 IO/编排 Facade 与共享小工具：
/// <see cref="DutSourceGenerator"/>（DUT 接口/驱动）、<see cref="ReferencesManifestBuilder"/>（manifest + 处理器编排）、
/// <see cref="TestStepSourceGenerator"/>（处理器源码）、<see cref="LegacyScriptTranslator"/>（旧脚本转译）。
/// </summary>
public static class ReferencesAdapter
{
    public const string ReportFileName = "_ReferencesAdapterReport.md";

    /// <summary>按被检类型解析 References 根目录（默认工作区根\References，其下 Dynamic 子目录再对接具体设备文件夹）。</summary>
    public static string ResolveReferencesRoot(BuildOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ReferencesRoot))
            return Path.GetFullPath(opts.ReferencesRoot);
        var templateRoot = Path.GetDirectoryName(Path.GetFullPath(opts.TemplatePath)) ?? "";
        return Path.Combine(templateRoot, "References");
    }

    /// <summary>
    /// 向 staging 输出目录注入 References 适配产物。找不到 References\Dynamic\{被检类型} 时仅警告并继续。
    /// </summary>
    public static ReferencesAdapterResult Inject(
        BuildOptions opts, string dutValue, string outputDir, Action<string>? onProgress = null)
    {
        var report = new List<string> { $"# References 适配报告（被检类型 {dutValue}）", "" };
        var generated = new List<string>();
        var removed = new List<string>();
        var todos = new List<string>();

        // 按业务类型路由 References 子目录：动态工装 → References\Dynamic\{dut}，整机 → References\Machine\{dut}
        var bizType = NormalizeBizType(opts.BusinessTemplate.BusinessType);
        var refDir = Path.Combine(ResolveReferencesRoot(opts), bizType, dutValue);
        if (!Directory.Exists(refDir))
        {
            var msg = $"未找到 References\\{bizType}\\{dutValue}，跳过 References 适配（保留模板内置占位 {opts.BusinessTemplate.DutPlaceholder}）";
            onProgress?.Invoke(msg);
            report.Add($"> {msg}");
            WriteReport(outputDir, report, generated, removed, todos);
            return new ReferencesAdapterResult(false, 0, 0, generated, removed, todos);
        }
        onProgress?.Invoke($"找到 References\\{bizType}\\{dutValue}，开始适配注入");

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
            var (ifaceRel, driverRel, uutTodos) = DutSourceGenerator.GenerateDutFiles(uutFile, dutValue, outputDir, bizType);
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
            var (handlerRel, manifestRel, stepsTodos) = ReferencesManifestBuilder.GenerateTestStepsAndManifest(stepsFile, jigFile, dutValue, outputDir, bizType);
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

        // ---- 4. StandardBox 标准模块（Tool 设备，如 DPSEX 正压/真空模块）----
        var stdDir = Path.Combine(refDir, "StandardBox");
        if (Directory.Exists(stdDir))
        {
            var stdFile = Directory.GetFiles(stdDir, "*.cs").FirstOrDefault();
            if (stdFile != null)
            {
                report.Add($"- StandardBox：{Path.GetFileName(stdFile)} 标准模块已识别（manifest ToolDevices 生成），"
                    + "驱动按 IStandardModule 接口模板内置/人工实现（见 DPSEXStandardModule 示例）");
                todos.Add($"{Path.GetFileName(stdFile)}：标准模块驱动需实现 IStandardModule（如 DPSEXStandardModule 示例）");
            }
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

    /// <summary>
    /// 归一化业务类型为 References 目录名（首字母大写，如 Dynamic / Machine；空值按动态工装处理）。
    /// </summary>
    /// <param name="businessType">模板配置的 businessType（小写）。</param>
    /// <returns>References 目录名。</returns>
    private static string NormalizeBizType(string? businessType)
    {
        if (string.IsNullOrWhiteSpace(businessType)) return "Dynamic";
        var t = businessType.ToLowerInvariant();
        return char.ToUpperInvariant(t[0]) + t[1..];
    }

    /// <summary>
    /// 业务类型对应的 manifest 后缀：整机 → Machine，其余（动态工装等）→ ControlBoard。
    /// </summary>
    /// <param name="bizType">业务类型（References 目录名）。</param>
    /// <returns>manifest 后缀。</returns>
    internal static string BizSuffix(string bizType)
        => bizType.Equals("Machine", StringComparison.OrdinalIgnoreCase) ? "Machine" : "ControlBoard";

    /// <summary>
    /// 业务类型对应的被检描述词：整机 → 整机，其余 → 主板。
    /// </summary>
    /// <param name="bizType">业务类型（References 目录名）。</param>
    /// <returns>描述词。</returns>
    internal static string BizLabel(string bizType)
        => bizType.Equals("Machine", StringComparison.OrdinalIgnoreCase) ? "整机" : "主板";

    internal static string ProductModelForVariant(string value)
        => value.ToUpperInvariant() switch
        {
            "MP" => "ConST811A-G",
            "DP" => "ConST811A-D",
            "LLP" => "ConST811A-LLP",
            "BP" => "ConST811A-BP",
            _ => "ConST811A",
        };

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

    /// <summary>
    /// 写入生成产物文件（UTF-8 无 BOM）。
    /// 若目标已存在（模板内置人工填充产物）则不写，保留现有内容；否则生成。
    /// </summary>
    /// <param name="path">目标文件。</param>
    /// <param name="factory">内容工厂。</param>
    internal static void WriteIfNotExists(string path, Func<string> factory)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, factory(), new UTF8Encoding(false));
    }

    // =====================================================================
    // 占位清理 + 报告
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
