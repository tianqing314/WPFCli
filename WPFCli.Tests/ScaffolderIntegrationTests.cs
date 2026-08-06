using WPFCli.Engine;
using WPFCli.Models;
using Xunit;

namespace WPFCli.Tests;

public sealed class ScaffolderIntegrationTests
{
    public static IEnumerable<object[]> EnabledBusinessTemplates()
    {
        var templateRoot = Path.Combine(LocateWorkspace(), "Template");
        return TemplateCatalog.Discover(templateRoot)
            .Where(template => !template.Config.Disabled)
            .Select(template => new object[] { template.DirectoryName });
    }

    [Theory]
    [MemberData(nameof(EnabledBusinessTemplates))]
    public void Every_enabled_business_template_generates_a_valid_project(string businessName)
    {
        var workspace = LocateWorkspace();
        var templateRoot = Path.Combine(workspace, "Template");
        var rootConfig = TemplateCatalog.LoadRoot(templateRoot);
        Assert.Null(TemplateCatalog.TryResolve(templateRoot, businessName, out var business));
        var output = CreateTempDirectory();
        Directory.Delete(output);

        try
        {
            var options = CreateOptions(rootConfig, templateRoot, business!, output);
            TemplateBuilder.Build(options);

            Assert.Empty(TemplateBuilder.RunPostBuildChecks(options));
            // 构建流水线 [2] 步骤：合并后的根 props 必须能被版本管理更新（Dynamic 的转发 props 不得覆盖 Common 版本）
            VersionManager.WriteVersion(output, "1.2.3", projectCode: "PT01", baseVersion: "1.0.0");
            Assert.Contains("<Version>1.2.3</Version>", File.ReadAllText(Path.Combine(output, "Directory.Build.props")));
            Assert.True(File.Exists(options.SolutionPath));
            Assert.True(Directory.Exists(Path.Combine(output, "src", "07.UI")));
            Assert.True(Directory.Exists(Path.Combine(output, "src", "08.App")));
            Assert.False(Directory.Exists(Path.Combine(output, "src", "libs", "DeviceLink", "tests")));
            Assert.False(Directory.Exists(Path.Combine(output, "src", "libs", "DeviceLink", "docs")));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Template_builder_supports_explicit_tokens_deletes_and_preserves_unknown_binary()
    {
        var root = CreateMinimalTemplate(includeRequiredFiles: true);
        var output = Path.Combine(Path.GetDirectoryName(root)!, $"output-{Guid.NewGuid():N}");
        try
        {
            var rootConfig = TemplateCatalog.LoadRoot(root);
            Assert.Null(TemplateCatalog.TryResolve(root, "complete", out var business));
            business!.Config.DeleteFromOutput.Add("remove.txt");
            File.WriteAllText(Path.Combine(root, "Common", "remove.txt"), "remove me");
            File.WriteAllText(Path.Combine(root, "Common", "{{ProjectCode}}.txt"),
                "{{ProjectCode}}|{{MainProjectName}}|{{Version}}|PCBA_suffix");
            var binary = new byte[] { 0xFF, 0xFE, 0x50, 0x43, 0x42, 0x41 };
            File.WriteAllBytes(Path.Combine(root, "Common", "payload.dat"), binary);

            var options = CreateOptions(rootConfig, root, business, output);
            TemplateBuilder.Build(options);

            Assert.False(File.Exists(Path.Combine(output, "remove.txt")));
            Assert.Equal("PT01|PT01.App|1.2.3|PT01_suffix",
                File.ReadAllText(Path.Combine(output, "PT01.txt")));
            Assert.Equal(binary, File.ReadAllBytes(Path.Combine(output, "payload.dat")));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Pipeline_failure_preserves_existing_output()
    {
        var root = CreateMinimalTemplate(includeRequiredFiles: false);
        var output = Path.Combine(Path.GetDirectoryName(root)!, "existing-output");
        Directory.CreateDirectory(output);
        var marker = Path.Combine(output, "keep.txt");
        File.WriteAllText(marker, "original");

        try
        {
            var rootConfig = TemplateCatalog.LoadRoot(root);
            Assert.Null(TemplateCatalog.TryResolve(root, "complete", out var business));
            var options = CreateOptions(rootConfig, root, business!, output);
            options.OverwriteExisting = true;
            options.SkipBuild = true;

            Assert.Equal(1, new BuildPipeline().Run(options));
            Assert.Equal("original", File.ReadAllText(marker));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Fact]
    public void Dry_run_does_not_publish_or_mutate_template_version()
    {
        var root = CreateMinimalTemplate(includeRequiredFiles: true);
        var output = Path.Combine(Path.GetDirectoryName(root)!, "dry-output");
        var props = Path.Combine(root, "Common", "Directory.Build.props");
        var originalProps = File.ReadAllText(props);

        try
        {
            var rootConfig = TemplateCatalog.LoadRoot(root);
            Assert.Null(TemplateCatalog.TryResolve(root, "complete", out var business));
            var options = CreateOptions(rootConfig, root, business!, output);
            options.DryRun = true;

            Assert.Equal(0, new BuildPipeline().Run(options));
            Assert.False(Directory.Exists(output));
            Assert.Equal(originalProps, File.ReadAllText(props));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Fact]
    public void Business_forward_import_props_keep_common_version_and_write_version_succeeds()
    {
        var root = CreateMinimalTemplate(includeRequiredFiles: true);
        var output = Path.Combine(Path.GetDirectoryName(root)!, $"output-{Guid.NewGuid():N}");
        try
        {
            // 业务模板根放一个只转发到 Common 的 Directory.Build.props（模拟 Dynamic 模板的单一来源设计）
            File.WriteAllText(Path.Combine(root, "Complete", "Directory.Build.props"),
                "<Project><!-- 复用 Common 构建属性 --><Import Project=\"..\\Common\\Directory.Build.props\" /></Project>");

            var rootConfig = TemplateCatalog.LoadRoot(root);
            Assert.Null(TemplateCatalog.TryResolve(root, "complete", out var business));
            var options = CreateOptions(rootConfig, root, business!, output);
            TemplateBuilder.Build(options);

            // 扁平化合并后，转发文件不得覆盖 Common 的版本文件（否则 [2] 步骤报“版本文件中没有 Version/…”，且 ..\Common\ 不可达）
            var mergedProps = File.ReadAllText(Path.Combine(output, "Directory.Build.props"));
            Assert.Contains("<Version>1.0.0</Version>", mergedProps);
            Assert.DoesNotContain("<Import", mergedProps);

            // 构建流水线 [2] 步骤：写入版本号必须成功
            VersionManager.WriteVersion(output, "1.2.3", projectCode: "PT01", baseVersion: "1.0.0");
            Assert.Contains("<Version>1.2.3</Version>", File.ReadAllText(Path.Combine(output, "Directory.Build.props")));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Common_project_references_are_rewritten_for_flat_output()
    {
        var root = CreateMinimalTemplate(includeRequiredFiles: true);
        var output = Path.Combine(Path.GetDirectoryName(root)!, $"output-{Guid.NewGuid():N}");
        try
        {
            // 模拟 Dynamic 模板：sln 跨引用 Common 项目、业务 csproj 跨引用 Common 项目（路径含 ..\Common\）
            File.WriteAllText(Path.Combine(root, "Complete", "PCBA.sln"),
                "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"PCBA.Core.Abstractions\", \"..\\Common\\src\\01.Core\\PCBA.Core.Abstractions\\PCBA.Core.Abstractions.csproj\", \"{3158AA90-4379-4721-8287-908425E25A56}\"");
            var stepsDir = Path.Combine(root, "Complete", "src", "04.TestSteps", "PCBA.TestSteps");
            Directory.CreateDirectory(stepsDir);
            File.WriteAllText(Path.Combine(stepsDir, "PCBA.TestSteps.csproj"),
                "<Project><ItemGroup><ProjectReference Include=\"..\\..\\..\\..\\Common\\src\\01.Core\\PCBA.Core.Abstractions\\PCBA.Core.Abstractions.csproj\" /></ItemGroup></Project>");
            // 模拟 App.csproj：引用模板根 README 的 None 项（4 级上跳）
            var appDir = Path.Combine(root, "Complete", "src", "08.App", "PCBA.App");
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "PCBA.App.csproj"),
                "<Project><ItemGroup><None Include=\"..\\..\\..\\..\\README.md\" Link=\"Docs\\README.md\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(root, "Complete", "README.md"), "# Dynamic README");

            var rootConfig = TemplateCatalog.LoadRoot(root);
            Assert.Null(TemplateCatalog.TryResolve(root, "complete", out var business));
            var options = CreateOptions(rootConfig, root, business!, output);
            TemplateBuilder.Build(options);

            // sln 在输出根：..\Common\src\… → src\…（占位符替换同时把 PCBA → PT01）
            var mergedSln = File.ReadAllText(Path.Combine(output, "PT01.sln"));
            Assert.Contains("src\\01.Core\\PT01.Core.Abstractions\\PT01.Core.Abstractions.csproj", mergedSln);
            Assert.DoesNotContain("..\\Common", mergedSln);

            // 业务 csproj 在 src\04.TestSteps\<代号>.TestSteps\：4 级 ..\Common\src\… → 3 级 ..\src\…
            var mergedCsproj = File.ReadAllText(
                Path.Combine(output, "src", "04.TestSteps", "PT01.TestSteps", "PT01.TestSteps.csproj"));
            Assert.Contains("..\\..\\..\\src\\01.Core\\PT01.Core.Abstractions\\PT01.Core.Abstractions.csproj", mergedCsproj);
            Assert.DoesNotContain("Common", mergedCsproj);

            // App.csproj 的模板根 README 引用：4 级 ..\..\..\..\ → 3 级（输出根的 README.md）
            var mergedAppCsproj = File.ReadAllText(
                Path.Combine(output, "src", "08.App", "PT01.App", "PT01.App.csproj"));
            Assert.Contains("<None Include=\"..\\..\\..\\README.md\" Link=\"Docs\\README.md\" />", mergedAppCsproj);
            Assert.DoesNotContain("..\\..\\..\\..\\README.md", mergedAppCsproj);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static BuildOptions CreateOptions(
        TemplateConfig rootConfig,
        string templateRoot,
        BusinessTemplateDescriptor business,
        string output)
        => new()
        {
            ProjectCode = "PT01",
            Template = rootConfig,
            TemplatePath = templateRoot,
            BusinessTemplate = business.Config,
            BusinessTemplatePath = business.DirectoryPath,
            OutputDir = output,
            Version = "1.2.3"
        };

    private static string CreateMinimalTemplate(bool includeRequiredFiles)
    {
        var parent = CreateTempDirectory();
        var root = Path.Combine(parent, "Template");
        var common = Path.Combine(root, "Common");
        var business = Path.Combine(root, "Complete");
        Directory.CreateDirectory(common);
        Directory.CreateDirectory(business);
        File.WriteAllText(Path.Combine(root, "template.config.json"), """
            {
              "schemaVersion": 1,
              "placeholder": "PCBA",
              "description": "test",
              "targetFramework": "net8.0-windows",
              "configuration": "Release",
              "mainProjectName": "PCBA.App",
              "excludeFromCopy": ["bin", "obj"],
              "excludeFromReplacement": [],
              "deleteFromOutput": [],
              "obfuscationTargets": [],
              "reservedNames": []
            }
            """);
        File.WriteAllText(Path.Combine(business, "template.config.json"), """
            { "description": "complete", "businessType": "complete" }
            """);
        File.WriteAllText(Path.Combine(common, "Directory.Build.props"), """
            <Project><PropertyGroup><Version>1.0.0</Version><AssemblyVersion>1.0.0.0</AssemblyVersion><FileVersion>1.0.0.0</FileVersion></PropertyGroup></Project>
            """);

        if (includeRequiredFiles)
        {
            File.WriteAllText(Path.Combine(common, "PCBA.sln"), "Microsoft Visual Studio Solution File");
            var app = Path.Combine(common, "src", "08.App", "PCBA.App");
            Directory.CreateDirectory(app);
            File.WriteAllText(Path.Combine(app, "PCBA.App.csproj"), "<Project />");
        }
        return root;
    }

    private static string LocateWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Template")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("无法定位测试工作区");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testrig-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
