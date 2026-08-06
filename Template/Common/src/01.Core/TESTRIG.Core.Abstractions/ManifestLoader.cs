using System.Text.Json;

namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 强类型 manifest 加载器：System.Text.Json 绑定到 <see cref="JigManifest"/>，
/// 禁用多态 <c>$type</c>，启动期即校验（required 字段缺失会抛）。
/// </summary>
public sealed class ManifestLoader
{
    /// <summary>
    /// JSON 反序列化选项：大小写不敏感、跳过注释、允许尾逗号、枚举按字符串。
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// 从文件加载并校验针床清单。
    /// </summary>
    /// <param name="path">清单 JSON 路径。</param>
    /// <returns>强类型清单。</returns>
    /// <exception cref="FileNotFoundException">文件不存在。</exception>
    /// <exception cref="InvalidDataException">格式错误或校验失败。</exception>
    public JigManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"针床清单不存在：{path}");
        }

        var json = File.ReadAllText(path);
        JigManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<JigManifest>(json, Options)
                       ?? throw new InvalidDataException($"清单解析为空：{path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"清单 {Path.GetFileName(path)} 格式错误：{ex.Message}", ex);
        }

        Validate(manifest, path);

        // 序号按 Steps 数组的加载顺序自动赋值（1 起），忽略 JSON 里可能写的 Order——调整顺序只需改数组位置
        manifest = manifest with
        {
            Steps = manifest.Steps.Select((s, i) => s with { Order = i + 1 }).ToList(),
        };
        return manifest;
    }

    /// <summary>
    /// 校验清单必填项（Key/Positions/Steps 非空）。序号由加载顺序赋值，无需校验重复。
    /// </summary>
    /// <param name="m">待校验清单。</param>
    /// <param name="path">清单路径（用于错误信息）。</param>
    /// <exception cref="InvalidDataException">校验失败。</exception>
    private static void Validate(JigManifest m, string path)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(m.Key))
        {
            errors.Add("Key 为空");
        }

        if (m.Positions.Count == 0)
        {
            errors.Add("Positions 为空");
        }

        if (m.Steps.Count == 0)
        {
            errors.Add("Steps 为空");
        }

        // Dut.Model 是驱动派发键 + 结果落库的 DeviceModel，缺了会静默回落通用仿真、且数据无法按型号归属
        if (string.IsNullOrWhiteSpace(m.Dut.Model))
        {
            errors.Add("Dut.Model 为空（必须一板一型号）");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException($"清单 {Path.GetFileName(path)} 校验失败：{string.Join("；", errors)}");
        }
    }
}
