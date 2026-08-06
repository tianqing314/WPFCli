namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// 认证结果。
/// </summary>
/// <param name="Success">是否通过。</param>
/// <param name="DisplayName">显示名（真实姓名或登录名）。</param>
/// <param name="Error">失败原因（成功为 null）。</param>
/// <param name="IsTestAccount">是否测试账号（如 admin）。测试账号免 OA 验证，且数据只落本地不上传云端。</param>
public sealed record AuthResult(bool Success, string? DisplayName = null, string? Error = null, bool IsTestAccount = false);

/// <summary>
/// 测试账号约定：测试账号（当前为 <c>admin</c>）免 OA 验证即可登录，且其测试数据只落盘本地、不上传云端。
/// </summary>
public static class TestAccounts
{
    /// <summary>
    /// 测试账号名（大小写不敏感）。
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// 测试账号（admin）固定密码。输错即报"密码错误"。
    /// </summary>
    public const string AdminPassword = "123456";

    /// <summary>
    /// 判断给定用户名是否为测试账号。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <returns>是否测试账号。</returns>
    public static bool IsTestAccount(string? userName)
    {
        return string.Equals(userName?.Trim(), Admin, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 认证抽象。本轮提供本地离线实现；远程 SQL Server / MES 认证以后实现同一接口即可替换，
/// 不影响上层——取代旧直连 mssql.const.cc 的硬耦合。
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 校验用户名/密码。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>认证结果。</returns>
    Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken ct = default);
}
