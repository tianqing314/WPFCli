using WPFCli.Engine;
using WPFCli.Models;

namespace WPFCli.Cli;

/// <summary>
/// 解析非交互 CLI 参数。解析器不写控制台，便于入口和测试复用。
/// </summary>
public static class CliOptionsParser
{
    public static BootstrapParseResult ParseBootstrap(string[] args)
    {
        string? templateRoot = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--template-root") continue;
            templateRoot = NextValue(args, ref i);
            if (string.IsNullOrWhiteSpace(templateRoot))
                return new BootstrapParseResult(null, "--template-root 缺少目录参数");
        }
        return new BootstrapParseResult(templateRoot, null);
    }

    public static CliParseResult Parse(
        string[] args,
        TemplateConfig templateConfig,
        string workspaceRoot,
        string templatePath)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(templateConfig);

        if (args.Any(a => a is "--help" or "-h" or "-?"))
            return CliParseResult.Help();
        if (args.Any(a => a == "--list-templates"))
            return CliParseResult.ListTemplates();

        string? business = null;
        string? code = null;
        string? output = null;
        string? version = null;
        string? gitLabUrl = null;
        string? ftpHost = null;
        string? ftpDirectory = null;
        string? dutType = null;
        string? referencesRoot = null;
        bool? obfuscate = null;
        bool? package = null;
        var overwriteExisting = false;
        var dryRun = false;
        var skipBuild = false;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            switch (argument)
            {
                case "--biz": business = NextValue(args, ref i); break;
                case "--code": code = NextValue(args, ref i); break;
                case "--template-root":
                    if (NextValue(args, ref i) == null)
                        return CliParseResult.Failure("--template-root 缺少目录参数");
                    break;
                case "--output": output = NextValue(args, ref i); break;
                case "--version": version = NextValue(args, ref i); break;
                case "--dut": dutType = NextValue(args, ref i); break;
                case "--references-root": referencesRoot = NextValue(args, ref i); break;
                case "--force": overwriteExisting = true; break;
                case "--dry-run": dryRun = true; break;
                case "--no-build": skipBuild = true; break;
                case "--obfuscate": obfuscate = true; break;
                case "--no-obfuscate": obfuscate = false; break;
                case "--pack": package = true; break;
                case "--no-pack": package = false; break;
                case "--gitlab": gitLabUrl = NextValue(args, ref i); break;
                case "--ftp-host": ftpHost = NextValue(args, ref i); break;
                case "--ftp-dir": ftpDirectory = NextValue(args, ref i); break;
                default: return CliParseResult.Failure($"未知参数: {argument}");
            }
        }

        foreach (var missing in new[]
                 {
                     ("--biz", business), ("--code", code), ("--output", output), ("--version", version),
                     ("--dut", dutType), ("--references-root", referencesRoot),
                     ("--gitlab", gitLabUrl), ("--ftp-host", ftpHost), ("--ftp-dir", ftpDirectory)
                 })
        {
            if (args.Contains(missing.Item1, StringComparer.Ordinal) && missing.Item2 == null)
                return CliParseResult.Failure($"{missing.Item1} 缺少参数值");
        }

        if (string.IsNullOrWhiteSpace(business))
            return CliParseResult.Failure("缺少必填参数 --biz <业务类型>");
        if (string.IsNullOrWhiteSpace(code))
            return CliParseResult.Failure("缺少必填参数 --code <项目代号>");

        var businessError = TemplateCatalog.TryResolve(templatePath, business, out var businessTemplate);
        if (businessError != null)
            return CliParseResult.Failure(businessError);

        var codeError = InteractiveWizard.ValidateProjectCode(templateConfig, code);
        if (codeError != null)
            return CliParseResult.Failure($"代号 \"{code}\" 非法：{codeError}");
        if (!string.IsNullOrWhiteSpace(version) && !VersionManager.IsValidVersion(version))
            return CliParseResult.Failure($"版本号 \"{version}\" 非法，应为 major.minor.patch[.revision]");
        if (!string.IsNullOrWhiteSpace(gitLabUrl) && !IsAbsoluteUri(gitLabUrl, "http", "https"))
            return CliParseResult.Failure("--gitlab 必须是有效的 HTTP/HTTPS 仓库地址");
        if (!string.IsNullOrWhiteSpace(ftpHost) && !IsAbsoluteUri(ftpHost, "ftp", "ftps"))
            return CliParseResult.Failure("--ftp-host 必须是有效的 FTP/FTPS 地址");
        if (!string.IsNullOrWhiteSpace(ftpDirectory) && string.IsNullOrWhiteSpace(ftpHost))
            return CliParseResult.Failure("使用 --ftp-dir 时必须同时提供 --ftp-host");

        string outputDir;
        try
        {
            outputDir = string.IsNullOrWhiteSpace(output)
                ? Path.Combine(workspaceRoot, "Output", code)
                : Path.GetFullPath(output, workspaceRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CliParseResult.Failure($"输出目录无效: {ex.Message}");
        }

        return CliParseResult.Success(new BuildOptions
        {
            Template = templateConfig,
            TemplatePath = templatePath,
            BusinessTemplatePath = businessTemplate!.DirectoryPath,
            BusinessTemplate = businessTemplate.Config,
            ProjectCode = code,
            OutputDir = outputDir,
            Version = version ?? string.Empty,
            OverwriteExisting = overwriteExisting,
            DryRun = dryRun,
            SkipBuild = skipBuild,
            EnableObfuscation = obfuscate ?? false,
            EnablePackaging = package ?? false,
            EnableGitLab = !string.IsNullOrWhiteSpace(gitLabUrl),
            GitLabRepoUrl = gitLabUrl ?? string.Empty,
            EnableFtp = !string.IsNullOrWhiteSpace(ftpHost),
            FtpHost = ftpHost ?? string.Empty,
            FtpRemoteDir = ftpDirectory ?? string.Empty,
            DutType = dutType ?? string.Empty,
            ReferencesRoot = referencesRoot ?? string.Empty
        });
    }

    private static string? NextValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length) return null;
        var value = args[index + 1];
        if (value.StartsWith("--", StringComparison.Ordinal)) return null;
        index++;
        return value;
    }

    private static bool IsAbsoluteUri(string value, params string[] schemes)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}

public sealed record BootstrapParseResult(string? TemplateRoot, string? Error)
{
    public bool IsSuccess => Error == null;
}

public sealed record CliParseResult(BuildOptions? Options, bool IsHelp, bool IsListTemplates, string? Error)
{
    public bool IsSuccess => Options != null;
    public static CliParseResult Success(BuildOptions options) => new(options, false, false, null);
    public static CliParseResult Help() => new(null, true, false, null);
    public static CliParseResult ListTemplates() => new(null, false, true, null);
    public static CliParseResult Failure(string message) => new(null, false, false, message);
}
