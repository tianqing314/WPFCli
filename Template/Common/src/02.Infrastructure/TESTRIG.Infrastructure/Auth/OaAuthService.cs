using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TESTRIG.Infrastructure.Configuration;

namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// OA（致远 Seeyon）远程认证：对接 <c>/seeyon/rest/authentication</c> 接口，
/// 取代旧 <c>OAUserService.UserVerify</c> 的 WebClient 直连。用户名/密码交 OA 校验，
/// 返回 <c>{"oK":"true"}</c> 视为通过。BaseUrl 由 <see cref="PcbaOptions.Oa"/> 配置注入。
/// </summary>
public sealed class OaAuthService : IAuthService
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<OaAuthService> _logger;

    /// <summary>
    /// HTTP 客户端（按配置超时，复用单例）。
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// OA 配置（基址/超时/令牌/公司列表）。
    /// </summary>
    private readonly OaOptions _oa;

    /// <summary>
    /// OA 基址（已去尾斜杠）。
    /// </summary>
    private readonly string _baseUrl;

    /// <summary>
    /// 公司 members 解析结果缓存（登录名→真实姓名），进程内复用，避免每次登录重复下载。
    /// </summary>
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _membersCache = new();

    /// <summary>
    /// 用配置的 OA 基址与超时构造，内部复用单个 <see cref="HttpClient"/>。
    /// </summary>
    /// <param name="options">全局配置（取 OA 段）。</param>
    /// <param name="logger">日志。</param>
    public OaAuthService(IOptions<PcbaOptions> options, ILogger<OaAuthService> logger)
    {
        _logger = logger;
        _oa = options.Value.Oa;
        _baseUrl = _oa.BaseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, _oa.TimeoutSeconds)) };
    }

    /// <summary>
    /// 向 OA 校验用户名/密码。网络异常一律判失败并返回可读错误，不抛给上层。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>认证结果（成功时 DisplayName 为真实姓名或登录名）。</returns>
    public async Task<AuthResult> AuthenticateAsync(string userName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, Error: "用户名和密码不能为空");
        }

        try
        {
            // Seeyon 认证：账号密码走查询串，POST 空表单体（沿用旧 UserVerify 的调用形态）
            var url = $"{_baseUrl}/seeyon/rest/authentication" +
                      $"?login_username={Uri.EscapeDataString(userName)}" +
                      $"&login_password={Uri.EscapeDataString(password)}";
            using var content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");
            using var resp = await _http.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("OA 认证 HTTP {Status}：{User}", (int)resp.StatusCode, userName);
                return new AuthResult(false, Error: $"OA 服务返回 {(int)resp.StatusCode}");
            }

            if (IsOk(body))
            {
                _logger.LogInformation("OA 登录成功：{User}", userName);
                var trueName = await TryResolveTrueNameAsync(userName, ct);
                return new AuthResult(true, DisplayName: string.IsNullOrWhiteSpace(trueName) ? userName : trueName);
            }

            _logger.LogInformation("OA 登录被拒：{User}", userName);
            return new AuthResult(false, Error: "用户名或密码错误");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OA 认证异常：{User}", userName);
            return new AuthResult(false, Error: "无法连接 OA 服务：" + ex.Message);
        }
    }

    /// <summary>
    /// 解析 Seeyon 返回体，判定 <c>oK</c> 字段（大小写宽松）是否为 true。
    /// </summary>
    /// <param name="body">响应体。</param>
    /// <returns>是否通过。</returns>
    private static bool IsOk(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(p.Name, "oK", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Equals(p.Value.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 返回：退化为文本包含判断
            return body.Replace(" ", "").Contains("\"oK\":\"true\"", StringComparison.OrdinalIgnoreCase)
                || body.Replace(" ", "").Contains("\"oK\":true", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// 登录名反查 OA 真实姓名：遍历各公司 members，命中即返回。任何失败都吞掉返回 null（不影响已成功的登录）。
    /// </summary>
    /// <param name="userName">登录名。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>真实姓名，未命中/失败返回 null。</returns>
    private async Task<string?> TryResolveTrueNameAsync(string userName, CancellationToken ct)
    {
        try
        {
            var token = await GetTokenAsync(ct);
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            foreach (var company in _oa.Companies)
            {
                var map = await GetMembersAsync(company, token, ct);
                if (map.TryGetValue(userName, out var trueName) && !string.IsNullOrWhiteSpace(trueName))
                {
                    return trueName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "反查真实姓名失败，回退登录名：{User}", userName);
        }
        return null;
    }

    /// <summary>
    /// 取 members 接口令牌：GET /seeyon/rest/token/{account}/{password}，返回体可能是 {"id":...} 或纯文本。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>令牌，失败返回 null/空。</returns>
    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        var url = $"{_baseUrl}/seeyon/rest/token/{_oa.TokenAccount}/{_oa.TokenPassword}";
        var body = (await _http.GetStringAsync(url, ct)).Trim();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("id", out var id))
            {
                return id.ToString();
            }
        }
        catch (JsonException) { /* 纯文本令牌，直接用 */ }
        return body;
    }

    /// <summary>
    /// 取某公司全体成员（登录名→真实姓名），带进程内缓存。
    /// </summary>
    /// <param name="company">公司名。</param>
    /// <param name="token">members 令牌。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>登录名→真实姓名字典（失败为空表并缓存）。</returns>
    private async Task<IReadOnlyDictionary<string, string>> GetMembersAsync(string company, string token, CancellationToken ct)
    {
        if (_membersCache.TryGetValue(company, out var cached))
        {
            return cached;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var url = $"{_baseUrl}/seeyon/rest/data/members/{Uri.EscapeDataString(company)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("token", token);
            using var resp = await _http.SendAsync(req, ct);
            var xml = await resp.Content.ReadAsStringAsync(ct);

            var xd = new XmlDocument();
            xd.LoadXml(xml);
            var nodes = xd.SelectNodes("/DataPojo/DataProperty/DataPojo");
            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    string? login = null, trueName = null;
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child is not XmlElement xe)
                        {
                            continue;
                        }

                        var col = xe.GetAttribute("propertyname");
                        var val = xe.HasAttribute("value") ? xe.GetAttribute("value") : xe.InnerText;
                        if (col == "loginName")
                        {
                            login = val;
                        }
                        else if (col == "trueName")
                        {
                            trueName = val;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(login) && trueName != null)
                    {
                        map[login!] = trueName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "拉取 OA 成员失败：{Company}", company);
        }

        _membersCache[company] = map;   // 失败也缓存空表，避免每次登录都重试慢接口
        return map;
    }
}
