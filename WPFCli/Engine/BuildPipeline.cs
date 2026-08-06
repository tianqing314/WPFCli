using System.Diagnostics;
using System.Text;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// CLI 构建流水线：环境检查、版本准备、模板生成、脚本生成、编译和产物汇总。
/// 入口只负责准备 BuildOptions，所有构建阶段在此集中编排。
/// </summary>
public sealed class BuildPipeline
{
    /// <summary>执行一次完整构建并返回进程退出码。</summary>
    public int Run(BuildOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        var finalOutput = Path.GetFullPath(opts.OutputDir);
        string stagingOutput;
        try
        {
            ValidateOutputTarget(finalOutput, opts.TemplatePath);
            if (Directory.Exists(finalOutput) && !opts.OverwriteExisting && !opts.DryRun)
            {
                WriteError($"输出目录已存在，使用 --force 才能在完整构建成功后替换: {finalOutput}");
                return 1;
            }

            var parent = Directory.GetParent(finalOutput)?.FullName
                ?? throw new InvalidOperationException($"输出目录不能是磁盘根目录: {finalOutput}");
            Directory.CreateDirectory(parent);
            stagingOutput = Path.Combine(parent, $".{Path.GetFileName(finalOutput)}.pipeline-{Guid.NewGuid():N}");

            // 清理上次失败遗留的 staging/backup 目录（匹配 .{outputName}.pipeline-* / .staging-* / .backup-*）
            CleanupStaleTempDirs(parent, Path.GetFileName(finalOutput));
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }

        Console.WriteLine();
        WriteHeading("环境检测");
        var envResult = EnvironmentChecker.Check(opts);
        if (!envResult.DotnetAvailable || !envResult.SdkVersionSufficient)
        {
            WriteError("构建中止：缺少 .NET 8+ SDK（模板目标框架需要 .NET 8）");
            return 1;
        }

        var commonPath = Path.Combine(opts.TemplatePath, "Common");
        var baseVersion = VersionManager.DetectVersion(commonPath, opts.Template.MainProjectName) ?? "1.0.0";
        opts.Version = string.IsNullOrWhiteSpace(opts.Version)
            ? VersionManager.IncrementPatch(baseVersion)
            : opts.Version;

        Console.WriteLine();
        WriteHeading("版本号管理");
        WriteSuccess($"版本号: {baseVersion} -> {opts.Version}");

        Console.WriteLine();
        WriteHeading("开始构建");

        var totalSw = Stopwatch.StartNew();
        var stepSw = new Stopwatch();
        var compileSuccess = opts.SkipBuild || opts.DryRun;
        var step = 0;

        try
        {
            opts.OutputDir = stagingOutput;
            RunStep(++step, "拷贝模板并替换占位符", stepSw, () =>
            {
                TemplateBuilder.Build(opts, message => Console.WriteLine($"    {message}"));
                var issues = TemplateBuilder.RunPostBuildChecks(opts);
                foreach (var issue in issues)
                {
                    WriteError($"[自检] {issue}");
                    throw new InvalidOperationException("生成后自检失败");
                }
            });

            RunStep(++step, "写入版本号", stepSw, () =>
            {
                VersionManager.WriteVersion(opts.OutputDir, opts.Version,
                    projectCode: opts.ProjectCode, baseVersion: baseVersion);
                Console.WriteLine($"    版本号写入: {opts.Version}");
                Console.WriteLine($"    审计元数据: ProjectCode={opts.ProjectCode}, BaseVersion={baseVersion}");
            });

            if (UploadScriptGenerator.IsEnabled(opts))
            {
                RunStep(++step, "生成上传方案脚本", stepSw, () =>
                    UploadScriptGenerator.Generate(opts, message => Console.WriteLine($"    {message}")));
            }

            if (opts.EnableObfuscation)
            {
                RunStep(++step, "生成混淆脚本", stepSw, () =>
                {
                    var path = Path.Combine(opts.OutputDir, "obfuscate.ps1");
                    File.WriteAllText(path, Obfuscator.GenerateObfuscatePs1(opts), new UTF8Encoding(false));
                    Console.WriteLine($"    脚本已生成: {path}");
                });
            }

            if (opts.EnablePackaging)
            {
                RunStep(++step, "生成打包脚本", stepSw, () =>
                {
                    var path = Path.Combine(opts.OutputDir, "package.ps1");
                    File.WriteAllText(path, InstallerPackager.GeneratePackagePs1(opts), new UTF8Encoding(false));
                    Console.WriteLine($"    脚本已生成: {path}");
                });
            }

            if (!opts.SkipBuild && !opts.DryRun)
            {
                RunStep(++step, "编译项目", stepSw, () =>
                {
                    var exitCode = ProjectCompiler.CompileAsync(opts, WriteCompilerLine)
                        .GetAwaiter().GetResult();
                    compileSuccess = exitCode == 0;
                    if (!compileSuccess)
                        throw new InvalidOperationException($"dotnet build 失败，退出码: {exitCode}");
                });
            }

            totalSw.Stop();
            if (opts.DryRun)
            {
                opts.OutputDir = finalOutput;
                WriteSuccess($"预演通过，正式输出未修改: {finalOutput}");
                return 0;
            }

            if (!compileSuccess) return 2;
            PublishStagingOutput(stagingOutput, finalOutput, opts.OverwriteExisting);
            opts.OutputDir = finalOutput;
            PrintBuildSuccess(totalSw.ElapsedMilliseconds);
            PrintArtifacts(opts);
            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            PrintBuildFailure();
            return 1;
        }
        finally
        {
            opts.OutputDir = finalOutput;
            TryDeleteDirectory(stagingOutput);
        }
    }

