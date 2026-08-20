using System.Text;
using System.Text.RegularExpressions;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 交互式向导 —— 以分步表单收集构建选项。
/// 交互终端支持方向键选择，管道/重定向输入继续支持数字和文本。
/// </summary>
public static class InteractiveWizard
{
    /// <summary>运行向导，返回构建选项；用户取消则返回 null。</summary>
    public static BuildOptions? Run(TemplateConfig templateConfig, string workspaceRoot, string templatePath)
    {
        PrintBanner(templateConfig);

        var opts = new BuildOptions
        {
            Template = templateConfig,
            TemplatePath = templatePath
        };

        PrintStep(1, 5, "选择业务模板", "先选模板大类，再选具体业务模板");
        var businessType = PromptBusinessType(templatePath, opts);
        if (businessType == null) return null;

        PrintStep(2, 5, "设置产品代号", "仅允许以字母开头的 2-20 位字母或数字");
        var productCode = PromptProductCode(templateConfig);
        if (productCode == null) return null;
        opts.ProductCode = productCode;
        opts.OutputDir = Path.Combine(workspaceRoot, "Output", productCode);
        if (Directory.Exists(opts.OutputDir))
        {
            opts.OverwriteExisting = PromptYesNo(
                $"输出目录已存在，完整构建成功后是否替换？ ({opts.OutputDir})",
                defaultValue: false);
            if (!opts.OverwriteExisting)
            {
                PrintCancelled();
                return null;
            }
        }

        // 所有模板统一确认被检类型（无被检占位符的模板可回车跳过）
        var dutPlaceholder = opts.BusinessTemplate.DutPlaceholder;
        PrintStep(3, 5, "设置被检类型",
            string.IsNullOrWhiteSpace(dutPlaceholder)
                ? "该模板暂不支持被检占位符替换，可直接回车跳过"
                : $"替换模板中的被检占位符 {dutPlaceholder}");
        opts.DutType = PromptDutType(dutPlaceholder);

        PrintStep(4, 5, "选择被检导入方式", "原测试平台导入：从 References 拉取旧 Bots.TestBench 资源自动转换；Excel 导入：预留");
        opts.ImportMethod = PromptImportMethod();

        PrintStep(5, 5, "确认构建", "检查配置后开始生成、编译和产物自检");
        PrintSummary(opts);

        if (!PromptYesNo("确认开始构建？", defaultValue: true))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  已取消。");
            Console.ResetColor();
            return null;
        }

