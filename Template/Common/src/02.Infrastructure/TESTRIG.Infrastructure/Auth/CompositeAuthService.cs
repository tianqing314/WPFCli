using Microsoft.Extensions.Logging;

namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// 组合认证：**测试账号（如 admin）走本地免验证**，其余走 OA 远程验证。
/// 即使配置了 OA 基址，测试账号也无需 OA 即可登录用于测试；且其 <see cref="AuthResult.IsTestAccount"/>=true，
/// 数据只落本地不上传云端。取代"配了 OA 就全部强制走 OA"的行为。
/// </summary>
public sealed class CompositeAuthService : IAuthService
{
    /// <summary>
    /// 本地认证（测试账号用）。
    /// </summary>
    private readonly LocalAuthService _local;

    /// <summary>
    /// OA 远程认证（普通账号用）。
    /// </summary>
    private readonly OaAuthService _oa;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<CompositeAuthService> _logger;

    /// <summary>
    /// 构造组合认证。
    /// </summary>
    /// <param name="local">本地认证。</param>
    /// <param name="oa">OA 远程认证。</param>
    /// <param name="logger">日志。</param>
    public CompositeAuthService(LocalAuthService local, OaAuthService oa, ILogger<CompositeAuthService> logger)
    {
        _local = local;
        _oa = oa;
        _logger = logger;
    }

    /// <summary>
    /// 测试账号走本地免验证，其余走 OA。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>认证结果。</returns>
    public Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken ct = default)
    {
        if (TestAccounts.IsTestAccount(userName))
        {
            _logger.LogInformation("测试账号 {User} 免 OA 验证，走本地登录", userName);
            return _local.AuthenticateAsync(userName, password, ct);
        }

        return _oa.AuthenticateAsync(userName, password, ct);
    }
}