    /// <summary>清理上次失败遗留的临时目录（.pipeline-*/.staging-*/.backup-*）。</summary>
    private static void CleanupStaleTempDirs(string parent, string outputName)
    {
        try
        {
            var prefixes = new[] { $".{outputName}.pipeline-", $".{outputName}.staging-", $".{outputName}.backup-" };
            foreach (var dir in Directory.GetDirectories(parent))
            {
                var name = Path.GetFileName(dir);
                if (prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    TryDeleteDirectory(dir);
            }
        }
        catch { /* 清理失败不阻塞构建 */ }
    }

    private static void ValidateOutputTarget(string outputPath, string templatePath)
    {
        var root = Path.GetPathRoot(outputPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedOutput = outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(root) || normalizedOutput.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"输出目录不能是磁盘根目录: {outputPath}");
        if (File.Exists(outputPath))
            throw new IOException($"输出路径已被文件占用: {outputPath}");
        if (Directory.Exists(Path.Combine(outputPath, ".git")))
            throw new InvalidOperationException($"拒绝替换 Git 仓库目录: {outputPath}");
        if (Directory.Exists(outputPath) &&
            (File.GetAttributes(outputPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"拒绝替换链接目录: {outputPath}");

        var templateRoot = Path.GetFullPath(templatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsSameOrChild(normalizedOutput, templateRoot) || IsSameOrChild(templateRoot, normalizedOutput))
            throw new InvalidOperationException("输出目录不能与模板目录重叠。");
    }

    private static bool IsSameOrChild(string path, string parent)
        => path.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void PublishStagingOutput(string stagingOutput, string finalOutput, bool overwrite)
    {
        var parent = Directory.GetParent(finalOutput)!.FullName;
        var backup = Path.Combine(parent, $".{Path.GetFileName(finalOutput)}.backup-{Guid.NewGuid():N}");
        var movedExisting = false;

        if (Directory.Exists(finalOutput))
        {
            if (!overwrite) throw new IOException($"输出目录在构建期间出现，未发布: {finalOutput}");
            Directory.Move(finalOutput, backup);
            movedExisting = true;
        }

        try
        {
            Directory.Move(stagingOutput, finalOutput);
            if (movedExisting) TryDeleteDirectory(backup);
        }
        catch
        {
            if (!Directory.Exists(finalOutput) && movedExisting && Directory.Exists(backup))
                Directory.Move(backup, finalOutput);
            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不覆盖构建结果；路径包含随机后缀，后续可人工清理。
        }
    }

    private static void WriteCompilerLine(string line)
    {
        if (!line.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("Build succeeded", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("警告", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("Warning", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("执行混淆脚本", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("执行打包脚本", StringComparison.OrdinalIgnoreCase)) return;

        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) WriteError(line);
        else Console.WriteLine($"    {line}");
    }

    private static void RunStep(int step, string label, Stopwatch sw, Action action)
    {
        sw.Restart();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{step}] ");
        Console.ResetColor();
        Console.Write(label);
        try
        {
            action();
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" ✓");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {sw.ElapsedMilliseconds}ms");
            Console.ResetColor();
        }
        catch
        {
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(" ✗");
            Console.ResetColor();
            throw;
        }
    }

    private static void WriteHeading(string text)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  ▸ {text}");
        Console.ResetColor();
    }

    private static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [✓] {text}");
        Console.ResetColor();
    }

    private static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"    [WARNING] {text}");
        Console.ResetColor();
    }

