using System.Text.Json;
using WPFCli.Engine;
using WPFCli.Models;
using Xunit;

namespace WPFCli.Tests;

public sealed class TemplateCatalogTests
{
    [Fact]
    public void LoadRoot_rejects_unknown_configuration_fields()
    {
        var root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Common"));
            File.WriteAllText(Path.Combine(root, "template.config.json"), """
                {
                  "schemaVersion": 1,
                  "placeholder": "PCBA",
                  "description": "test",
                  "targetFramework": "net8.0-windows",
                  "configuration": "Release",
                  "mainProjectName": "PCBA.App",
                  "unexpected": true
                }
                """);

            Assert.Throws<InvalidDataException>(() => TemplateCatalog.LoadRoot(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Discover_rejects_business_type_that_does_not_match_directory()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteRootConfig(root);
            var business = Path.Combine(root, "Complete");
            Directory.CreateDirectory(business);
            File.WriteAllText(Path.Combine(business, "template.config.json"), """
                { "description": "test", "businessType": "machine" }
                """);

            Assert.Throws<InvalidDataException>(() => TemplateCatalog.Discover(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("..\\outside")]
    [InlineData("folder/name")]
    [InlineData("C:\\outside")]
    public void TryResolve_rejects_paths_as_business_names(string name)
    {
        var error = TemplateCatalog.TryResolve("C:\\templates", name, out var template);

        Assert.NotNull(error);
        Assert.Null(template);
    }

    [Fact]
    public void Discover_loads_current_enabled_templates()
    {
        var templateRoot = Path.Combine(LocateWorkspace(), "Template");
        var templates = TemplateCatalog.Discover(templateRoot);

        Assert.Equal(3, templates.Count);
        Assert.Equal(2, templates.Count(template => !template.Config.Disabled));
        Assert.Contains(templates, template => template.Config.BusinessType == "dynamic" && !template.Config.Disabled);
        Assert.Contains(templates, template => template.Config.BusinessType == "machine" && !template.Config.Disabled);
        Assert.Contains(templates, template => template.Config.BusinessType == "aging" && template.Config.Disabled);
    }

    private static void WriteRootConfig(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Common"));
        File.WriteAllText(
            Path.Combine(root, "template.config.json"),
            JsonSerializer.Serialize(new TemplateConfig
            {
                Description = "test",
                TargetFramework = "net8.0-windows",
                Configuration = "Release",
                MainProjectName = "PCBA.App"
            }));
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
