using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 模板构建器 —— 负责拷贝 Template\ 到 Output\&lt;代号&gt;\，
/// 并对文件名、文件夹名、文本文件内容做精确占位符替换。
///
/// 替换策略：
    ///   - 支持 {{ProjectCode}} 等显式令牌，并兼容旧 PCBA 占位符
    ///   - 仅处理明确允许的 UTF-8 文本类型，未知文件不读取
///   - 排除目录（DeviceLink/tools/docs）下的文件不替换内容，但仍拷贝
///   - 拷贝时排除编译产物目录（bin/obj/.reasonix/.vs/.git）
/// </summary>
public static class TemplateBuilder
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".xaml", ".json", ".xml", ".config", ".props", ".targets",
        ".md", ".txt", ".ps1", ".yml", ".yaml", ".toml", ".ini", ".editorconfig", ".resx",
        ".manifest", ".sql", ".sh", ".cmd", ".bat", ".iss", ".ruleset"
    };

    private static readonly HashSet<string> TextFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gitignore", ".gitattributes", "Dockerfile", "LICENSE"
    };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Regex ExplicitTokenPattern = new(@"\{\{[A-Za-z][A-Za-z0-9]*\}\}", RegexOptions.Compiled);

    /// <summary>
    /// 项目代号占位符（如 "TESTRIG"）的边界感知正则缓存：只替换前后非字母/数字的独立词，
    /// 避免误伤 DataTemplate/ControlTemplate/ItemTemplate 等 C#/XAML 常见技术标识符。
    /// 下划线不视为词字符（PCBA_suffix 这类模板占位风格仍需替换）。
    /// </summary>
    private static readonly Dictionary<string, Regex> PlaceholderBoundaryCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 匹配业务模板中指向公共骨架的相对引用（如 sln 的 "..\Common\src\…"、
    /// csproj 的 "..\..\..\..\Common\src\…"）。扁平化合并后 Common 与业务内容
    /// 同在输出根下，这类引用需去掉一级 "..\" 并删除 "Common\" 段才指向正确的 src\。
    /// </summary>
    private static readonly Regex CommonProjectReferencePattern =
        new(@"((?:\.\.(?:\\|/))+)Common(?:\\|/)", RegexOptions.Compiled);

    /// <summary>
    /// 匹配业务模板中越过业务根指向模板根级文件（如 "..\..\..\..\README.md"）的
    /// 深层向上引用。合并后业务根消失，级数减 1 即指向输出根的同名文件。
    /// </summary>
    private static readonly Regex DeepUpwardReferencePattern =
        new(@"((?:\.\.(?:\\|/)){4,})(?=[^\s])", RegexOptions.Compiled);

    /// <summary>执行模板构建：拷贝 Common + 业务模板（合并）→ 替换内容 → 重命名文件/目录。</summary>
    public static void Build(BuildOptions opts, Action<string>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(opts);

        if (!Directory.Exists(opts.TemplatePath))
            throw new DirectoryNotFoundException($"模板目录不存在: {opts.TemplatePath}");
        if (string.IsNullOrEmpty(opts.BusinessTemplatePath) || !Directory.Exists(opts.BusinessTemplatePath))
            throw new DirectoryNotFoundException($"业务模板目录不存在: {opts.BusinessTemplatePath}");

        var outputParent = Path.GetDirectoryName(opts.OutputDir);
        if (string.IsNullOrWhiteSpace(outputParent))
            throw new InvalidOperationException($"输出目录没有有效父目录: {opts.OutputDir}");
        Directory.CreateDirectory(outputParent);

        if (File.Exists(opts.OutputDir))
            throw new IOException($"输出路径已被文件占用: {opts.OutputDir}");

        // BuildPipeline 已在 staging 目录中隔离，此处直接在 opts.OutputDir 工作，
        // 不再创建嵌套 staging（嵌套会导致 ..{name} 双点路径问题）。
        // 若存在残留（上次失败遗留），先清理。
        if (Directory.Exists(opts.OutputDir))
            TryDeleteDirectory(opts.OutputDir);
        Directory.CreateDirectory(opts.OutputDir);

        // 排除规则：全局 ∪ 业务模板（取并集）
        var excludeCopy = Merge(opts.Template.ExcludeFromCopy, opts.BusinessTemplate.ExcludeFromCopy);
        var excludeReplace = Merge(opts.Template.ExcludeFromReplacement, opts.BusinessTemplate.ExcludeFromReplacement);

        // 1. 拷贝公共骨架（Common）
        var commonPath = Path.Combine(opts.TemplatePath, "Common");
        if (!Directory.Exists(commonPath))
            throw new DirectoryNotFoundException($"公共模板目录不存在: {commonPath}");
        onProgress?.Invoke($"  拷贝公共模板到临时目录: {commonPath}");
        var commonFiles = CopyDirectory(commonPath, opts.OutputDir, excludeCopy);
        onProgress?.Invoke($"  公共模板文件: {commonFiles}");

        // 2. 拷贝业务模板（覆盖合并：同名文件覆盖，新文件追加）
        onProgress?.Invoke($"  合并业务模板: {opts.BusinessTemplatePath}");
        var businessFiles = CopyDirectory(opts.BusinessTemplatePath, opts.OutputDir, excludeCopy);
        onProgress?.Invoke($"  业务覆盖文件: {businessFiles}");

        var deletePaths = Merge(opts.Template.DeleteFromOutput, opts.BusinessTemplate.DeleteFromOutput);
        var deleted = DeleteConfiguredPaths(opts.OutputDir, deletePaths);
        if (deleted > 0) onProgress?.Invoke($"  按业务配置删除: {deleted}");

        // 2.5 References 适配注入（仅动态工装模板；找到 References\Dynamic\{被检类型} 时，
        //     从旧 Bots.TestBench 体系转换生成新 PCBA 体系产物并替换内置被检占位）
        var dutPlaceholder = opts.BusinessTemplate.DutPlaceholder;
        var hasDut = !string.IsNullOrWhiteSpace(dutPlaceholder);
        var dutValue = string.IsNullOrWhiteSpace(opts.DutType)
            ? (hasDut ? dutPlaceholder! : "TemplateUUT")
            : opts.DutType;
        if (hasDut && !string.IsNullOrWhiteSpace(dutValue))
        {
            var refResult = ReferencesAdapter.Inject(opts, dutValue, opts.OutputDir, onProgress);
            if (refResult.Found)
                onProgress?.Invoke($"  References 适配: 生成 {refResult.GeneratedFiles.Count} / 删除 {refResult.RemovedFiles.Count} / TODO {refResult.TodoItems.Count}");
        }

        // 3. 替换文件内容（排除目录、跳过二进制和模板元数据）
        onProgress?.Invoke($"  替换占位符 '{opts.Template.Placeholder}' → '{opts.ProjectCode}'");
        var replaced = ReplaceContentInFiles(opts.OutputDir, opts, excludeReplace);
        onProgress?.Invoke($"  内容替换文件: {replaced}");

        // 4. 重命名文件和文件夹（先深后浅）
        onProgress?.Invoke("  重命名文件和文件夹");
        var renamed = RenameFilesAndDirectories(opts.OutputDir, opts, excludeReplace);
        onProgress?.Invoke($"  重命名项: {renamed}");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 备份/临时目录删除失败不影响已完成的正式输出替换。
        }
    }

    /// <summary>合并两个排除列表（取并集，忽略大小写）。</summary>
    private static List<string> Merge(List<string> a, List<string> b)
    {
        var set = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var x in b) set.Add(x);
        return set.ToList();
    }

    /// <summary>
    /// 生成后自检：校验关键产物存在 + 扫描占位符残留。
    /// 返回问题列表（空 = 全部通过）；占位符残留为警告，关键产物缺失为致命。
    /// </summary>
    public static List<string> RunPostBuildChecks(BuildOptions opts)
    {
        var issues = new List<string>();
        var rootDir = opts.OutputDir;
        var placeholder = opts.Template.Placeholder;
        var projectCode = opts.ProjectCode;

        // 1. 关键产物存在性
        if (!File.Exists(opts.SolutionPath))
            issues.Add($"解决方案不存在: {opts.SolutionPath}");
        var mainCsproj = Path.Combine(rootDir, "src", "08.App", opts.MainProjectName, $"{opts.MainProjectName}.csproj");
        if (!File.Exists(mainCsproj))
            issues.Add($"主项目不存在: {mainCsproj}");

        // 2. 占位符残留扫描（排除 excludeFromReplacement 目录、二进制、模板元数据）
        //    注意：项目代号可能以占位符为前缀（如代号 PCBA221、占位符 Template 时代号 Template221），
        //    替换产物天然含占位符子串，须剔除项目代号后再判断，避免误报。
        var exclude = Merge(opts.Template.ExcludeFromReplacement, opts.BusinessTemplate.ExcludeFromReplacement);
        var leftover = new List<string>();
        foreach (var file in EnumerateFilesSafe(rootDir))
        {
            var relPath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            if (IsInExcludedPath(relPath, exclude)) continue;
            if (!IsTextFile(file)) continue;
            if (Path.GetFileName(file).Equals("template.config.json", StringComparison.OrdinalIgnoreCase)) continue;

            if (!TryReadUtf8(file, out var content, out _)) continue;
            if (HasPlaceholderLeftover(content, placeholder, projectCode))
                leftover.Add(relPath);
        }
        if (leftover.Count > 0)
        {
            var sample = string.Join(", ", leftover.Take(5)) + (leftover.Count > 5 ? " ..." : "");
            issues.Add($"发现 {leftover.Count} 个文件仍有占位符 '{placeholder}' 残留: {sample}");
        }

        var leftoverPaths = Directory.EnumerateFileSystemEntries(rootDir, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(rootDir, path).Replace('\\', '/'))
            .Where(path => path.Split('/').Any(segment => HasPlaceholderLeftover(segment, placeholder, projectCode)))
            .Take(6)
            .ToList();
        if (leftoverPaths.Count > 0)
            issues.Add($"生成路径仍有占位符残留: {string.Join(", ", leftoverPaths.Take(5))}");

        return issues;
    }

    /// <summary>
    /// 判断内容是否仍有占位符残留：显式 {{...}} 令牌恒为残留；
    /// 项目代号占位符按边界感知（独立词）匹配，且需先剔除完整项目代号（替换产物可能含占位符子串）。
    /// </summary>
    private static bool HasPlaceholderLeftover(string content, string placeholder, string projectCode)
    {
        if (ExplicitTokenPattern.IsMatch(content)) return true;
        if (placeholder.Length == 0) return false;
        var rx = GetPlaceholderBoundaryRegex(placeholder);
        if (!rx.IsMatch(content)) return false;
        if (projectCode.Length > 0 && projectCode.StartsWith(placeholder, StringComparison.Ordinal) &&
            content.Contains(projectCode, StringComparison.Ordinal))
        {
            // 剔除项目代号后再匹配（若残留的只是代号本身则非问题）
            return rx.IsMatch(content.Replace(projectCode, "", StringComparison.Ordinal));
        }
        return true;
    }

    /// <summary>取（并缓存）项目代号占位符的边界感知正则。</summary>
    private static Regex GetPlaceholderBoundaryRegex(string placeholder)
    {
        lock (PlaceholderBoundaryCache)
        {
            if (!PlaceholderBoundaryCache.TryGetValue(placeholder, out var rx))
            {
                rx = new Regex(
                    $@"(?<![A-Za-z0-9]){Regex.Escape(placeholder)}(?![A-Za-z0-9])", RegexOptions.Compiled);
                PlaceholderBoundaryCache[placeholder] = rx;
            }
            return rx;
        }
    }

    /// <summary>递归拷贝目录，支持目录名和相对路径排除。</summary>
    private static int CopyDirectory(string sourceDir, string destDir, List<string> excludeDirs)
        => CopyDirectory(sourceDir, sourceDir, destDir, excludeDirs);

    private static int CopyDirectory(string sourceRoot, string sourceDir, string destDir, List<string> excludeDirs)
    {
        Directory.CreateDirectory(destDir);
        var copied = 0;

        // 拷贝文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"模板不能包含文件链接: {file}");
            var fileName = Path.GetFileName(file);

            // 模板元数据不进入生成产物（Common/业务 config 均在合并后丢弃）
            if (fileName.Equals("template.config.json", StringComparison.OrdinalIgnoreCase))
                continue;

            // 纯转发到 ..\Common\ 的 props/targets（如 Dynamic 的 Directory.Build.props 仅
            // <Import Project="..\Common\…" />）在扁平化输出中指向不存在的目录且不含任何
            // 属性定义，跳过拷贝以保留 Common 拷贝的完整版本文件（单一来源）。
            if (IsCommonForwardImport(file))
                continue;

            var destPath = Path.Combine(destDir, fileName);
            File.Copy(file, destPath, overwrite: true);
            copied++;
            // 清除只读属性，避免后续替换/重命名失败
            var attrs = File.GetAttributes(destPath);
            if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(destPath, attrs & ~FileAttributes.ReadOnly);
        }

        // 递归拷贝子目录（排除指定目录名）
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"模板不能包含目录链接: {dir}");
            var dirName = Path.GetFileName(dir);
            var relativePath = Path.GetRelativePath(sourceRoot, dir).Replace('\\', '/');
            if (IsExcludedPath(relativePath, excludeDirs))
                continue;
            copied += CopyDirectory(sourceRoot, dir, Path.Combine(destDir, dirName), excludeDirs);
        }
        return copied;
    }

    /// <summary>递归替换目录下所有文本文件的内容（排除目录、二进制跳过）。</summary>
    private static int ReplaceContentInFiles(string rootDir, BuildOptions opts, List<string> excludeReplace)
    {
        var replacements = CreateReplacements(opts);
        var replaced = 0;

        foreach (var file in EnumerateFilesSafe(rootDir))
        {
            // 检查是否在排除目录中
            var relPath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            if (IsInExcludedPath(relPath, excludeReplace))
                continue;

            // 跳过二进制文件
            if (!IsTextFile(file)) continue;

            // 跳过自身（template.config.json 不替换）
            if (Path.GetFileName(file).Equals("template.config.json", StringComparison.OrdinalIgnoreCase))
                continue;

            // 读取 → 替换 → 写回（仅当内容变化时写回）
            if (!TryReadUtf8(file, out var content, out var hasBom)) continue;

            var newContent = ApplyReplacements(content, replacements);
            newContent = RewriteCommonProjectReferences(newContent);
            if (newContent != content)
            {
                File.WriteAllText(file, newContent, new UTF8Encoding(hasBom));
                replaced++;
            }
        }
        return replaced;
    }

    /// <summary>递归重命名文件和文件夹（先深后浅）。</summary>
    private static int RenameFilesAndDirectories(string rootDir, BuildOptions opts, List<string> excludeReplace)
    {
        var replacements = CreateReplacements(opts, forRenaming: true);
        var renamed = 0;

        // 先收集所有需要重命名的项（按深度降序：先文件后目录、先深后浅）
        var filesToRename = new List<string>();
        var dirsToRename = new List<string>();

        foreach (var file in EnumerateFilesSafe(rootDir))
        {
            // 排除目录（如 refdlls）下的文件不重命名（外部库 DLL 名称不可变）
            var relPath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            if (IsInExcludedPath(relPath, excludeReplace)) continue;

            var fileName = Path.GetFileName(file);
            if (ApplyReplacements(fileName, replacements) != fileName)
                filesToRename.Add(file);
        }

        foreach (var dir in EnumerateDirectoriesSafe(rootDir))
        {
            // 排除目录本身不重命名
            var relPath = Path.GetRelativePath(rootDir, dir).Replace('\\', '/');
            if (IsInExcludedPath(relPath, excludeReplace)) continue;

            var dirName = Path.GetFileName(dir);
            if (ApplyReplacements(dirName, replacements) != dirName)
                dirsToRename.Add(dir);
        }

        // 按路径深度降序排序（用路径分隔符数量更准确，避免长度排序的字符长度歧义）
        dirsToRename.Sort((a, b) => CountSeparators(b).CompareTo(CountSeparators(a)));
        filesToRename.Sort((a, b) => CountSeparators(b).CompareTo(CountSeparators(a)));

        // 重命名文件
        foreach (var file in filesToRename)
        {
            if (!File.Exists(file)) continue;
            var dir = Path.GetDirectoryName(file) ?? "";
            var fileName = Path.GetFileName(file);
            var newName = ApplyReplacements(fileName, replacements);
            var newPath = Path.Combine(dir, newName);
            if (newPath != file)
            {
                if (File.Exists(newPath) || Directory.Exists(newPath))
                    throw new IOException($"模板重命名冲突: {file} -> {newPath}");
                TryMoveWithRetry(() => File.Move(file, newPath), file);
                renamed++;
            }
        }

        // 重命名目录（先深后浅）
        foreach (var dir in dirsToRename)
        {
            if (!Directory.Exists(dir)) continue;
            var parent = Path.GetDirectoryName(dir) ?? "";
            var dirName = Path.GetFileName(dir);
            var newName = ApplyReplacements(dirName, replacements);
            var newPath = Path.Combine(parent, newName);
            if (newPath != dir)
            {
                if (File.Exists(newPath) || Directory.Exists(newPath))
                    throw new IOException($"模板重命名冲突: {dir} -> {newPath}");
                TryMoveWithRetry(() => Directory.Move(dir, newPath), dir);
                renamed++;
            }
        }
        return renamed;
    }

    private static int DeleteConfiguredPaths(string rootDir, List<string> deletePaths)
    {
        var root = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var deleted = 0;
        foreach (var relativePath in deletePaths)
        {
            var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"删除路径超出生成目录: {relativePath}");

            if (File.Exists(target))
            {
                File.Delete(target);
                deleted++;
            }
            else if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
                deleted++;
            }
        }
        return deleted;
    }

    private static IReadOnlyList<(string Token, string Value)> CreateReplacements(BuildOptions opts)
        => CreateReplacements(opts, forRenaming: false);

    /// <summary>
    /// 生成替换列表。<paramref name="forRenaming"/>=true 时用于文件/文件夹重命名（使用原始占位符字符串），
    /// =false 时用于文件内容替换（使用显式 {{DutType}} 令牌，避免误伤外部库引用如 ConST171Base）。
    /// </summary>
    private static IReadOnlyList<(string Token, string Value)> CreateReplacements(BuildOptions opts, bool forRenaming)
    {
        var replacements = new List<(string Token, string Value)>
        {
            ("{{ProjectCode}}", opts.ProjectCode),
            ("{{ProjectName}}", opts.ProjectCode),
            ("{{MainProjectName}}", opts.MainProjectName),
            ("{{RootNamespace}}", opts.ProjectCode),
            ("{{Version}}", opts.Version),
            ("{{BusinessType}}", opts.BusinessType),
            ("{{TargetFramework}}", opts.Template.TargetFramework)
        };

        // 项目代号占位符：边界感知替换（仅独立词），并缓存对应正则
        var placeholder = opts.Template.Placeholder;
        if (placeholder.Length > 0)
        {
            GetPlaceholderBoundaryRegex(placeholder);
            replacements.Add((placeholder, opts.ProjectCode));
        }

        // 被检类型占位符替换（仅动态工装模板配置了 dutPlaceholder）
        var dutPlaceholder = opts.BusinessTemplate.DutPlaceholder;
        var hasDut = !string.IsNullOrWhiteSpace(dutPlaceholder);
        // dutValue：用户指定 --dut 时用用户值，否则默认 dutPlaceholder（TemplateUUT）
        var dutValue = string.IsNullOrWhiteSpace(opts.DutType)
            ? (hasDut ? dutPlaceholder! : "TemplateUUT")
            : opts.DutType;

        if (forRenaming)
        {
            // 文件/文件夹重命名：仅动态工装模板替换原始占位符字符串（如 TemplateUUT → PS02）
            if (hasDut)
                replacements.Add((dutPlaceholder!, dutValue));
        }
        else
        {
            // 文件内容替换：始终添加 {{DutType}} 令牌（模板中用 {{DutType}} 标记需替换的位置，
            // 外部库引用如 ConST171Base 保持原样不被替换）
            replacements.Add(("{{DutType}}", dutValue));
        }

        return replacements;
    }

    private static string ApplyReplacements(
        string value,
        IReadOnlyList<(string Token, string Value)> replacements)
    {
        foreach (var replacement in replacements)
        {
            // 项目代号占位符做边界感知替换（避免误伤 DataTemplate/ControlTemplate 等标识符）；
            // 其余令牌（{{...}}、dutPlaceholder 等）保持子串替换（如 TemplateUUTDut.cs → ConST221Dut.cs 需子串命中）
            if (PlaceholderBoundaryCache.TryGetValue(replacement.Token, out var rx))
                value = rx.Replace(value, replacement.Value);
            else
                value = value.Replace(replacement.Token, replacement.Value, StringComparison.Ordinal);
        }
        return value;
    }

    private static bool IsTextFile(string path)
        => TextExtensions.Contains(Path.GetExtension(path)) || TextFileNames.Contains(Path.GetFileName(path));

    private static bool TryReadUtf8(string path, out string content, out bool hasBom)
    {
        content = string.Empty;
        hasBom = false;
        try
        {
            var bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            content = StrictUtf8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// 判断文件是否为“纯转发到 Common”的构建配置文件：除注释外只包含
    /// &lt;Import Project="…Common…" /&gt;，不定义任何属性/项。
    /// 扁平化合并后这类文件无法工作（..\Common\ 不可达），应跳过。
    /// </summary>
    private static bool IsCommonForwardImport(string filePath)
    {
        if (!TryReadUtf8(filePath, out var content, out _)) return false;
        try
        {
            var root = XDocument.Parse(content, LoadOptions.PreserveWhitespace).Root;
            if (root == null || !root.Name.LocalName.Equals("Project", StringComparison.OrdinalIgnoreCase))
                return false;

            // Elements() 只返回元素子节点（自动忽略注释与空白文本节点）
            var elements = root.Elements().ToList();
            if (elements.Count == 0) return false;

            return elements.All(element =>
                element.Name.LocalName.Equals("Import", StringComparison.OrdinalIgnoreCase) &&
                element.Attribute("Project")?.Value.Contains("Common", StringComparison.OrdinalIgnoreCase) == true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将业务模板中指向业务目录外的相对引用重写为扁平化合并后的正确路径：
    /// 去掉一级 "..\"；Common 段一并删除（Common 项目与业务项目同处输出根下的 src\）。
    /// 例：sln 的 "..\Common\src\01.Core\…" → "src\01.Core\…"；
    ///     csproj 的 "..\..\..\..\Common\src\…" → "..\..\..\src\…"；
    ///     "..\..\..\..\README.md" → "..\..\..\README.md"。
    /// 业务目录内部的引用（如 src 内项目互引的 "..\..\04.TestSteps\…"）级数不足，
    /// 不匹配，保持不变。
    /// </summary>
    private static string RewriteCommonProjectReferences(string content)
    {
        content = CommonProjectReferencePattern.Replace(content, match =>
            ReduceUpLevels(match.Groups[1].Value));
        content = DeepUpwardReferencePattern.Replace(content, match =>
            ReduceUpLevels(match.Groups[1].Value));
        return content;
    }

    private static string ReduceUpLevels(string ups)
        => ups.Length > 3 ? ups[3..] : string.Empty; // 去掉一个 "..\"

    /// <summary>统计路径中的分隔符数量（用于估算深度）。</summary>
    private static int CountSeparators(string path)
    {
        var count = 0;
        foreach (var c in path)
            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                count++;
        return count;
    }

    /// <summary>移动/重命名失败时重试 —— 清除 read-only 属性后重试，处理 AccessDenied/IOException（杀软/索引器短暂锁文件）。</summary>
    private static void TryMoveWithRetry(Action moveAction, string path)
    {
        try
        {
            moveAction();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // 等待短暂时间后重试（杀毒软件/Windows 搜索索引器可能短暂锁文件）
            Thread.Sleep(200);

            // 清除 read-only 属性后重试
            try
            {
                if (File.Exists(path))
                {
                    var attrs = File.GetAttributes(path);
                    if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
                }
                else if (Directory.Exists(path))
                {
                    ClearReadOnlyAttributeRecursive(path);
                }
                moveAction();
            }
            catch (Exception ex2)
            {
                throw new IOException($"无法重命名（即使清除 read-only 并等待后仍失败）: {path} — {ex2.Message}", ex2);
            }
        }
    }

    /// <summary>递归清除目录及内容的 read-only 属性。</summary>
    private static void ClearReadOnlyAttributeRecursive(string dir)
    {
        try
        {
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }
            foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(d);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(d, attrs & ~FileAttributes.ReadOnly);
            }
            var dirAttrs = File.GetAttributes(dir);
            if ((dirAttrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(dir, dirAttrs & ~FileAttributes.ReadOnly);
        }
        catch { }
    }

    /// <summary>检查相对路径是否位于排除目录中。</summary>
    private static bool IsInExcludedPath(string relativePath, List<string> excludePaths)
        => IsExcludedPath(relativePath, excludePaths);

    private static bool IsExcludedPath(string relativePath, List<string> excludePaths)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var ex in excludePaths)
        {
            var normalized = ex.Replace('\\', '/').Trim('/');
            if (normalized.Contains('/'))
            {
                if (path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (segments.Any(segment => segment.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>安全枚举文件（容错，遇到访问拒绝跳过）。</summary>
    private static IEnumerable<string> EnumerateFilesSafe(string rootDir)
    {
        var stack = new Stack<string>();
        stack.Push(rootDir);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] files;
            try { files = Directory.GetFiles(current); }
            catch { continue; }
            foreach (var f in files) yield return f;

            string[] dirs;
            try { dirs = Directory.GetDirectories(current); }
            catch { continue; }
            foreach (var d in dirs) stack.Push(d);
        }
    }

    /// <summary>安全枚举目录（容错，按深度降序返回）。</summary>
    private static List<string> EnumerateDirectoriesSafe(string rootDir)
    {
        var result = new List<string>();
        var stack = new Stack<string>();
        stack.Push(rootDir);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] dirs;
            try { dirs = Directory.GetDirectories(current); }
            catch { continue; }
            foreach (var d in dirs)
            {
                result.Add(d);
                stack.Push(d);
            }
        }
        // 按路径长度降序：先处理深层目录
        result.Sort((a, b) => b.Length.CompareTo(a.Length));
        return result;
    }
}
