using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 强类型 manifest 序列化器：把 <see cref="JigManifest"/> 写回缩进 JSON，与 <see cref="ManifestLoader"/> 成对。
/// 用于维护页保存针床清单。中文不转义（可读），空值省略，枚举按字符串，Order 不落盘（加载时按数组顺序重算）。
/// </summary>
public static class ManifestWriter
{
    /// <summary>
    /// JSON 序列化选项：缩进、空值省略、中文不转义、枚举按字符串名。
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 序列化针床清单为 JSON 文本。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <returns>缩进 JSON 文本。</returns>
    public static string ToJson(JigManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, Options);
    }
}
