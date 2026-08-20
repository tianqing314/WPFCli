using System.Text.Json;
using Xunit;
using WPFCli.Cli;
using WPFCli.Engine;
using WPFCli.Models;

namespace WPFCli.Tests;

public sealed class CoreFlowTests
{
    [Fact]
    public void Template_builder_merges_business_files_and_replaces_project_placeholder()
    {
        var root = CreateTempDirectory();
        try
        {
            var templateRoot = Path.Combine(root, "Template");
            var common = Path.Combine(templateRoot, "Common");
            var business = Path.Combine(templateRoot, "Complete");
            var output = Path.Combine(root, "Output", "PT01");
            Directory.CreateDirectory(Path.Combine(common, "src", "08.App", "PCBA.App"));
            Directory.CreateDirectory(business);
            File.WriteAllText(Path.Combine(common, "PCBA.sln"), "Project PCBA");
            File.WriteAllText(
                Path.Combine(common, "src", "08.App", "PCBA.App", "PCBA.App.csproj"),
                "<Project><PropertyGroup><AssemblyName>PCBA.App</AssemblyName></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(business, "business.txt"), "PCBA complete");

            var config = new TemplateConfig { Placeholder = "PCBA", MainProjectName = "PCBA.App" };
            TemplateBuilder.Build(new BuildOptions
            {
                ProjectPrefix = "PT01",
                ProductCode = "PS02",
                OutputDir = output,
                TemplatePath = templateRoot,
                BusinessTemplatePath = business,
                Template = config,
                BusinessTemplate = new TemplateConfig()
            });

            Assert.True(File.Exists(Path.Combine(output, "PT01.sln")));
            Assert.True(File.Exists(Path.Combine(output, "src", "08.App", "PT01.App", "PT01.App.csproj")));
            Assert.Equal("PT01 complete", File.ReadAllText(Path.Combine(output, "business.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Cli_parser_validates_business_and_project_code_without_running_pipeline()
    {
        var workspace = LocateWorkspace();
        var templatePath = Path.Combine(workspace, "Template");
        var config = JsonSerializer.Deserialize<TemplateConfig>(
            File.ReadAllText(Path.Combine(templatePath, "template.config.json")))!;

        var result = CliOptionsParser.Parse(
            ["--biz", "machine", "--code", "PT01"],
            config,
            workspace,
            templatePath);

        Assert.True(result.IsSuccess);
        Assert.Equal("PT01", result.Options!.ProjectPrefix);
        Assert.Equal("PT01", result.Options!.ProductCode);
    }

    [Fact]
    public void Cli_parser_does_not_consume_the_next_flag_as_a_missing_value()
    {
        var result = CliOptionsParser.Parse(
            ["--biz", "--code", "PT01"],
            new TemplateConfig(),
            "C:\\workspace",
            "C:\\workspace\\Template");

        Assert.False(result.IsSuccess);
        Assert.Equal("--biz 缺少参数值", result.Error);
    }

    private static string LocateWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Template")))
            directory = directory.Parent!;
        return directory?.FullName ?? throw new DirectoryNotFoundException("无法定位测试工作区");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testrig-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
