namespace TESTRIG.Infrastructure.Configuration;

/// <summary>
/// 全局强类型配置——取代散落的 App.config + GlobalConfigSetting + homeConfig。
/// 绑定 appsettings.json 的 "Pcba" 段。
/// </summary>
public sealed class PcbaOptions
{
    /// <summary>
    /// 配置段名。
    /// </summary>
    public const string Section = "Pcba";

    /// <summary>
    /// 界面语言（zh/en）。
    /// </summary>
    public string Culture { get; set; } = "zh";

    /// <summary>
    /// MaterialDesign 主题名。
    /// </summary>
    public string Theme { get; set; } = "Light.Blue";

    /// <summary>
    /// 本地 SQLite 结果库相对路径（相对程序目录）。
    /// </summary>
    public string DatabasePath { get; set; } = "Data/pcba_results.db";

    /// <summary>
    /// 测试项失败即停整机测试。
    /// </summary>
    public bool StopOnFail { get; set; } = false;

    /// <summary>
    /// OA（致远 Seeyon）远程认证配置。BaseUrl 为空时回退本地离线认证。
    /// </summary>
    public OaOptions Oa { get; set; } = new();

    /// <summary>
    /// 远程 MySQL 结果库上报配置。ConnectionString 为空时不启用远程上报。
    /// </summary>
    public RemoteSyncOptions RemoteSync { get; set; } = new();
}

/// <summary>
/// 远程 MySQL 结果库上报配置。
/// </summary>
public sealed class RemoteSyncOptions
{
    /// <summary>
    /// MySQL 连接串，如 Server=192.168.4.103;Port=3306;Database=cst_auto_test_data;User=yanfa;Password=yanfa。
    /// 为空则不启用远程上报（仅本地 SQLite）。
    /// </summary>
    public string ConnectionString { get; set; } = "";
}

/// <summary>
/// OA（致远 Seeyon）认证配置——取代旧硬编码的 oa.const.cc 直连。
/// </summary>
public sealed class OaOptions
{
    /// <summary>
    /// OA 服务基址，如 http://oa.const.cc:8080。为空则不启用远程认证。
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// 认证请求超时（秒）。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 取 members 令牌的账号（Seeyon token 接口路径段）。
    /// </summary>
    public string TokenAccount { get; set; } = "const";

    /// <summary>
    /// 取 members 令牌的口令（Seeyon token 接口路径段）。
    /// </summary>
    public string TokenPassword { get; set; } = "const123456";

    /// <summary>
    /// 登录成功后按登录名反查真实姓名时要遍历的公司名列表（OA members 按公司分组）。
    /// </summary>
    public string[] Companies { get; set; } =
    {
        "北京康斯特仪表科技股份有限公司",
        "北京桑普新源技术有限公司",
        "北京恒矩检测技术有限公司",
    };
}
