using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TESTRIG.Devices.Abstractions.Version;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// HTTP 版本校验：GET <c>{BaseUrl}/api/db/vm/version/verify/{endpoint}?...</c>，
/// 解析响应体 <c>Data</c> 为 <see cref="VersionValidResponse"/>。各被检设备测试项共用。
/// 迁移自旧 <c>DBService</c> 版本验证区（同一批服务器接口）：失败重试 3 次、每次间隔 2s，
/// 全失败降级为 <see cref="VersionValidResult.UnKnown"/>，不向调用方抛出。
/// </summary>
public sealed class HttpVersionValidator : IVersionValidator
{
    /// <summary>服务器版本校验接口的相对路径前缀。</summary>
    private const string ApiPrefix = "api/db/vm/version/verify";

    /// <summary>重试次数。</summary>
    private const int MaxRetry = 3;

    /// <summary>重试间隔。</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>反序列化选项：忽略大小写 + 枚举可接受名称或数字。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>HTTP 客户端（10s 超时）。</summary>
    private readonly HttpClient _http;

    /// <summary>版本服务器基址（已去尾斜杠）。</summary>
    private readonly string _baseUrl;

    /// <summary>日志。</summary>
    private readonly ILogger _logger;

    /// <summary>用版本服务器基址构造。</summary>
    /// <param name="baseUrl">版本服务器基址（如 http://192.168.0.134:10001）。</param>
    /// <param name="logger">日志。</param>
    public HttpVersionValidator(string baseUrl, ILogger<HttpVersionValidator> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateAsync(string softVersion, CancellationToken ct = default)
    {
        return RequestAsync("VaildSoftVersion", ct, ("softVersion", softVersion));
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateByDeviceTypeAsync(string softVersion, string deviceType, CancellationToken ct = default)
    {
        return RequestAsync("VaildSoftVersion-byDeviceType", ct, ("softVersion", softVersion), ("deviceType", deviceType));
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateByHardVersionAsync(string softVersion, string hardVersion, CancellationToken ct = default)
    {
        return RequestAsync("VaildSoftVersion-byHardVersion", ct, ("softVersion", softVersion), ("hardVersion", hardVersion));
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateByHardVersionAndDeviceTypeAsync(string softVersion, string hardVersion, string deviceType, CancellationToken ct = default)
    {
        // 注意：此接口服务器约定 deviceType 参数名为全小写 devicetype（与 byDeviceType 接口不同）
        return RequestAsync("VaildSoftVersion-byHardVersionAndDeviceType", ct, ("softVersion", softVersion), ("hardVersion", hardVersion), ("devicetype", deviceType));
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateByHostVersionAsync(string softVersion, string hostVersion, CancellationToken ct = default)
    {
        return RequestAsync("VaildSoftVersion-byHostVersion", ct, ("softVersion", softVersion), ("hostVersion", hostVersion));
    }

    /// <inheritdoc/>
    public Task<VersionValidResponse> ValidateBySuffixAsync(string softVersion, string suffix, CancellationToken ct = default)
    {
        return RequestAsync("VaildSoftVersion-bySuffix", ct, ("softVersion", softVersion), ("suffix", suffix));
    }

    /// <summary>
    /// 请求版本服务器并解析 <c>Data</c>：重试 <see cref="MaxRetry"/> 次，全失败降级为 UnKnown。
    /// </summary>
    /// <param name="endpoint">接口名（ApiPrefix 之后的段）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="query">查询参数（键值对，内部做 URL 编码）。</param>
    /// <returns>校验结果，全失败降级为 UnKnown。</returns>
    private async Task<VersionValidResponse> RequestAsync(string endpoint, CancellationToken ct, params (string Key, string Value)[] query)
    {
        var qs = string.Join("&", query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value ?? string.Empty)}"));
        var url = $"{_baseUrl}/{ApiPrefix}/{endpoint}?{qs}";
        var lastError = string.Empty;

        for (var attempt = 1; attempt <= MaxRetry; attempt++)
        {
            try
            {
                var json = await _http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Data", out var data) || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return Unknown("没从服务器找到最新版本");
                }

                // Data 可能是对象，也可能是被转义的 JSON 字符串（旧接口两种都出现过）
                var result = data.ValueKind == JsonValueKind.String
                    ? JsonSerializer.Deserialize<VersionValidResponse>(data.GetString() ?? "", JsonOptions)
                    : data.Deserialize<VersionValidResponse>(JsonOptions);
                return result ?? Unknown("没从服务器找到最新版本");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "版本校验请求失败（{Attempt}/{Max}）：{Url}", attempt, MaxRetry, url);
                if (attempt < MaxRetry)
                {
                    await Task.Delay(RetryDelay, ct);
                }
            }
        }

        return Unknown($"{lastError}，没从服务器找到最新版本");
    }

    /// <summary>构造 UnKnown 降级结果。</summary>
    /// <param name="latestVersion">写入 LatestVersion 字段的说明文本。</param>
    /// <returns>UnKnown 结果。</returns>
    private static VersionValidResponse Unknown(string latestVersion)
    {
        return new VersionValidResponse { Result = VersionValidResult.UnKnown, LatestVersion = latestVersion };
    }
}
