using System.Diagnostics;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// Obfuscar 混淆器 —— 检测 Obfuscar 工具路径、生成 Obfuscar XML 配置、生成 obfuscate.ps1 脚本。
/// 参考实现：E:\ExeBuilder\Services\BuildService.cs（BuildObfuscarXml、AddAssemblySearchPaths、TryGetPEInfo）。
///
/// 混淆策略：WPF 安全模式
///   - AnalyzeXaml=true：自动分析 BAML，跳过 XAML 引用的类型/属性/事件
///   - KeepPublicApi=true：保留公共 API（数据绑定依赖公共属性名）
///   - RenameProperties=false / RenameEvents=false：不重命名属性和事件
///   - HideStrings=true：字符串加密
///   - UseUnicodeNames=true：Unicode 字符名增加反编译难度
/// </summary>
public static class Obfuscator
{
    /// <summary>检测 Obfuscar 工具路径（5 级查找）。</summary>
    public static string? FindObfuscar()
    {
        try
        {
            // 1. PATH 中的 obfuscar.console.exe 或 obfuscar.globaltool
            var pathVars = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var dir in pathVars)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var name in new[] { "obfuscar.console.exe", "obfuscar.globaltool.exe", "obfuscar.exe" })
                {
                    var p = Path.Combine(dir.Trim('"'), name);
                    if (File.Exists(p)) return p;
                }
            }

            // 2. dotnet tool list -g 查找 Obfuscar.GlobalTool
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "tool list -g",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    if (output.Contains("obfuscar", StringComparison.OrdinalIgnoreCase))
                    {
                        // 全局工具默认路径
                        var toolPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".dotnet", "tools", "obfuscar.console.exe");
                        if (File.Exists(toolPath)) return toolPath;

                        // Linux/macOS 路径
                        toolPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".dotnet", "tools", "obfuscar");
                        if (File.Exists(toolPath)) return toolPath;
                    }
                }
            }
            catch { }

            // 3. %USERPROFILE%\.dotnet\tools\ 目录
            var userToolsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet", "tools");
            foreach (var name in new[] { "obfuscar.console.exe", "obfuscar.globaltool.exe", "obfuscar.exe" })
            {
                var p = Path.Combine(userToolsDir, name);
                if (File.Exists(p)) return p;
            }

            // 4. NuGet 全局包缓存
            try
            {
                var nugetCache = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "obfuscar");
                if (Directory.Exists(nugetCache))
                {
                    foreach (var verDir in Directory.GetDirectories(nugetCache))
                    {
                        var candidate = Path.Combine(verDir, "tools", "obfuscar.console.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>生成 Obfuscar XML 配置内容（WPF 安全混淆策略）。</summary>
    /// <param name="modules">要混淆的 DLL 文件名列表（如 PT01.Infrastructure.dll）。</param>
    /// <param name="outPath">Obfuscar 输出目录（绝对路径）。</param>
    /// <param name="inPath">输入目录（编译产物目录，绝对路径）。</param>
    public static string BuildObfuscarXml(List<string> modules, string outPath, string inPath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<Obfuscator>");

        // 路径配置
        sb.AppendLine($"  <Var name=\"InPath\" value=\"{EscapeXml(inPath)}\" />");
        sb.AppendLine($"  <Var name=\"OutPath\" value=\"{EscapeXml(outPath)}\" />");
        var cacheDir = Path.Combine(outPath, "_cache");
        Directory.CreateDirectory(cacheDir);
        sb.AppendLine($"  <Var name=\"CacheDir\" value=\"{EscapeXml(cacheDir)}\" />");
        sb.AppendLine("  <Var name=\"CopyOutputFilesToOutPath\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"SkipCFAnalysis\" value=\"true\" />");

        // ── WPF 安全混淆策略 ──
        sb.AppendLine("  <Var name=\"AnalyzeXaml\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"KeepPublicApi\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"HidePrivateApi\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"HidePrivateFields\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"RenameProperties\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"RenameEvents\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"RenameFields\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"RenameLocalVariables\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"RenameParameters\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"RenameGenericParameters\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"UseUnicodeNames\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"ReuseNames\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"HideStrings\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"OptimizeMethods\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"SkipGenerated\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"SkipSpecialName\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"SuppressIldasm\" value=\"true\" />");
        sb.AppendLine("  <Var name=\"EncryptResources\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"MarkedOnly\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"AntiDebug\" value=\"false\" />");
        sb.AppendLine("  <Var name=\"SkipVerified\" value=\"true\" />");

        // 添加 .NET 框架程序集搜索路径（关键，否则混淆会报找不到引用）
        AddAssemblySearchPaths(sb);

        // 模块列表
        sb.AppendLine();
        sb.AppendLine("  <!-- 待混淆模块 -->");
        foreach (var mod in modules)
        {
            sb.AppendLine($"  <Module file=\"{EscapeXml(mod)}\" />");
        }

        sb.AppendLine("</Obfuscator>");
        return sb.ToString();
    }

    /// <summary>自动添加 .NET 框架程序集搜索路径（参考 ExeBuilder）。</summary>
    private static void AddAssemblySearchPaths(System.Text.StringBuilder sb)
    {
        try
        {
            var dotnetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared");
            if (!Directory.Exists(dotnetRoot))
            {
                dotnetRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "dotnet", "shared");
            }

            if (!Directory.Exists(dotnetRoot)) return;

            var frameworkDirs = new[] { "Microsoft.WindowsDesktop.App", "Microsoft.NETCore.App" };
            foreach (var framework in frameworkDirs)
            {
                var frameworkPath = Path.Combine(dotnetRoot, framework);
                if (!Directory.Exists(frameworkPath)) continue;

                var versions = Directory.GetDirectories(frameworkPath)
                    .Select(d => Path.GetFileName(d) ?? "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var version in versions)
                {
                    var searchPath = Path.Combine(frameworkPath, version) + "/";
                    sb.AppendLine($"  <AssemblySearchPath path=\"{searchPath.Replace('\\', '/')}\" />");
                }
            }
        }
        catch { }
    }

    /// <summary>生成 obfuscate.ps1 脚本内容。</summary>
    public static string GenerateObfuscatePs1(BuildOptions opts)
    {
        var publishDir = opts.PublishDir.Replace('\\', '/'); // PS 路径用 / 更稳
        var buildDir = $"{opts.OutputDir.Replace('\\', '/')}/build".Replace('/', '\\'); // 用反斜杠
        var obfOutDir = Path.Combine(opts.OutputDir, "build", "obfuscated").Replace('\\', '/');
        var cacheDir = Path.Combine(opts.OutputDir, "build", "obfuscated", "_cache").Replace('\\', '/');

        var modules = opts.GetObfuscationTargetPaths()
            .Select(p => Path.GetFileName(p))
            .ToList();

        var xml = BuildObfuscarXml(modules, obfOutDir, publishDir);
        // PowerShell here-string @"..."@ 中 $ 仍会被插值，需转义为 `$；双引号无需转义
        var xmlPsEscape = xml.Replace("$", "`$");

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# ===== Obfuscar 混淆脚本（由 TestRig CLI 自动生成，请勿手动修改）=====");
        sb.AppendLine($"# 项目代号: {opts.ProjectCode}");
        sb.AppendLine($"# 版本: {opts.Version}");
        sb.AppendLine($"# 生成时间: {timestamp}");
        sb.AppendLine($"# 混淆目标: {string.Join(", ", modules)}");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = \"Stop\"");
        sb.AppendLine("$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path");
        sb.AppendLine($"$PublishDir = Join-Path $ProjectRoot \"src\\08.App\\{opts.MainProjectName}\\bin\\{opts.Template.Configuration}\\{opts.Template.TargetFramework}\"");
        sb.AppendLine("$ObfOutDir = Join-Path $ProjectRoot \"build\\obfuscated\"");
        sb.AppendLine("$BuildDir = Join-Path $ProjectRoot \"build\"");
        sb.AppendLine();
        sb.AppendLine("# ===== 辅助函数（必须在调用前定义，PowerShell 函数不会前置解析）=====");
        sb.AppendLine("function Find-Obfuscar {");
        sb.AppendLine("    # 1. PATH");
        sb.AppendLine("    $path = Get-Command obfuscar.console -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($path) { return $path.Source }");
        sb.AppendLine("    $path = Get-Command obfuscar -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($path) { return $path.Source }");
        sb.AppendLine("    # 2. dotnet tools");
        sb.AppendLine("    $toolsDir = Join-Path $env:USERPROFILE \".dotnet\\tools\"");
        sb.AppendLine("    foreach ($name in @(\"obfuscar.console.exe\", \"obfuscar.exe\")) {");
        sb.AppendLine("        $p = Join-Path $toolsDir $name");
        sb.AppendLine("        if (Test-Path $p) { return $p }");
        sb.AppendLine("    }");
        sb.AppendLine("    # 3. NuGet 全局包缓存");
        sb.AppendLine("    $nugetCache = Join-Path $env:USERPROFILE \".nuget\\packages\\obfuscar\"");
        sb.AppendLine("    if (Test-Path $nugetCache) {");
        sb.AppendLine("        foreach ($verDir in Get-ChildItem $nugetCache -Directory) {");
        sb.AppendLine("            $p = Join-Path $verDir.FullName \"tools\\obfuscar.console.exe\"");
        sb.AppendLine("            if (Test-Path $p) { return $p }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return $null");
        sb.AppendLine("}");
        sb.AppendLine();
        // 生成时已探测到的 Obfuscar 路径直接注入，避免运行时再查找
        var detectedObfuscar = FindObfuscar();
        if (!string.IsNullOrEmpty(detectedObfuscar))
            sb.AppendLine($"$DetectedObfuscar = \"{detectedObfuscar}\"");
        else
            sb.AppendLine("$DetectedObfuscar = $null");
        sb.AppendLine();
        sb.AppendLine("Write-Host \"====== 开始混淆 ======\" -ForegroundColor Cyan");
        sb.AppendLine();
        sb.AppendLine("# 1. 检测 Obfuscar（优先用生成时探测到的路径，其次运行时查找）");
        sb.AppendLine("$obfuscar = $DetectedObfuscar");
        sb.AppendLine("if (-not $obfuscar -or -not (Test-Path $obfuscar)) { $obfuscar = Find-Obfuscar }");
        sb.AppendLine("if (-not $obfuscar) {");
        sb.AppendLine("    Write-Host \"[ERROR] 未找到 Obfuscar 工具\" -ForegroundColor Red");
        sb.AppendLine("    Write-Host \"请安装: dotnet tool install -g Obfuscar.GlobalTool\" -ForegroundColor Yellow");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine("Write-Host \"[OK] Obfuscar: $obfuscar\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# 2. 验证待混淆的 DLL 存在");
        foreach (var mod in modules)
        {
            sb.AppendLine($"if (-not (Test-Path (Join-Path $PublishDir \"{mod}\"))) {{");
            sb.AppendLine($"    Write-Host \"[ERROR] 待混淆 DLL 不存在: {mod}\" -ForegroundColor Red");
            sb.AppendLine("    exit 1");
            sb.AppendLine("}");
        }
        sb.AppendLine();
        sb.AppendLine("# 3. 清理旧的混淆输出目录");
        sb.AppendLine("if (Test-Path $ObfOutDir) { Remove-Item $ObfOutDir -Recurse -Force }");
        sb.AppendLine("New-Item -ItemType Directory -Path $ObfOutDir -Force | Out-Null");
        sb.AppendLine();
        sb.AppendLine("# 4. 生成 obfuscar.xml");
        sb.AppendLine("$xml = @\"");
        sb.Append(xmlPsEscape);
        sb.AppendLine("\"@");
        sb.AppendLine("$xmlPath = Join-Path $BuildDir \"obfuscar.xml\"");
        sb.AppendLine("if (-not (Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null }");
        sb.AppendLine("$xml | Out-File $xmlPath -Encoding UTF8");
        sb.AppendLine("Write-Host \"[OK] Obfuscar XML: $xmlPath\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# 5. 执行混淆（工作目录设为 PublishDir）");
        sb.AppendLine("Push-Location $PublishDir");
        sb.AppendLine("try {");
        sb.AppendLine("    & $obfuscar $xmlPath");
        sb.AppendLine("    $exitCode = $LASTEXITCODE");
        sb.AppendLine("} finally {");
        sb.AppendLine("    Pop-Location");
        sb.AppendLine("}");
        sb.AppendLine("if ($exitCode -ne 0) {");
        sb.AppendLine("    Write-Host \"[ERROR] 混淆失败，退出码: $exitCode\" -ForegroundColor Red");
        sb.AppendLine("    exit $exitCode");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# 6. 覆盖回 publish 目录");
        sb.AppendLine("Write-Host \"[INFO] 覆盖混淆后的 DLL 到 publish 目录...\" -ForegroundColor Yellow");
        sb.AppendLine("Copy-Item \"$ObfOutDir\\*\" $PublishDir -Force -Recurse");
        sb.AppendLine();
        sb.AppendLine("Write-Host \"✓ 混淆完成\" -ForegroundColor Green");

        return sb.ToString();
    }

    private static string EscapeXml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");
}
