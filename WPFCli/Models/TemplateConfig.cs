using System.Text.Json.Serialization;

namespace WPFCli.Models;

/// <summary>
/// 模板元数据 —— 对应 Template/template.config.json。
/// 声明占位符、排除规则等模板级配置。
/// </summary>
public class TemplateConfig
{
    /// <summary>配置格式版本，用于未来兼容迁移。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>模板占位符（产品代号），如 "TESTRIG"。生成时全词替换为用户输入。</summary>
    [JsonPropertyName("placeholder")]
    public string Placeholder { get; set; } = "TESTRIG";

    /// <summary>模板描述。</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>目标框架，如 "net8.0-windows10.0.19041.0"。</summary>
    [JsonPropertyName("targetFramework")]
    public string TargetFramework { get; set; } = "net8.0-windows10.0.19041.0";

    /// <summary>编译配置，如 "Release"。</summary>
    [JsonPropertyName("configuration")]
    public string Configuration { get; set; } = "Release";

    /// <summary>主项目名（替换占位符后即为 {代号}.App）。</summary>
    [JsonPropertyName("mainProjectName")]
    public string MainProjectName { get; set; } = "TESTRIG.App";

    /// <summary>拷贝时排除的目录名（编译产物等）。</summary>
    [JsonPropertyName("excludeFromCopy")]
    public List<string> ExcludeFromCopy { get; set; } = new();

    /// <summary>替换内容时排除的相对路径（不替换内容，但仍拷贝）。</summary>
    [JsonPropertyName("excludeFromReplacement")]
    public List<string> ExcludeFromReplacement { get; set; } = new();

    /// <summary>业务模板合并后需要从公共骨架删除的相对路径。</summary>
    [JsonPropertyName("deleteFromOutput")]
    public List<string> DeleteFromOutput { get; set; } = new();

    /// <summary>保留名列表 —— 禁止用户作为产品代号（避免与模板中已有标识符冲突，如设备型号 PS02）。</summary>
    [JsonPropertyName("reservedNames")]
    public List<string> ReservedNames { get; set; } = new();

    /// <summary>业务类型标识（common/complete/machine/inspect/aging/dynamic）。</summary>
    [JsonPropertyName("businessType")]
    public string BusinessType { get; set; } = "";

    /// <summary>业务分类（aging/dynamic/machine），用于向导中分组展示。</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    /// <summary>模板大类（dedicated=专线模板 / general=通用模板），用于向导一级分类。</summary>
    [JsonPropertyName("group")]
    public string Group { get; set; } = "";

    /// <summary>预留模板标记 —— 为 true 时向导中显示但不可选择（如动态工装预留）。</summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>被检类型占位符（如 "TemplateUUT"），生成时替换为用户指定的被检类型。仅动态工装模板使用。</summary>
    [JsonPropertyName("dutPlaceholder")]
    public string DutPlaceholder { get; set; } = "";
}
