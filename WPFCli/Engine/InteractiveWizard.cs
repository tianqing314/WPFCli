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

        PrintStep(1, 7, "选择业务模板", "模板决定生成项目的业务骨架和默认能力");
        var businessType = PromptBusinessType(templatePath, opts);
        if (businessType == null) return null;

        // 动态工装模板需要指定被检类型（如 PS02），替换模板中的被检占位符（如 TemplateUUT）
        var hasDut = !string.IsNullOrWhiteSpace(opts.BusinessTemplate.DutPlaceholder);
        var totalSteps = hasDut ? 7 : 6;
        var step = 2;

        if (hasDut)
        {
            PrintStep(step, totalSteps, "设置被检类型", $"替换模板中的被检占位符 {opts.BusinessTemplate.DutPlaceholder}");
            opts.DutType = PromptDutType(opts.BusinessTemplate.DutPlaceholder!);
            step++;
        }

        PrintStep(step++, totalSteps, "设置项目代号", "仅允许以字母开头的 2-20 位字母或数字");
        var projectCode = PromptProjectCode(templateConfig);
        if (projectCode == null) return null;
        opts.ProjectCode = projectCode;
        opts.OutputDir = Path.Combine(workspaceRoot, "Output", projectCode);
        opts.Version = PromptVersion();
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

        PrintStep(step++, totalSteps, "编译选项", "可在最后的配置摘要中再次确认");
        opts.EnableObfuscation = PromptYesNo("编译文件是否混淆？", defaultValue: false);

        opts.EnablePackaging = PromptYesNo("编译后是否生成安装包？", defaultValue: true);

        PrintStep(step++, totalSteps, "GitLab 发布方案", "只生成发布文件，不会替你执行 git push");
        opts.EnableGitLab = PromptYesNo("是否上传到 GitLab（生成 .gitlab-ci.yml + 推送脚本）？", defaultValue: false);
        if (opts.EnableGitLab)
        {
            opts.GitLabRepoUrl = PromptText(
                "请输入 GitLab 仓库地址",
                "如 http://gitlab.const.cc/guanduzhen/tool/xxx.git",
                required: true);
        }

        PrintStep(step++, totalSteps, "FTP 发布方案", "发布凭据通过环境变量配置，不会写入生成文件");
        opts.EnableFtp = PromptYesNo("是否上传到 FTP 服务器（生成 FTP 发布脚本）？", defaultValue: false);
        if (opts.EnableFtp)
        {
            opts.FtpHost = PromptText(
                "请输入 FTP 服务器地址",
                "如 ftp://imdtool.const.cc",
                required: true);
            opts.FtpRemoteDir = PromptText("请输入 FTP 远程目录", "如 /TestApp，留空则上传到根目录", required: false);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    发布前设置 TESTRIG_FTP_USER 和 TESTRIG_FTP_PASSWORD 环境变量");
            Console.ResetColor();
        }

        PrintStep(step, totalSteps, "确认构建", "检查配置后开始生成、编译和产物自检");
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

    /// <summary>输入被检类型，校验合法性。回车默认使用模板占位符本身（不替换）。</summary>
    private static string PromptDutType(string placeholder)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  被检类型");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" [如 PS02、P06，回车默认 {placeholder}]");
            Console.ResetColor();
            Console.Write("\n  > ");

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                PrintAccepted($"使用默认被检类型: {placeholder}");
                return placeholder;
            }

            var error = ValidateDutType(input, placeholder);
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

    /// <summary>业务分类定义（顺序即展示顺序）：分类标识 → 显示名称。</summary>
    private static readonly (string Category, string DisplayName)[] BusinessCategories =
    {
        ("aging", "① 老化模板"),
        ("dynamic", "② 动态工装模板"),
        ("machine", "③ 整机测试模板")
    };

    /// <summary>选择业务类型：按业务分类分组展示 Template\ 下含 template.config.json 的子目录（排除 Common）。</summary>
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

        // 只保留已归入 3 个业务分类的模板（隐藏其余预留模板）
        var grouped = BusinessCategories
            .Select(category => (
                Category: category,
                Templates: templates
                    .Where(template => template.Config.Category == category.Category)
                    .ToList()))
            .ToList();

        if (grouped.All(group => group.Templates.Count == 0))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [ERROR] 未找到任何业务模板（Template\\<业务>\\template.config.json）。");
            Console.ResetColor();
            return null;
        }

        var choices = new List<ChoiceItem>();
        foreach (var group in grouped)
        {
            if (group.Templates.Count == 0) continue;
            choices.Add(new ChoiceItem(group.Category.DisplayName, "", false, IsHeader: true));
            choices.AddRange(group.Templates.Select(template => new ChoiceItem(
                template.DirectoryName,
                template.Config.Description,
                template.Config.Disabled)));
        }

        var selected = PromptChoice(choices);
        if (selected == null)
        {
            PrintCancelled();
            return null;
        }

        var selectedTemplate = templates.First(template =>
            template.DirectoryName.Equals(selected.Value, StringComparison.OrdinalIgnoreCase));
        opts.BusinessTemplatePath = selectedTemplate.DirectoryPath;
        opts.BusinessTemplate = selectedTemplate.Config;
        PrintAccepted($"业务类型: {selectedTemplate.DirectoryName}（{selectedTemplate.Config.Description}）");
        return selectedTemplate.DirectoryName;
    }

    /// <summary>校验项目代号合法性（CLI 与交互共用）。返回错误消息，null 表示通过。</summary>
    public static string? ValidateProjectCode(TemplateConfig cfg, string input)
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

    private static string PromptVersion()
    {
        while (true)
        {
            var version = PromptText("项目版本", "留空则按模板版本自动递增 patch", required: false);
            if (string.IsNullOrWhiteSpace(version) || VersionManager.IsValidVersion(version)) return version;
            PrintWarning("版本号格式应为 major.minor.patch[.revision]。");
        }
    }

    /// <summary>输入项目代号，校验合法性。</summary>
    private static string? PromptProjectCode(TemplateConfig cfg)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  项目代号");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" [如 PT01、MyApp]");
            Console.ResetColor();
            Console.Write("\n  > ");

            var input = Console.ReadLine()?.Trim();

            var error = ValidateProjectCode(cfg, input ?? "");
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

    /// <summary>通用文本输入（必填/选填，带示例提示）。</summary>
    private static string PromptText(string label, string example, bool required)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  > {label}");
            Console.ResetColor();
            if (!string.IsNullOrEmpty(example))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" [{example}]");
                Console.ResetColor();
            }
            Console.Write(": ");

            var input = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(input) && required)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("    此项为必填，请输入。");
                Console.ResetColor();
                continue;
            }

            // 校验：禁止引号/反引号/美元符/反斜杠及 shell 元字符 —— 这些字符会被原样写入
            // .gitlab-ci.yml / .ps1，可能破坏 YAML/PowerShell 语法（注入风险）
            if (input.Any(c => c is '\'' or '"' or '`' or '$' or '\\' or ';' or '|' or '&'))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("    输入不能包含引号、反引号、$、反斜杠、分号、管道或 & 符号，请重新输入。");
                Console.ResetColor();
                continue;
            }
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

    private sealed record ChoiceItem(string Value, string Description, bool Disabled, bool IsHeader = false);

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
                var state = choices[i].Disabled ? "[暂不可用]" : "[可用]";
                Console.WriteLine($"    {itemIndex}. {choices[i].Value,-12} {state,-10} {choices[i].Description}");
                Console.WriteLine();
            }
            Console.Write("  > ");
            var input = Console.ReadLine()?.Trim();
            if (input is "q" or "Q") return null;
            if (int.TryParse(input, out var index) && index >= 1 && index <= selectableIndexes.Count)
            {
                var target = selectableIndexes[index - 1];
                if (!choices[target].Disabled) return choices[target];
                PrintWarning($"{choices[target].Value} 为预留模板，暂不可用。");
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
            var description = WrapDisplayText(choices[i].Description, descriptionWidth).FirstOrDefault() ?? string.Empty;
            Console.ForegroundColor = choices[i].Disabled
                ? ConsoleColor.DarkGray
                : i == selected ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write($"  {marker} {itemIndex}. {choices[i].Value,-12} [{state,-5}]  {description}");
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
            ("项目代号", opts.ProjectCode),
            ("输出目录", opts.OutputDir),
            ("模板描述", opts.Template.Description),
            ("目标框架", opts.Template.TargetFramework),
            ("编译配置", opts.Template.Configuration),
            ("混淆", opts.EnableObfuscation ? $"是 ({opts.Template.ObfuscationTargets.Count} 个 DLL)" : "否"),
            ("安装包", opts.EnablePackaging ? "是" : "否"),
            ("上传 GitLab", opts.EnableGitLab ? opts.GitLabRepoUrl : "否"),
            ("上传 FTP", opts.EnableFtp ? $"{opts.FtpHost}{opts.FtpRemoteDir}" : "否"),
            ("版本号", string.IsNullOrWhiteSpace(opts.Version) ? "自动（模板 patch +1）" : opts.Version)
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
