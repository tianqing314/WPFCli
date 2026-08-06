using System.Xml.Linq;

namespace WPFCli.Engine;

/// <summary>
/// 版本号管理：从 MSBuild XML 读取和写入版本，并维护生成审计元数据。
/// </summary>
public static class VersionManager
{
    private static readonly string[] VersionTags = ["Version", "AssemblyVersion", "FileVersion"];

    public static string? ReadVersionFromCsproj(string csprojPath)
        => ReadVersionFromXml(csprojPath);

    private static string? ReadVersionFromProps(string propsPath)
        => ReadVersionFromXml(propsPath);

    private static string? ReadVersionFromXml(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            foreach (var tag in VersionTags)
            {
                var value = document.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    ?.Value.Trim();
                if (!string.IsNullOrWhiteSpace(value) && IsValidVersion(value)) return value;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从 Common 的 Directory.Build.props 读取版本，兼容旧模板的主项目 csproj。</summary>
    public static string? DetectVersion(string templatePath, string mainProjectName)
    {
        var propsFile = Path.Combine(templatePath, "Directory.Build.props");
        var version = ReadVersionFromProps(propsFile);
        if (!string.IsNullOrWhiteSpace(version)) return version;

        var csproj = Path.Combine(templatePath, "src", "08.App", mainProjectName, $"{mainProjectName}.csproj");
        return ReadVersionFromCsproj(csproj);
    }

    /// <summary>递增版本号的 patch 段。</summary>
    public static string IncrementPatch(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "1.0.1";

        var parts = version.Trim().Split('.');
        var numbers = parts.Select(part => int.TryParse(part, out var number) ? number : 0).ToList();
        while (numbers.Count < 3) numbers.Add(0);
        if (numbers.Count > 4) numbers = numbers.Take(4).ToList();
        numbers[2]++;
        if (numbers.Count == 4) numbers[3] = 0;
        return string.Join('.', numbers);
    }

    public static bool IsValidVersion(string version)
        => !string.IsNullOrWhiteSpace(version) &&
           System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+(\.\d+)?$");

    /// <summary>写入生成项目版本和构建审计元数据。</summary>
    public static void WriteVersion(string outputDir, string newVersion, string? projectCode = null, string? baseVersion = null)
    {
        var propsFile = Path.Combine(outputDir, "Directory.Build.props");
        if (!File.Exists(propsFile))
            throw new FileNotFoundException("生成项目缺少 Directory.Build.props", propsFile);
        UpdateVersionDocument(propsFile, newVersion, includeAudit: true, projectCode, baseVersion);
    }

    private static void UpdateVersionDocument(
        string path,
        string newVersion,
        bool includeAudit,
        string? projectCode = null,
        string? baseVersion = null)
    {
        if (!IsValidVersion(newVersion))
            throw new ArgumentException($"无效版本号: {newVersion}", nameof(newVersion));

        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var updated = false;
        foreach (var tag in VersionTags)
        {
            var elements = document.Descendants()
                .Where(element => element.Name.LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var element in elements)
            {
                element.Value = tag.Equals("Version", StringComparison.OrdinalIgnoreCase)
                    ? newVersion
                    : $"{newVersion}.0";
                updated = true;
            }
        }

        if (!updated)
            throw new InvalidDataException($"版本文件中没有 Version/AssemblyVersion/FileVersion: {path}");

        if (includeAudit)
        {
            SetOrCreateProperty(document, "BuildProjectCode", projectCode);
            SetOrCreateProperty(document, "BuildBaseVersion", baseVersion);
            SetOrCreateProperty(document, "BuildGeneratedAt", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }

        WriteDocumentAtomically(path, document);
    }

    private static void SetOrCreateProperty(XDocument document, string tag, string? value)
    {
        if (value == null && tag != "BuildGeneratedAt") return;
        var element = document.Descendants()
            .FirstOrDefault(item => item.Name.LocalName.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (element != null)
        {
            element.Value = value ?? string.Empty;
            return;
        }

        var propertyGroup = document.Descendants()
            .FirstOrDefault(item => item.Name.LocalName.Equals("PropertyGroup", StringComparison.OrdinalIgnoreCase));
        if (propertyGroup == null)
            throw new InvalidDataException("版本文件中没有 PropertyGroup");
        propertyGroup.Add(new XElement(propertyGroup.Name.Namespace + tag, value ?? string.Empty));
    }

    private static void WriteDocumentAtomically(string path, XDocument document)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            document.Save(tempPath, SaveOptions.DisableFormatting);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