        return opts;
    }

    /// <summary>输入被检类型，校验合法性。有占位符时回车默认使用占位符本身（不替换）；无占位符时回车跳过。</summary>
    private static string PromptDutType(string? placeholder)
    {
        var hasPlaceholder = !string.IsNullOrWhiteSpace(placeholder);
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  被检类型");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(hasPlaceholder ? $" [如 PS02、P06，回车默认 {placeholder}]" : " [回车跳过]");
            Console.ResetColor();
            Console.Write("\n  > ");

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                if (hasPlaceholder)
                {
                    PrintAccepted($"使用默认被检类型: {placeholder}");
                    return placeholder!;
                }
                PrintAccepted("已跳过被检类型设置");
                return string.Empty;
            }

            var error = ValidateDutType(input, placeholder ?? "");
            if (error != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    {error}");
                Console.ResetColor();
                continue;
            }

            PrintAccepted($"被检类型: {input}");
            return input;
        }
    }

    /// <summary>校验被检类型合法性。返回错误消息，null 表示通过。</summary>
    public static string? ValidateDutType(string input, string placeholder)
    {
        if (string.IsNullOrEmpty(input))
            return "此项为必填，请输入。";

        if (input.Length < 2 || input.Length > 20)
            return "长度需在 2-20 字符之间。";

        if (!Regex.IsMatch(input, @"^[A-Za-z][A-Za-z0-9]*$"))
            return "仅允许字母和数字，且首字符必须为字母。";

        if (string.Equals(input, placeholder, StringComparison.OrdinalIgnoreCase))
            return null!; // 与占位符相同 = 不替换，合法

        return null;
    }

    /// <summary>打印 Banner（简洁居中样式，避免 ASCII 艺术字在部分控制台乱码）。</summary>
    private static void PrintBanner(TemplateConfig cfg)
    {
        var contentLines = new[]
        {
            "TestRig CLI  v2.0.0",
            "动态测试工装构建工具",
            cfg.Description,
            $"占位符: {cfg.Placeholder}    框架: {cfg.TargetFramework}"
        };
        var maxWidth = Math.Max(42, GetTerminalWidth() - 8);
        var wrappedLines = contentLines
            .SelectMany(line => WrapDisplayText(line, maxWidth - 4))
            .ToList();
        var contentWidth = Math.Max(42, wrappedLines.Max(DisplayWidth) + 2);
        var border = "  +" + new string('-', contentWidth + 2) + "+";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(border);
        for (var i = 0; i < wrappedLines.Count; i++)
        {
            Console.ForegroundColor = i switch
            {
                0 => ConsoleColor.White,
                1 => ConsoleColor.Cyan,
                _ => ConsoleColor.DarkGray
            };
            Console.WriteLine($"  | {PadDisplay(wrappedLines[i], contentWidth)} |");
        }
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(border);
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>判断是否为宽字符（CJK/全角，终端显示占 2 列）。</summary>
    private static bool IsWideChar(char c)
        => (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFFEF);

    private static int DisplayWidth(string text)
        => text.Sum(c => IsWideChar(c) ? 2 : 1);

    private static string PadDisplay(string text, int width)
        => text + new string(' ', Math.Max(0, width - DisplayWidth(text)));

    private static int GetTerminalWidth()
    {
        try { return Math.Max(60, Console.WindowWidth); }
        catch { return 100; }
    }

    private static IEnumerable<string> WrapDisplayText(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        var line = new StringBuilder();
        var currentWidth = 0;
        foreach (var c in text)
        {
            var charWidth = IsWideChar(c) ? 2 : 1;
            if (currentWidth + charWidth > width && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
                currentWidth = 0;
            }
            line.Append(c);
            currentWidth += charWidth;
        }
        if (line.Length > 0) yield return line.ToString();
    }

    /// <summary>模板大类定义（顺序即展示顺序）：大类标识 → 显示名称。</summary>
    private static readonly (string Group, string DisplayName)[] BusinessGroups =
    {
        ("dedicated", "专线模板"),
        ("general", "通用模板")
    };

    /// <summary>选择业务模板：两级选择——先选模板大类（专线/通用），再选该大类下的具体业务模板。</summary>
    private static string? PromptBusinessType(string templatePath, BuildOptions opts)
    {
        IReadOnlyList<BusinessTemplateDescriptor> templates;
        try
        {
            templates = TemplateCatalog.Discover(templatePath);
        }
        catch (Exception ex)
        {
            PrintWarning(ex.Message);
            return null;
        }

        // 按大类分组（仅保留声明了 group 的模板）
        var grouped = BusinessGroups
            .Select(group => (
                Group: group,
                Templates: templates
                    .Where(template => template.Config.Group.Equals(group.Group, StringComparison.OrdinalIgnoreCase))
                    .ToList()))
            .Where(group => group.Templates.Count > 0)
            .ToList();

        if (grouped.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [ERROR] 未找到任何业务模板（Template\\<业务>\\template.config.json）。");
            Console.ResetColor();
            return null;
        }

        // 第一步：选大类
        var groupChoices = grouped
            .Select(group => new ChoiceItem(
                group.Group.Group,
                $"包含 {group.Templates.Count} 个模板",
                false,
                DisplayName: group.Group.DisplayName))
            .ToList();
        var selectedGroup = PromptChoice(groupChoices);
        if (selectedGroup == null)
        {
            PrintCancelled();
            return null;
        }

        var chosenGroup = grouped.First(group =>
            group.Group.Group.Equals(selectedGroup.Value, StringComparison.OrdinalIgnoreCase));

        // 第二步：选该大类下的具体模板
        var templateChoices = chosenGroup.Templates
            .Select(template => new ChoiceItem(
                template.DirectoryName,
                template.Config.Description,
                template.Config.Disabled,
                DisplayName: ShortDisplayName(template.Config.Description)))
            .ToList();
        var selectedTemplate = PromptChoice(templateChoices);
        if (selectedTemplate == null)
        {
            PrintCancelled();
            return null;
        }

        var template = chosenGroup.Templates.First(template =>
            template.DirectoryName.Equals(selectedTemplate.Value, StringComparison.OrdinalIgnoreCase));
        opts.BusinessTemplatePath = template.DirectoryPath;
        opts.BusinessTemplate = template.Config;
        PrintAccepted($"业务模板: {ShortDisplayName(template.Config.Description)}（{template.Config.Description}）");
        return template.DirectoryName;
    }

    /// <summary>从模板描述提取短显示名（取第一个全角括号前的前缀）。</summary>
    private static string ShortDisplayName(string description)
    {
        var idx = description.IndexOf('（');
        return idx > 0 ? description[..idx] : description;
    }

    /// <summary>选择被检导入方式：原测试平台导入（默认）/ 新方式 Excel 导入（预留）。</summary>
    private static DutImportMethod PromptImportMethod()
    {
        var choices = new List<ChoiceItem>
        {
            new("original", "从 References 拉取旧 Bots.TestBench 资源自动转换", false, DisplayName: "原测试平台导入"),
            new("excel", "预留，暂不实现解析", false, DisplayName: "新方式 Excel 导入")
        };
        var selected = PromptChoice(choices);
        if (selected == null)
        {
            PrintAccepted("使用默认导入方式：原测试平台导入");
            return DutImportMethod.OriginalPlatform;
        }
        var method = selected.Value.Equals("excel", StringComparison.OrdinalIgnoreCase)
            ? DutImportMethod.Excel
            : DutImportMethod.OriginalPlatform;
        PrintAccepted($"被检导入方式: {(method == DutImportMethod.OriginalPlatform ? "原测试平台导入" : "新方式 Excel 导入")}");
        return method;
    }

    /// <summary>校验产品代号合法性（CLI 与交互共用）。返回错误消息，null 表示通过。</summary>
    public static string? ValidateProductCode(TemplateConfig cfg, string input)
    {
        if (string.IsNullOrEmpty(input))
            return "此项为必填，请输入。";

        // 校验：长度 2-20
        if (input.Length < 2 || input.Length > 20)
            return "长度需在 2-20 字符之间。";

        // 校验：仅字母数字，首字符必须字母
        if (!Regex.IsMatch(input, @"^[A-Za-z][A-Za-z0-9]*$"))
            return "仅允许字母和数字，且首字符必须为字母。";

        // 校验：不能与模板中的保留名冲突（如 PS02 设备型号）
        if (cfg.ReservedNames.Any(r => string.Equals(r, input, StringComparison.OrdinalIgnoreCase)))
            return $"代号 \"{input}\" 与模板中的设备型号 \"{cfg.ReservedNames.First(r => string.Equals(r, input, StringComparison.OrdinalIgnoreCase))}\" 冲突，请使用其他代号。";

        // 校验：不能与占位符相同
        if (string.Equals(input, cfg.Placeholder, StringComparison.OrdinalIgnoreCase))
            return $"代号不能与模板占位符 \"{cfg.Placeholder}\" 相同。";

        return null;
    }

    /// <summary>按目录名解析业务模板（CLI 与交互共用）。返回错误消息，null 表示成功。</summary>
    /// <param name="templatePath">模板根目录（Template\）。</param>
    /// <param name="name">业务类型目录名（如 complete、Complete）。</param>
    /// <param name="cfg">解析到的业务模板元数据。</param>
    public static string? TryResolveBusinessType(string templatePath, string name, out TemplateConfig? cfg)
    {
        cfg = null;
        var error = TemplateCatalog.TryResolve(templatePath, name, out var template);
        if (error != null) return error;
        cfg = template!.Config;
        return null;
    }

    private static string? PromptProductCode(TemplateConfig cfg)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  产品代号");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" [如 PT01、MyApp]");
            Console.ResetColor();
            Console.Write("\n  > ");

            var input = Console.ReadLine()?.Trim();

            var error = ValidateProductCode(cfg, input ?? "");
            if (error != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    {error}");
                Console.ResetColor();
                continue;
            }

            PrintAccepted($"代号合法: {input}");
            return input;
        }
    }

    /// <summary>是/否询问。</summary>
    private static bool PromptYesNo(string prompt, bool defaultValue)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {prompt}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(defaultValue ? " [Y/n]" : " [y/N]");
        Console.ResetColor();
        Console.Write(": ");

        while (true)
        {
            var input = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine();
                return defaultValue;
            }
            if (input is "y" or "yes" or "是")
            {
                Console.WriteLine();
                return true;
            }
            if (input is "n" or "no" or "否")
            {
                Console.WriteLine();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("    请输入 y/yes 或 n/no，也可以直接回车使用默认值。\n");
            Console.ResetColor();
        }
    }

    private sealed record ChoiceItem(
        string Value,
        string Description,
        bool Disabled,
        bool IsHeader = false,
        string? DisplayName = null);

    /// <summary>非分组标题行的索引列表，供序号快捷选择和序号输入复用。</summary>
    private static IReadOnlyList<int> GetSelectableIndexes(IReadOnlyList<ChoiceItem> choices)
        => choices.Select((choice, index) => (choice, index))
                  .Where(item => !item.choice.IsHeader)
                  .Select(item => item.index)
                  .ToList();

    /// <summary>
    /// 选择列表：真实终端支持方向键，输入重定向时使用数字，便于 CI 和管道自动化。
    /// 分组标题行（IsHeader）不可选择，方向键与序号自动跳过。
    /// </summary>
    private static ChoiceItem? PromptChoice(IReadOnlyList<ChoiceItem> choices)
    {
        if (choices.Count == 0) return null;
        if (Console.IsInputRedirected)
            return PromptChoiceByNumber(choices);

        try
        {
            var selectableIndexes = GetSelectableIndexes(choices);
            var selected = selectableIndexes.FirstOrDefault(choiceIndex => !choices[choiceIndex].Disabled, -1);
            if (selected < 0)
            {
                PrintWarning("当前没有可用的业务模板。");
                return null;
            }
            Console.WriteLine("  使用 ↑/↓ 选择，Enter 确认，Esc 取消");
            Console.WriteLine("  ");
            var startRow = ReserveChoiceRows(choices.Count);

            while (true)
            {
                RenderChoiceList(choices, selected, startRow);
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Enter && !choices[selected].Disabled)
                {
                    Console.SetCursorPosition(0, startRow + choices.Count * 2);
                    return choices[selected];
                }
                if (key == ConsoleKey.Escape)
                {
                    Console.SetCursorPosition(0, startRow + choices.Count * 2);
                    return null;
                }
                if (key is ConsoleKey.UpArrow or ConsoleKey.DownArrow)
                {
                    var direction = key == ConsoleKey.UpArrow ? -1 : 1;
                    for (var i = 0; i < choices.Count; i++)
                    {
                        selected = (selected + direction + choices.Count) % choices.Count;
                        if (!choices[selected].Disabled && !choices[selected].IsHeader) break;
                    }
                }
                else if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9)
                {
                    var index = key - ConsoleKey.D0 - 1;
                    if (index >= 0 && index < selectableIndexes.Count)
                    {
                        var target = selectableIndexes[index];
                        if (!choices[target].Disabled) selected = target;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentOutOfRangeException)
        {
            return PromptChoiceByNumber(choices);
        }
    }

    /// <summary>
    /// 为可重绘的列表预留行。控制台在缓冲区末尾写入换行时会自动滚动，
    /// 因此在预留完成后用当前光标位置反推出列表起始行，避免定位到 BufferHeight。
    /// </summary>
    private static int ReserveChoiceRows(int choiceCount)
    {
        var requiredRows = checked(choiceCount * 2 + 1);
        if (requiredRows > Console.BufferHeight)
            throw new InvalidOperationException("终端缓冲区高度不足以显示选择列表。");

        for (var i = 0; i < requiredRows; i++)
            Console.WriteLine();

        return Math.Max(0, Console.CursorTop - requiredRows);
    }

    private static ChoiceItem? PromptChoiceByNumber(IReadOnlyList<ChoiceItem> choices)
    {
        var selectableIndexes = GetSelectableIndexes(choices);
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("  可输入序号选择，输入 q 取消：");
            var itemIndex = 0;
            for (var i = 0; i < choices.Count; i++)
            {
                if (choices[i].IsHeader)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    ── {choices[i].Value} ──");
                    Console.ResetColor();
                    Console.WriteLine();
                    continue;
                }
                itemIndex++;
                var label = choices[i].DisplayName ?? choices[i].Value;
                var state = choices[i].Disabled ? "[暂不可用]" : "[可用]";
                Console.WriteLine($"    {itemIndex}. {label,-12} {state,-10} {choices[i].Description}");
                Console.WriteLine();
            }
            Console.Write("  > ");
            var input = Console.ReadLine()?.Trim();
            if (input is "q" or "Q" or null) return null; // null = 输入重定向时 stdin 耗尽，视为取消
            if (int.TryParse(input, out var index) && index >= 1 && index <= selectableIndexes.Count)
            {
                var target = selectableIndexes[index - 1];
                if (!choices[target].Disabled) return choices[target];
                PrintWarning($"{choices[target].DisplayName ?? choices[target].Value} 为预留模板，暂不可用。");
            }
            else
            {
                PrintWarning($"请输入 1-{selectableIndexes.Count} 之间的序号。");
            }
        }
    }

    private static void RenderChoiceList(IReadOnlyList<ChoiceItem> choices, int selected, int startRow)
    {
        var descriptionWidth = Math.Max(20, GetTerminalWidth() - 34);
        var itemIndex = 0;
        for (var i = 0; i < choices.Count; i++)
        {
            var row = startRow + i * 2;
            Console.SetCursorPosition(0, row);
            if (choices[i].IsHeader)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  ── {choices[i].Value} ──");
                Console.ResetColor();
                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft - 1)));
                Console.SetCursorPosition(0, row + 1);
                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1)));
                continue;
            }

            itemIndex++;
            var marker = i == selected ? ">" : " ";
            var state = choices[i].Disabled ? "暂不可用" : "可用";
            var label = choices[i].DisplayName ?? choices[i].Value;
            var description = WrapDisplayText(choices[i].Description, descriptionWidth).FirstOrDefault() ?? string.Empty;
            Console.ForegroundColor = choices[i].Disabled
                ? ConsoleColor.DarkGray
                : i == selected ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write($"  {marker} {itemIndex}. {label,-12} [{state,-5}]  {description}");
            Console.ResetColor();
            Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft - 1)));

            Console.SetCursorPosition(0, row + 1);
            Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1)));
        }
    }

    private static void PrintStep(int step, int total, string title, string subtitle)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [ 步骤 {step} / {total} ]");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {title}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {subtitle}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }

    private static void PrintAccepted(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {message}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ! {message}");
        Console.ResetColor();
    }

    private static void PrintCancelled()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  已取消。\n");
        Console.ResetColor();
    }

    /// <summary>打印配置摘要（交互与 CLI 模式共用）。</summary>
    public static void PrintSummary(BuildOptions opts)
    {
        var rows = new List<(string Label, string Value)>
        {
            ("业务类型", opts.BusinessType)
        };

        if (!string.IsNullOrWhiteSpace(opts.DutType))
            rows.Add(("被检类型", opts.DutType));

        rows.AddRange(new[]
        {
            ("产品代号", opts.ProductCode),
            ("输出目录", opts.OutputDir),
            ("模板描述", opts.Template.Description),
            ("目标框架", opts.Template.TargetFramework),
            ("编译配置", opts.Template.Configuration),
            ("被检导入方式", opts.ImportMethod == DutImportMethod.OriginalPlatform ? "原测试平台导入" : "新方式 Excel 导入"),
            ("安装包", opts.EnablePackaging ? "是" : "否")
        });
        var labelWidth = rows.Max(row => DisplayWidth(row.Label));
        var valueWidth = Math.Max(28, Math.Min(72, GetTerminalWidth() - labelWidth - 9));
        var border = $"  +{new string('-', labelWidth + 2)}+{new string('-', valueWidth + 2)}+";

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  配置摘要");
        Console.WriteLine(border);
        Console.ResetColor();

        foreach (var row in rows) PrintSummaryRow(row.Label, row.Value, labelWidth, valueWidth);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(border);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        PrintSummaryRow("生成时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), labelWidth, valueWidth);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(border);
        Console.ResetColor();
    }

    private static void PrintSummaryRow(string label, string value, int labelWidth, int valueWidth)
    {
        Console.ForegroundColor = ConsoleColor.White;
        var lines = WrapDisplayText(value ?? string.Empty, valueWidth).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            Console.Write($"  | {PadDisplay(i == 0 ? label : string.Empty, labelWidth)} | ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(PadDisplay(lines[i], valueWidth));
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" |");
        }
        Console.ResetColor();
    }
}
