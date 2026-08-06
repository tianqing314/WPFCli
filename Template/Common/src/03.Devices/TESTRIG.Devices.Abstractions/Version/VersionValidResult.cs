using System.ComponentModel;

namespace TESTRIG.Devices.Abstractions.Version;

/// <summary>
/// 版本校验结果（被检读回版本 与 服务器最新版本 的比对结论）。
/// 迁移自旧平台 <c>Bots.TestBench.Model.Upgrade.Enum.VersionValidResult</c>，枚举值顺序保持一致（服务器按序号返回）。
/// </summary>
public enum VersionValidResult
{
    /// <summary>当前版本不规范（无法解析）。</summary>
    [Description("当前版本不规范")]
    NonStandard = 0,

    /// <summary>未匹配到服务器版本。</summary>
    [Description("未匹配到服务器版本")]
    UnKnown = 1,

    /// <summary>被检设备版本小于服务器版本。</summary>
    [Description("被检设备版本小于服务器版本")]
    Less = 2,

    /// <summary>被检设备版本等于服务器版本。</summary>
    [Description("被检设备版本等于服务器版本")]
    Equal = 3,

    /// <summary>被检设备版本大于服务器版本。</summary>
    [Description("被检设备版本大于服务器版本")]
    Greater = 4,
}
