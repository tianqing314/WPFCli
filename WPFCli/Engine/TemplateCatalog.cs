using System.Text.Json;
using System.Text.Json.Serialization;
using WPFCli.Models;

namespace WPFCli.Engine;

public sealed record BusinessTemplateDescriptor(
    string DirectoryName,
    string DirectoryPath,
    TemplateConfig Config);

/// <summary>集中加载和校验模板配置，供交互模式与参数模式共用。</summary>
public static class TemplateCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static TemplateConfig LoadRoot(string templatePath)
    {
        var root = Path.GetFullPath(templatePath);
        var config = LoadConfig(Path.Combine(root, "template.config.json"));
        var errors = ValidateConfig(config, isRoot: true, directoryName: null);

        if (!Directory.Exists(Path.Combine(root, "Common")))
            errors.Add($"公共模板目录不存在: {Path.Combine(root, "Common")}");

        ThrowIfInvalid(Path.Combine(root, "template.config.json"), errors);
        return config;
    }

    public static IReadOnlyList<BusinessTemplateDescriptor> Discover(string templatePath)
    {
        var root = Path.GetFullPath(templatePath);
        var templates = new List<BusinessTemplateDescriptor>();

        foreach (var directory in Directory.GetDirectories(root))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.Equals("Common", StringComparison.OrdinalIgnoreCase)) continue;

            var configPath = Path.Combine(directory, "template.config.json");
            if (!File.Exists(configPath)) continue;

            var config = LoadConfig(configPath);
            var errors = ValidateConfig(config, isRoot: false, directoryName);
            ThrowIfInvalid(configPath, errors);
            templates.Add(new BusinessTemplateDescriptor(directoryName, directory, config));
        }

        return templates
            .OrderBy(template => template.DirectoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? TryResolve(
        string templatePath,
        string name,
        out BusinessTemplateDescriptor? template)
    {
        template = null;
        if (string.IsNullOrWhiteSpace(name)) return "业务类型不能为空。";
        if (Path.IsPathRooted(name) || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            name is "." or "..")
            return "业务类型必须是模板名称，不能包含路径。";

        IReadOnlyList<BusinessTemplateDescriptor> templates;
        try
        {
            templates = Discover(templatePath);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        template = templates.FirstOrDefault(candidate =>
            candidate.DirectoryName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            candidate.Config.BusinessType.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (template == null) return $"未找到业务模板: {name}";
        if (template.Config.Disabled)
        {
            var disabledName = template.DirectoryName;
            template = null;
            return $"\"{disabledName}\" 为预留模板（disabled），暂不可用。";
        }

        return null;
    }

    private static TemplateConfig LoadConfig(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("模板配置文件不存在", path);
        try
        {
            return JsonSerializer.Deserialize<TemplateConfig>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException($"模板配置解析为 null: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"模板配置解析失败: {path}: {ex.Message}", ex);
        }
    }

    private static List<string> ValidateConfig(TemplateConfig config, bool isRoot, string? directoryName)
    {
        var errors = new List<string>();
        if (config.SchemaVersion != 1) errors.Add($"不支持 schemaVersion={config.SchemaVersion}，当前仅支持 1");

        ValidateList(config.ExcludeFromCopy, "excludeFromCopy", errors);
        ValidateList(config.ExcludeFromReplacement, "excludeFromReplacement", errors);
        ValidateList(config.DeleteFromOutput, "deleteFromOutput", errors);
        ValidateList(config.ObfuscationTargets, "obfuscationTargets", errors, paths: false);
        ValidateList(config.ReservedNames, "reservedNames", errors, paths: false);

        if (isRoot)
        {
            if (string.IsNullOrWhiteSpace(config.Placeholder)) errors.Add("placeholder 不能为空");
            if (string.IsNullOrWhiteSpace(config.Description)) errors.Add("description 不能为空");
            if (string.IsNullOrWhiteSpace(config.TargetFramework)) errors.Add("targetFramework 不能为空");
            if (string.IsNullOrWhiteSpace(config.Configuration)) errors.Add("configuration 不能为空");
            if (string.IsNullOrWhiteSpace(config.MainProjectName)) errors.Add("mainProjectName 不能为空");
            if (!IsSafeIdentifier(config.TargetFramework)) errors.Add("targetFramework 包含不安全字符");
            if (!IsSafeIdentifier(config.Configuration)) errors.Add("configuration 包含不安全字符");
            if (config.MainProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                errors.Add("mainProjectName 包含非法文件名字符");
            if (!string.IsNullOrWhiteSpace(config.Placeholder) &&
                !config.MainProjectName.Contains(config.Placeholder, StringComparison.Ordinal))
                errors.Add("mainProjectName 必须包含 placeholder");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.BusinessType)) errors.Add("businessType 不能为空");
            if (string.IsNullOrWhiteSpace(config.Description)) errors.Add("description 不能为空");
            if (!string.IsNullOrWhiteSpace(directoryName) &&
                !directoryName.Equals(config.BusinessType, StringComparison.OrdinalIgnoreCase))
                errors.Add($"businessType '{config.BusinessType}' 必须与目录名 '{directoryName}' 一致");
        }

        return errors;
    }

    private static void ValidateList(List<string>? values, string name, List<string> errors, bool paths = true)
    {
        if (values == null)
        {
            errors.Add($"{name} 不能为 null");
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{name}[{i}] 不能为空");
                continue;
            }

            if (!paths) continue;
            var normalized = value.Replace('\\', '/');
            if (Path.IsPathRooted(value) || normalized.Split('/').Any(segment => segment == ".."))
                errors.Add($"{name}[{i}] 必须是模板内的安全相对路径: {value}");
        }
    }

    private static void ThrowIfInvalid(string path, List<string> errors)
    {
        if (errors.Count == 0) return;
        throw new InvalidDataException($"模板配置无效: {path}{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", errors)}");
    }

    private static bool IsSafeIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value) && value.All(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_');
}
