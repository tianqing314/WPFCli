namespace TESTRIG.Devices.Abstractions.Version;

/// <summary>
/// 版本校验返回（服务器 <c>Data</c> 字段反序列化结果）。迁移自旧平台
/// <c>Bots.TestBench.Model.Upgrade.Model.VersionValidResponse</c>，字段名与服务器 JSON 保持一致。
/// </summary>
public sealed class VersionValidResponse
{
    /// <summary>校验结果（默认按不规范处理）。</summary>
    public VersionValidResult Result { get; set; } = VersionValidResult.NonStandard;

    /// <summary>被检当前版本。</summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>服务器最新版本。</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>设备类型。</summary>
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>校验是否通过：被检版本 ≥ 服务器最新（等于或更高）视为合格。</summary>
    public bool IsPass => Result is VersionValidResult.Equal or VersionValidResult.Greater;

    /// <summary>可读描述。</summary>
    /// <returns>「当前版本 / 最新版本 / 结论」描述。</returns>
    public override string ToString()
    {
        return $"当前版本：{CurrentVersion}，最新版本：{LatestVersion}，校验结果：{Result}";
    }
}
