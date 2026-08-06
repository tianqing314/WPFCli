using System.Text;
using TESTRIG.Infrastructure.Data;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 出厂合格证生成器：把一条通过的测试记录（主表 + 测试项明细）渲染为 HTML 合格证。
/// 生成后保存到 <c>AppContext.BaseDirectory/Certificates/</c> 并可用默认浏览器打开打印。
/// </summary>
public static class CertificateBuilder
{
    /// <summary>
    /// 渲染合格证 HTML。
    /// </summary>
    /// <param name="record">主表记录（须通过）。</param>
    /// <param name="steps">该 SN 的测试项明细。</param>
    /// <param name="companyName">公司名（默认占位）。</param>
    /// <returns>完整 HTML 文本。</returns>
    public static string Build(MainTestRecord record, IReadOnlyList<StoredStepDetail> steps, string companyName = "TESTRIG 制造有限公司")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'><title>出厂合格证</title><style>");
        sb.AppendLine("body{font-family:'Microsoft YaHei',sans-serif;margin:32px;color:#222}");
        sb.AppendLine(".cert{border:2px solid #2F6FED;border-radius:8px;padding:28px 36px;max-width:760px;margin:0 auto}");
        sb.AppendLine("h1{text-align:center;color:#2F6FED;margin:0 0 4px 0;font-size:26px}");
        sb.AppendLine(".sub{text-align:center;color:#888;margin-bottom:20px}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-top:14px}");
        sb.AppendLine("th,td{border:1px solid #ccc;padding:6px 10px;font-size:13px}");
        sb.AppendLine("th{background:#EEF3FE;text-align:left}");
        sb.AppendLine(".ok{color:#16A34A;font-weight:bold}.ng{color:#DC2626;font-weight:bold}");
        sb.AppendLine(".meta{display:flex;flex-wrap:wrap;gap:6px 28px;margin-top:14px;font-size:13px;color:#555}");
        sb.AppendLine(".concl{text-align:center;font-size:18px;font-weight:bold;margin-top:20px;color:#16A34A}");
        sb.AppendLine(".foot{display:flex;justify-content:space-between;margin-top:30px;font-size:13px;color:#555}");
        sb.AppendLine("</style></head><body><div class='cert'>");
        sb.AppendLine($"<h1>出 厂 合 格 证</h1><div class='sub'>Certificate of Conformity</div>");
        sb.AppendLine($"<div class='meta'><span>产品型号：{E(record.DeviceModel ?? "-")}</span><span>序列号：{E(record.DeviceSn)}</span>");
        sb.AppendLine($"<span>批次号：{E(record.BatchNo ?? "-")}</span><span>检验员：{E(record.Operator ?? "-")}</span>");
        sb.AppendLine($"<span>检验时间：{record.EndTime:yyyy-MM-dd HH:mm:ss}</span></div>");
        sb.AppendLine("<table><tr><th>#</th><th>检验项目</th><th>状态</th><th>时间</th></tr>");
        var idx = 1;
        foreach (var s in steps)
        {
            var pass = s.ResultStatus is "Success" or "通过" or "Pass";
            var cls = pass ? "ok" : "ng";
            var status = pass ? "通过" : (string.IsNullOrWhiteSpace(s.ErrorMessage) ? s.ResultStatus : "不合格");
            sb.AppendLine($"<tr><td>{idx++}</td><td>{E(s.TestItemName)}</td><td class='{cls}'>{E(status)}</td><td>{s.EndTime:HH:mm:ss}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine(record.IsPass
            ? "<div class='concl'>检验结论：合 格</div>"
            : "<div class='concl' style='color:#DC2626'>检验结论：不合格（不得出厂）</div>");
        sb.AppendLine($"<div class='foot'><span>{E(companyName)}</span><span>检验专用章：____________</span><span>日期：{DateTime.Now:yyyy-MM-dd}</span></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// HTML 转义。
    /// </summary>
    /// <param name="s">原文。</param>
    /// <returns>转义文本。</returns>
    private static string E(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
