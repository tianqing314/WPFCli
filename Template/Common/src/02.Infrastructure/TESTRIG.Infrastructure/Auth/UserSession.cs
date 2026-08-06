namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// 当前登录用户会话（全局共享单例）。登录成功后写入，落库时作为 operator。
/// </summary>
public interface IUserSession
{
    /// <summary>
    /// 当前操作员（登录用户显示名）。
    /// </summary>
    string? Operator { get; set; }

    /// <summary>
    /// 当前是否为测试账号（如 admin）登录。为 true 时测试数据只落盘本地、不上传云端。
    /// </summary>
    bool IsTestAccount { get; set; }
}

/// <summary>
/// 用户会话默认实现（进程内单例）。
/// </summary>
public sealed class UserSession : IUserSession
{
    /// <summary>
    /// 当前操作员（登录用户显示名）。
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    /// 当前是否为测试账号（如 admin）登录。为 true 时测试数据只落盘本地、不上传云端。
    /// </summary>
    public bool IsTestAccount { get; set; }
}