    private static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"    [ERROR] {text}");
        Console.ResetColor();
    }

    private static void PrintBuildSuccess(long elapsedMs)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  +--------------------------------------------------+");
        Console.WriteLine("  |  ✓ 项目构建成功                                  |");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  |  耗时: {elapsedMs}ms".PadRight(51) + "|");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  +--------------------------------------------------+");
        Console.ResetColor();
    }

    private static void PrintBuildFailure()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  [!] 构建失败，请检查上方编译或脚本日志。");
        Console.WriteLine("      修复后可进入 Output 目录重新执行 dotnet build。");
        Console.ResetColor();
    }

    private static void PrintArtifacts(BuildOptions opts)
    {
        Console.WriteLine();
        WriteHeading("产物清单");
        PrintArtifact("解决方案", opts.SolutionPath);
        PrintArtifact("编译产物目录", opts.PublishDir);
        PrintArtifact("主程序", Path.Combine(opts.PublishDir, opts.MainExeFileName));

        if (opts.EnableObfuscation)
            PrintArtifact("混淆脚本", Path.Combine(opts.OutputDir, "obfuscate.ps1"));
        if (opts.EnablePackaging)
        {
            PrintArtifact("打包脚本", Path.Combine(opts.OutputDir, "package.ps1"));
            PrintArtifact("安装包目录", Path.Combine(opts.OutputDir, "installer"));
        }

        if (opts.EnableGitLab && opts.EnableFtp)
        {
            PrintArtifact("GitLab CI", Path.Combine(opts.OutputDir, ".gitlab-ci.yml"));
            PrintArtifact("发布脚本", Path.Combine(opts.OutputDir, "upgrade", "publish.ps1"));
            PrintArtifact("版本同步", Path.Combine(opts.OutputDir, "upgrade", "ci_update_versions.ps1"));
            PrintArtifact("本地模拟", Path.Combine(opts.OutputDir, "upgrade", "local_test_ci.ps1"));
            PrintArtifact("推送脚本", Path.Combine(opts.OutputDir, "push_gitlab.ps1"));
            PrintArtifact("版本文件", Path.Combine(opts.OutputDir, "AutoDeployConfig.xml"));
        }
        else if (opts.EnableGitLab)
        {
            PrintArtifact("GitLab CI", Path.Combine(opts.OutputDir, ".gitlab-ci.yml"));
            PrintArtifact("推送脚本", Path.Combine(opts.OutputDir, "push_gitlab.ps1"));
        }
        else if (opts.EnableFtp)
        {
            PrintArtifact("FTP 发布脚本", Path.Combine(opts.OutputDir, "publish_ftp.ps1"));
            PrintArtifact("版本文件", Path.Combine(opts.OutputDir, "AutoDeployConfig.xml"));
        }
    }

    private static void PrintArtifact(string label, string path)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        Console.ForegroundColor = exists ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.Write(exists ? "    [✓]" : "    [ ]");
        Console.ResetColor();
        Console.WriteLine($" {label,-12} {path}");
    }
}
