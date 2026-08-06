using System.Text.Json;
using Xunit;
using WPFCli.Cli;
using WPFCli.Engine;
using WPFCli.Models;

namespace WPFCli.Tests;

public sealed class CoreFlowTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.2", "1.2.1")]
    [InlineData("1.2.3.4", "1.2.4.0")]
    public void IncrementPatch_handles_supported_versions(string current, string expected)
        => Assert.Equal(expected, VersionManager.IncrementPatch(current));

    [Fact]
    public void WriteVersion_updates_msbuild_and_audit_metadata_atomically()
    {
        var root = CreateTempDirectory();
        try
        {
            var props = Path.Combine(root, "Directory.Build.props");
            File.WriteAllText(props, """
                <Project><PropertyGroup><Version>1.0.0</Version><AssemblyVersion>1.0.0.0</AssemblyVersion></PropertyGroup></Project>
                """);

            VersionManager.WriteVersion(root, "1.2.3", "PT01", "1.2.2");
            var text = File.ReadAllText(props);
            Assert.Contains("<Version>1.2.3</Version>", text);
            Assert.Contains("<AssemblyVersion>1.2.3.0</AssemblyVersion>", text);
            Assert.Contains("<BuildProjectCode>PT01</BuildProjectCode>", text);
            Assert.Contains("<BuildBaseVersion>1.2.2</BuildBaseVersion>", text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Upload_scripts_require_runtime_environment_credentials()
    {
        var root = CreateTempDirectory();
        try
        {
            var options = new BuildOptions
            {
                ProjectCode = "PT01",
                OutputDir = root,
                Version = "1.0.1",
                EnableFtp = true,
                EnableGitLab = true,
                GitLabRepoUrl = "https://gitlab.example.invalid/group/PT01.git",
                FtpHost = "ftp://example.invalid",
                FtpRemoteDir = "release",
                Template = new TemplateConfig
                {
                    Configuration = "Release",
                    TargetFramework = "net8.0-windows",
                    MainProjectName = "PT01.App"
                }
            };

            UploadScriptGenerator.Generate(options);
            var scripts = Directory.EnumerateFiles(root, "*.ps1", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(root, "*.yml", SearchOption.AllDirectories))
                .Select(File.ReadAllText)
                .ToArray();

            Assert.NotEmpty(scripts);
            Assert.All(scripts, script =>
            {
                Assert.Contains("TESTRIG_FTP_PASSWORD", script);
                Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
                ProjectCode = "PT01",
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
            ["--biz", "machine", "--code", "PT01", "--ftp-host", "ftp://example.invalid"],
            config,
            workspace,
            templatePath);

        Assert.True(result.IsSuccess);
        Assert.Equal("PT01", result.Options!.ProjectCode);
        Assert.True(result.Options.EnableFtp);
        Assert.Equal("ftp://example.invalid", result.Options.FtpHost);
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
