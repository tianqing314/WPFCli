namespace WPFCli.Models;

/// <summary>
/// 一次构建的完整选项 —— 由向导收集，流水线全程使用。
/// </summary>
public class BuildOptions
{
    /// <summary>用户输入的项目代号（如 PT01）。</summary>
    public string ProjectCode { get; set; } = "";

    /// <summary>输出目录绝对路径（固定为 &lt;工作区&gt;\Output\&lt;代号&gt;）。</summary>
    public string OutputDir { get; set; } = "";

    /// <summary>模板根目录绝对路径（Template\）。</summary>
    public string TemplatePath { get; set; } = "";

    /// <summary>模板元数据。</summary>
    public TemplateConfig Template { get; set; } = new();

    /// <summary>业务模板目录绝对路径（Template\&lt;业务类型&gt;）。</summary>
    public string BusinessTemplatePath { get; set; } = "";

    /// <summary>业务模板元数据（可覆盖全局配置的 description/exclude 等）。</summary>
    public TemplateConfig BusinessTemplate { get; set; } = new();

    /// <summary>业务类型标识（complete/machine/inspect/aging）。</summary>
    public string BusinessType => Path.GetFileName(BusinessTemplatePath);

    /// <summary>是否生成安装包脚本。</summary>
    public bool EnablePackaging { get; set; }

    /// <summary>输出已存在时是否允许在完整构建成功后替换。</summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>仅验证并预演生成，不发布正式输出。</summary>
    public bool DryRun { get; set; }

    /// <summary>只生成项目，不执行 dotnet build。</summary>
    public bool SkipBuild { get; set; }

    /// <summary>被检类型（如 PS02），仅动态工装模板需要。空值表示不替换被检占位符。</summary>
    public string DutType { get; set; } = "";

    /// <summary>References 适配根目录（含 References\Dynamic\{被检类型}\ 子目录）。空值默认取模板根目录同级的 References。</summary>
    public string ReferencesRoot { get; set; } = "";

    /// <summary>被检导入方式（原测试平台导入 / Excel 导入）。</summary>
    public DutImportMethod ImportMethod { get; set; } = DutImportMethod.OriginalPlatform;

    /// <summary>主项目名（替换占位符后，如 PT01.App）。</summary>
    public string MainProjectName => Template.MainProjectName.Replace(Template.Placeholder, ProjectCode);

    /// <summary>解决方案文件名（替换占位符后，如 PT01.sln）。</summary>
    public string SolutionFileName => $"{ProjectCode}.sln";

    /// <summary>解决方案文件绝对路径。</summary>
    public string SolutionPath => Path.Combine(OutputDir, SolutionFileName);

    /// <summary>编译产物目录（&lt;代号&gt;.App\bin\Release\&lt;tfm&gt;）。</summary>
    public string PublishDir => Path.Combine(OutputDir,
        "src", "08.App", MainProjectName, "bin",
        Template.Configuration, Template.TargetFramework);

    /// <summary>主 EXE 文件名（如 PT01.App.exe）。</summary>
    public string MainExeFileName => $"{MainProjectName}.exe";
}

/// <summary>
/// 被检导入方式：原测试平台导入（References 适配旧 Bots.TestBench 资源）或 Excel 导入（预留）。
/// </summary>
public enum DutImportMethod
{
    /// <summary>原测试平台导入（默认）。</summary>
    OriginalPlatform,

    /// <summary>新方式 Excel 导入（预留，暂不实现解析）。</summary>
    Excel
}
