using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// HTTP 环境大气压：GET <c>{BaseUrl}/api/constantlymonitor/getmonitordatabyflag?flag={sn}&amp;type=BP</c>，
/// 解析 <c>Data.Val</c> 为大气压，<c>Data.CreatedTime</c> 超过 10 分钟视为过期。逐条 SN 尝试，取首个新鲜值。
/// 迁移自旧 <c>Helper.GetLatestAtoms</c> 的产线传感器取值分支（与 <see cref="HttpEnvironmentTemperature"/> 同接口，仅 type=BP）。
/// </summary>
public sealed class HttpEnvironmentPressure : IEnvironmentPressure
{
    /// <summary>
    /// HTTP 客户端（10s 超时）。
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// 监控服务基址（已去尾斜杠）。
    /// </summary>
    private readonly string _baseUrl;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 用监控服务基址构造。
    /// </summary>
    /// <param name="baseUrl">监控服务基址。</param>
    /// <param name="logger">日志。</param>
    public HttpEnvironmentPressure(string baseUrl, ILogger<HttpEnvironmentPressure> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// 逐条 SN 请求监控服务，返回首个新鲜（10 分钟内）大气压（kPa），全失败返回 null。
    /// </summary>
    /// <param name="sensorSns">温湿度计 SN 集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>大气压（kPa），或 null。</returns>
    public async Task<double?> ReadAsync(IEnumerable<string> sensorSns, CancellationToken ct = default)
    {
        foreach (var sn in sensorSns)
        {
            if (string.IsNullOrWhiteSpace(sn))
            {
                continue;
            }

            try
            {
                var url = $"{_baseUrl}/api/constantlymonitor/getmonitordatabyflag?flag={Uri.EscapeDataString(sn)}&type=BP";
                var json = await _http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // 新鲜度：CreatedTime 超过 10 分钟未更新 → 跳过
                if (data.TryGetProperty("CreatedTime", out var ctEl)
                    && DateTime.TryParse(ctEl.GetString(), out var created)
                    && (DateTime.Now - created).TotalMinutes > 10)
                {
                    _logger.LogWarning("大气压数据过期（>10min）：{Sn}", sn);
                    continue;
                }

                if (data.TryGetProperty("Val", out var valEl))
                {
                    var raw = valEl.ValueKind == JsonValueKind.String ? valEl.GetString() : valEl.GetRawText();
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        return v;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读环境大气压失败：{Sn}", sn);
            }
        }
        return null;
    }
}
