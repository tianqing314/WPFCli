using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TESTRIG.Infrastructure.Auth;

/// <summary>
/// 一条历史登录凭据（账号 + 密码）。供登录页默认回填与近 10 人快速选择。
/// </summary>
/// <param name="UserName">账号。</param>
/// <param name="Password">密码（内存明文；落盘时用 DPAPI 按当前 Windows 用户加密）。</param>
public sealed record LoginCredential(string UserName, string Password);

/// <summary>
/// 登录历史存储契约：记录近 10 个登录账号+密码，最近登录者置顶，供登录页默认回填与快速选择。
/// </summary>
public interface ILoginHistoryStore
{
    /// <summary>
    /// 近期登录凭据（最近登录者在前，最多 10 条）。
    /// </summary>
    IReadOnlyList<LoginCredential> Recent { get; }

    /// <summary>
    /// 最近一次登录凭据（无历史返回 null），登录页默认回填用。
    /// </summary>
    LoginCredential? Last { get; }

    /// <summary>
    /// 记录一次成功登录：同账号去重后置顶，超出 10 条丢弃最旧，并持久化。
    /// </summary>
    /// <param name="userName">账号。</param>
    /// <param name="password">密码。</param>
    void Record(string userName, string password);
}

/// <summary>
/// 登录历史读写：<c>Config/login_history.json</c>，近 10 人；密码落盘用 DPAPI（<see cref="DataProtectionScope.CurrentUser"/>）
/// 加密，仅当前 Windows 用户可解密（避免明文密码落盘）。解析/解密失败按空历史处理，不阻断登录。
/// </summary>
public sealed class JsonLoginHistoryStore : ILoginHistoryStore
{
    /// <summary>
    /// 最多保留的历史条数。
    /// </summary>
    private const int MaxItems = 10;

    /// <summary>
    /// DPAPI 附加熵（与其它应用隔离）。
    /// </summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TESTRIG.LoginHistory.v1");

    /// <summary>
    /// JSON 序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<JsonLoginHistoryStore> _logger;

    /// <summary>
    /// login_history.json 路径。
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// 内存中的历史（最近在前）。
    /// </summary>
    private readonly List<LoginCredential> _items;

    /// <summary>
    /// 构造：定位 Config/login_history.json 并加载。
    /// </summary>
    /// <param name="logger">日志。</param>
    public JsonLoginHistoryStore(ILogger<JsonLoginHistoryStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(AppContext.BaseDirectory, "Config");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "login_history.json");
        _items = Load();
    }

    /// <inheritdoc/>
    public IReadOnlyList<LoginCredential> Recent => _items;

    /// <inheritdoc/>
    public LoginCredential? Last => _items.Count > 0 ? _items[0] : null;

    /// <inheritdoc/>
    public void Record(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        var name = userName.Trim();
        _items.RemoveAll(c => string.Equals(c.UserName, name, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, new LoginCredential(name, password ?? string.Empty));
        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        Save();
    }

    /// <summary>
    /// 落盘一条持久化记录（密码密文）。
    /// </summary>
    /// <param name="UserName">账号。</param>
    /// <param name="Protected">DPAPI 加密后的密码 base64。</param>
    private sealed record StoredCredential(string UserName, string Protected);

    /// <summary>
    /// 加载历史：文件不存在/解析失败返回空表；逐条解密密码，解密失败的条目跳过。
    /// </summary>
    /// <returns>历史列表（最近在前）。</returns>
    private List<LoginCredential> Load()
    {
        if (!File.Exists(_path))
        {
            return new List<LoginCredential>();
        }

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredCredential>>(File.ReadAllText(_path), Options) ?? new List<StoredCredential>();
            var result = new List<LoginCredential>();
            foreach (var s in stored)
            {
                if (string.IsNullOrWhiteSpace(s.UserName))
                {
                    continue;
                }
                if (TryUnprotect(s.Protected, out var pwd))
                {
                    result.Add(new LoginCredential(s.UserName, pwd));
                }
            }
            return result.Count > MaxItems ? result.GetRange(0, MaxItems) : result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "登录历史加载失败，按空历史处理");
            return new List<LoginCredential>();
        }
    }

    /// <summary>
    /// 持久化当前历史（密码加密）。写盘失败仅记日志，不抛。
    /// </summary>
    private void Save()
    {
        try
        {
            var stored = _items.Select(c => new StoredCredential(c.UserName, Protect(c.Password))).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(stored, Options));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "登录历史保存失败");
        }
    }

    /// <summary>
    /// DPAPI 加密（当前用户），返回 base64。非 Windows 回落 base64 明文（本平台仅 Windows 运行）。
    /// </summary>
    /// <param name="plain">明文密码。</param>
    /// <returns>密文 base64。</returns>
    private static string Protect(string plain)
    {
        var raw = Encoding.UTF8.GetBytes(plain ?? string.Empty);
        var bytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(raw, Entropy, DataProtectionScope.CurrentUser)
            : raw;
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// DPAPI 解密（当前用户）。失败返回 false。非 Windows 回落 base64 明文。
    /// </summary>
    /// <param name="base64">密文 base64。</param>
    /// <param name="plain">明文密码（成功时有值）。</param>
    /// <returns>是否解密成功。</returns>
    private static bool TryUnprotect(string? base64, out string plain)
    {
        plain = string.Empty;
        if (string.IsNullOrEmpty(base64))
        {
            return true;
        }
        try
        {
            var data = Convert.FromBase64String(base64);
            var bytes = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser)
                : data;
            plain = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
