using Microsoft.Extensions.Logging;

namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// 本地离线认证：测试账号（admin）用固定密码 <see cref="TestAccounts.AdminPassword"/> 校验，密码不符报"密码错误"。
/// </summary>
public sealed class LocalAuthService : IAuthService
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<LocalAuthService> _logger;

    /// <summary>
    /// 构造本地认证服务。
    /// </summary>
    /// <param name="logger">日志。</param>
    public LocalAuthService(ILogger<LocalAuthService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 本地校验：用户名与密码均非空即通过，显示名取用户名。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>认证结果。</returns>
    public Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(new AuthResult(false, Error: "用户名和密码不能为空"));
        }

        // 测试账号（admin）须用固定密码，输错报"密码错误"
        if (TestAccounts.IsTestAccount(userName) && !string.Equals(password, TestAccounts.AdminPassword, StringComparison.Ordinal))
        {
            _logger.LogWarning("测试账号 {User} 密码错误", userName);
            return Task.FromResult(new AuthResult(false, Error: "密码错误"));
        }

        _logger.LogInformation("本地登录：{User}", userName);
        return Task.FromResult(new AuthResult(true, DisplayName: userName, IsTestAccount: TestAccounts.IsTestAccount(userName)));
    }
}
