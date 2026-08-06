using WPFCli.Cli;
using WPFCli.Engine;
using WPFCli.Models;

namespace WPFCli;

/// <summary>
/// CLI 入口：定位工作区、加载模板配置、解析输入，然后交给 <see cref="BuildPipeline"/>。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var bootstrap = CliOptionsParser.ParseBootstrap(args);
        if (!bootstrap.IsSuccess)
        {
            WriteError(bootstrap.Error!);
            return 1;
        }

        TemplateLocation location;
        TemplateConfig templateConfig;
        IReadOnlyList<BusinessTemplateDescriptor> templates;
        try
        {
            location = ResolveTemplateLocation(bootstrap.TemplateRoot);
            templateConfig = TemplateCatalog.LoadRoot(location.TemplatePath);
            templates = TemplateCatalog.Discover(location.TemplatePath);
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }

        BuildOptions? options;
        if (args.Length == 0)
        {
            options = InteractiveWizard.Run(templateConfig, location.WorkspaceRoot, location.TemplatePath);
            if (options == null) return 0;
        }
        else
        {
            var parsed = CliOptionsParser.Parse(args, templateConfig, location.WorkspaceRoot, location.TemplatePath);
            if (parsed.IsHelp)
            {
                PrintUsage();
                return 0;
            }
            if (parsed.IsListTemplates)
            {
                PrintTemplates(templates);
                return 0;
            }

            if (!parsed.IsSuccess)
            {
                WriteError(parsed.Error!);
                PrintUsage();
                return 1;
            }

            options = parsed.Options!;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ▸ CLI 参数模式");
            Console.ResetColor();
            InteractiveWizard.PrintSummary(options);
        }

        return new BuildPipeline().Run(options);
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("  用法:");
        Console.WriteLine("    testrig-cli                              # 交互式向导");
        Console.WriteLine("    testrig-cli --biz <类型> --code <代号> [选项]");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  参数:");
        Console.WriteLine("    --biz <类型>                             业务类型（必填）");
        Console.WriteLine("    --code <代号>                            项目代号（必填，2-20 位字母数字）");
        Console.WriteLine("    --list-templates                         列出当前模板目录中的业务模板");
        Console.WriteLine("    --template-root <目录>                   指定模板根目录");
        Console.WriteLine("    --output <目录>                          指定输出目录");
        Console.WriteLine("    --version <版本>                         指定版本，默认模板 patch +1");
        Console.WriteLine("    --dut <被检类型>                         被检类型（如 PS02），仅动态工装模板");
        Console.WriteLine("    --references-root <目录>                  References 适配根目录（默认模板根同级 References）");
        Console.WriteLine("    --force                                  完整构建成功后替换已有输出");
        Console.WriteLine("    --dry-run                                在临时目录预演，不发布输出");
        Console.WriteLine("    --no-build                               只生成，不执行 dotnet build");
        Console.WriteLine("    --obfuscate / --no-obfuscate             是否混淆（默认否）");
        Console.WriteLine("    --pack / --no-pack                       是否生成安装包（默认否）");
        Console.WriteLine("    --gitlab <URL>                           GitLab 仓库地址");
        Console.WriteLine("    --ftp-host <地址> --ftp-dir <目录>         FTP 地址和远程目录");
        Console.WriteLine("                                             凭据在发布时从 TESTRIG_FTP_USER /");
        Console.WriteLine("                                             TESTRIG_FTP_PASSWORD 环境变量读取");
        Console.WriteLine("    --help / -h                              显示帮助");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static TemplateLocation ResolveTemplateLocation(string? explicitRoot)
    {
        var currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var templatePath = Path.GetFullPath(explicitRoot, currentDirectory);
            if (!Directory.Exists(templatePath))
                throw new DirectoryNotFoundException($"模板根目录不存在: {templatePath}");
            return new TemplateLocation(currentDirectory, templatePath);
        }

        var repositoryTemplate = FindTemplateUpward(currentDirectory);
        if (repositoryTemplate != null)
            return new TemplateLocation(Directory.GetParent(repositoryTemplate)!.FullName, repositoryTemplate);

        var packagedTemplate = Path.Combine(AppContext.BaseDirectory, "Template");
        if (Directory.Exists(packagedTemplate))
            return new TemplateLocation(currentDirectory, packagedTemplate);

        var appTemplate = FindTemplateUpward(AppContext.BaseDirectory);
        if (appTemplate != null)
            return new TemplateLocation(currentDirectory, appTemplate);

        throw new DirectoryNotFoundException(
            "无法定位模板。请确认工具包包含 Template，或使用 --template-root <目录> 指定。");
    }

    private static string? FindTemplateUpward(string start)
    {
        var directory = Path.GetFullPath(start);
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "Template");
            if (File.Exists(Path.Combine(candidate, "template.config.json"))) return candidate;
            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == null || parent == directory) break;
            directory = parent;
        }
        return null;
    }

    private static void PrintTemplates(IReadOnlyList<BusinessTemplateDescriptor> templates)
    {
        Console.WriteLine();
        Console.WriteLine("  可用业务模板:");
        foreach (var template in templates)
        {
            var state = template.Config.Disabled ? "disabled" : "enabled";
            Console.WriteLine($"    {template.Config.BusinessType,-12} [{state,-8}] {template.Config.Description}");
        }
        Console.WriteLine();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERROR] {message}");
        Console.ResetColor();
    }

    private sealed record TemplateLocation(string WorkspaceRoot, string TemplatePath);
}
