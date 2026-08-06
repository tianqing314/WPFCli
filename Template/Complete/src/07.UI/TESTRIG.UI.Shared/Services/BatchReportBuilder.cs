using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TESTRIG.Infrastructure.Data;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 批次生产测试报告生成器：按批次号从结果库拉全部 SN + 测试项，解析过程日志里的判定行
/// （形如「名=值单位：数值 在范围/超出范围 [下限, 上限]」）汇总成 HTML 报告——总览、直通率、
/// 各测试项合格率、逐指标横向统计（min/max/均值/σ/合格数 + 逐板数值），并按启发式规则给结论建议。
/// 指标名不硬编码，任何板卡通用（按首次出现顺序保留）。自包含 HTML（内联 CSS），可浏览器打开/打印成 PDF。
/// </summary>
public sealed class BatchReportBuilder
{
    /// <summary>结果仓储。</summary>
    private readonly ITestResultStore _store;

    /// <summary>正无穷/负无穷占位（无上限/无下限区间）。</summary>
    private const double Inf = double.PositiveInfinity;

    /// <summary>行首时间戳（解析前剥掉）。</summary>
    private static readonly Regex TimePrefix = new(@"^\d{2}:\d{2}:\d{2}\.\d+\s+", RegexOptions.Compiled);

    /// <summary>判定行：名<空格或=>显示值单位：数值 在范围/超出范围/超差 [下限, 上限]。名与值分隔符兼容空格与 =。</summary>
    private static readonly Regex JudgeLine = new(
        @"^(?<name>[^：:=\s]+)[\s=]+(?<disp>-?\d+(?:\.\d+)?)(?<unit>[^：:]*?)[：:]\s*(?<val>-?\d+(?:\.\d+)?)\s+(?<verb>在范围|超出范围|超差)\s*\[\s*(?<lo>-?\d+(?:\.\d+)?|-∞)\s*,\s*(?<hi>[+\-]?\d+(?:\.\d+)?|\+?∞)\s*\]\s*(?:内)?$",
        RegexOptions.Compiled);

    /// <summary>用结果仓储构造。</summary>
    /// <param name="store">结果仓储。</param>
    public BatchReportBuilder(ITestResultStore store)
    {
        _store = store;
    }

    /// <summary>
    /// 生成批次报告 HTML 文本。
    /// </summary>
    /// <param name="batchNo">批次号。</param>
    /// <param name="useRemote">是否查远程库（与查询页开关一致）。</param>
    /// <param name="deviceModel">设备型号（限定当前测试页对应的被检型号；空则不限）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>HTML 全文；批次无数据返回提示性简报。</returns>
    public async Task<string> BuildHtmlAsync(string batchNo, bool useRemote, string? deviceModel = null, CancellationToken ct = default)
    {
        var records = await FetchAllAsync(batchNo, useRemote, deviceModel, ct);
        var model = new ReportModel(batchNo, useRemote, records.Count);
        if (records.Count == 0)
        {
            return RenderEmpty(model);
        }

        // 逐 SN 取测试项，解析判定指标 + 记录测试项合格情况
        foreach (var rec in records)
        {
            var steps = await _store.GetStepsBySnAsync(rec.DeviceSn, useRemote, ct);
            var board = new BoardResult(rec);
            model.Boards.Add(board);
            foreach (var s in steps)
            {
                board.Items.Add(s);
                model.NoteItem(s.TestItemName, s.ResultStatus == "Success");
                if (board.FirstFail is null && s.ResultStatus != "Success")
                {
                    board.FirstFail = $"{s.TestItemName}（{StatusText(s.ResultStatus)}）{s.ErrorMessage}".Trim();
                }

                ParseMetrics(rec.DeviceSn, s, model);
            }
        }

        return Render(model);
    }

    /// <summary>分页拉全批次主表记录，按开始时间升序。</summary>
    /// <param name="batchNo">批次号。</param>
    /// <param name="useRemote">数据源。</param>
    /// <param name="deviceModel">设备型号（空则不限）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>全部记录。</returns>
    private async Task<List<MainTestRecord>> FetchAllAsync(string batchNo, bool useRemote, string? deviceModel, CancellationToken ct)
    {
        var all = new List<MainTestRecord>();
        const int size = 500;
        var page = 1;
        while (true)
        {
            var res = await _store.QueryMainAsync(new TestRecordFilter(BatchNo: batchNo, DeviceModel: deviceModel), page, size, useRemote, ct);
            all.AddRange(res.Items);
            if (res.Items.Count == 0 || all.Count >= res.Total)
            {
                break;
            }

            page++;
        }

        return all.OrderBy(r => r.StartTime).ToList();
    }

    /// <summary>解析一个测试项过程日志里的所有判定行，登记到指标汇总。</summary>
    /// <param name="sn">被检 SN。</param>
    /// <param name="s">测试项详情。</param>
    /// <param name="model">报告模型。</param>
    private static void ParseMetrics(string sn, StoredStepDetail s, ReportModel model)
    {
        if (string.IsNullOrEmpty(s.ProcessInfos))
        {
            return;
        }

        foreach (var raw in s.ProcessInfos.Split('\n'))
        {
            var line = TimePrefix.Replace(raw.Trim(), "");
            var m = JudgeLine.Match(line);
            if (!m.Success)
            {
                continue;
            }

            var metric = model.Metric(s.TestItemName, m.Groups["name"].Value.Trim(), m.Groups["unit"].Value.Trim());
            metric.Add(sn, new Sample(
                ParseNum(m.Groups["val"].Value),
                ParseNum(m.Groups["lo"].Value),
                ParseNum(m.Groups["hi"].Value),
                m.Groups["verb"].Value == "在范围"));
        }
    }

    /// <summary>解析数值（含 ±∞）。</summary>
    /// <param name="s">文本。</param>
    /// <returns>数值。</returns>
    private static double ParseNum(string s)
    {
        s = s.Trim();
        if (s is "+∞" or "∞")
        {
            return Inf;
        }

        if (s == "-∞")
        {
            return -Inf;
        }

        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>结果状态码 → 中文。</summary>
    /// <param name="status">状态码。</param>
    /// <returns>中文状态。</returns>
    private static string StatusText(string status) => status switch
    {
        "Success" => "通过",
        "MetricFail" => "指标异常",
        "HardwareError" => "工装通讯异常",
        "CommunicationError" => "被检通讯异常",
        _ => status,
    };

    // ===================== 渲染 =====================

    /// <summary>渲染完整报告 HTML。</summary>
    /// <param name="m">报告模型。</param>
    /// <returns>HTML 全文。</returns>
    private static string Render(ReportModel m)
    {
        var pass = m.Boards.Count(b => b.Record.IsPass);
        var fail = m.Boards.Count - pass;
        var rate = m.Boards.Count == 0 ? 0 : pass * 100.0 / m.Boards.Count;

        // 重压后通过：主表 IsRePressed 标记（经重压重测）且最终通过的板。占比 = 占全部样本比例。
        var repressPass = m.Boards.Count(b => b.Record.IsRePressed && b.Record.IsPass);
        var repressRate = m.Boards.Count == 0 ? 0 : repressPass * 100.0 / m.Boards.Count;
        var model = m.Boards.Select(b => b.Record.DeviceModel).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "-";
        var op = m.Boards.Select(b => b.Record.Operator).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "-";

        var sb = new StringBuilder();
        sb.Append(HtmlHead($"批次 {m.BatchNo} 测试报告"));

        // 头部
        sb.Append($"<h1>批次生产测试报告</h1>");
        sb.Append("<div class='meta'>");
        sb.Append($"<span><b>批次号</b>{E(m.BatchNo)}</span>");
        sb.Append($"<span><b>型号</b>{E(model)}</span>");
        sb.Append($"<span><b>数据源</b>{(m.UseRemote ? "远程 MySQL" : "本地 SQLite")}</span>");
        sb.Append($"<span><b>操作员</b>{E(op)}</span>");
        sb.Append($"<span><b>生成时间</b>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</span>");
        sb.Append("</div>");

        // 总览卡片
        sb.Append("<div class='cards'>");
        sb.Append(Card("样本数", m.Boards.Count.ToString(), ""));
        sb.Append(Card("合格", pass.ToString(), "ok"));
        sb.Append(Card("不合格", fail.ToString(), fail > 0 ? "bad" : ""));
        sb.Append(Card("直通率", $"{rate:F1}%", rate >= 95 ? "ok" : rate >= 80 ? "warn" : "bad"));
        sb.Append(Card("重压后通过", repressPass.ToString(), repressPass > 0 ? "warn" : ""));
        sb.Append(Card("重压通过占比", $"{repressRate:F1}%", repressPass > 0 ? "warn" : ""));
        sb.Append("</div>");

        // 一、测试项合格率
        sb.Append("<h2>一、各测试项合格率</h2>");
        sb.Append("<table><thead><tr><th>测试项</th><th>合格/总数</th><th>合格率</th></tr></thead><tbody>");
        foreach (var (name, stat) in m.ItemStats)
        {
            var r2 = stat.Total == 0 ? 0 : stat.Pass * 100.0 / stat.Total;
            var cls = stat.Pass == stat.Total ? "ok" : "bad";
            sb.Append($"<tr><td>{E(name)}</td><td class='mono'>{stat.Pass}/{stat.Total}</td><td class='{cls} b'>{r2:F1}%</td></tr>");
        }

        sb.Append("</tbody></table>");

        // 二、逐指标统计
        sb.Append("<h2>二、逐指标统计</h2>");
        var conclusions = new List<string>();
        foreach (var (item, metrics) in m.MetricsByItem)
        {
            sb.Append($"<h3>{E(item)}</h3>");

            // 逐板数值表
            sb.Append("<div class='scroll'><table><thead><tr><th>SN</th>");
            foreach (var mm in metrics)
            {
                sb.Append($"<th>{E(mm.Name)}{(string.IsNullOrEmpty(mm.Unit) ? "" : $"/{E(mm.Unit)}")}</th>");
            }

            sb.Append("</tr></thead><tbody>");
            foreach (var b in m.Boards)
            {
                sb.Append($"<tr><td class='mono small'>{E(Tail(b.Record.DeviceSn))}</td>");
                foreach (var mm in metrics)
                {
                    if (mm.Samples.TryGetValue(b.Record.DeviceSn, out var sp))
                    {
                        sb.Append($"<td class='mono {(sp.Pass ? "" : "bad b")}'>{G(sp.Val)}{(sp.Pass ? "" : " ✗")}</td>");
                    }
                    else
                    {
                        sb.Append("<td class='muted'>—</td>");
                    }
                }

                sb.Append("</tr>");
            }

            sb.Append("</tbody></table></div>");

            // 统计表
            sb.Append("<table><thead><tr><th>指标</th><th>限值</th><th>n</th><th>最小</th><th>最大</th><th>均值</th><th>σ</th><th>合格</th><th>评估</th></tr></thead><tbody>");
            foreach (var mm in metrics)
            {
                var st = mm.Stats();
                var assess = Assess(mm, st, out var flagged);
                if (flagged)
                {
                    conclusions.Add($"<b>{E(item)} · {E(mm.Name)}</b>：{assess}");
                }

                var cls = st.PassCount < st.N ? "bad" : "";
                sb.Append($"<tr><td>{E(mm.Name)}</td><td class='mono small'>{Limit(mm)}</td><td>{st.N}</td>");
                sb.Append($"<td class='mono'>{G(st.Min)}</td><td class='mono'>{G(st.Max)}</td><td class='mono'>{st.Mean:G4}</td><td class='mono'>{st.Sd:G3}</td>");
                sb.Append($"<td class='mono {cls} b'>{st.PassCount}/{st.N}</td><td class='small'>{assess}</td></tr>");
            }

            sb.Append("</tbody></table>");
        }

        // 三、逐 SN 结果
        sb.Append("<h2>三、逐 SN 结果</h2>");
        sb.Append("<table><thead><tr><th>#</th><th>SN</th><th>工位</th><th>重压</th><th>结果</th><th>首个不合格项</th><th>起止</th></tr></thead><tbody>");
        var idx = 1;
        foreach (var b in m.Boards)
        {
            var r = b.Record;
            var cls = r.IsPass ? "ok" : "bad";
            var res = r.IsPass ? "通过" : "不合格";
            sb.Append($"<tr><td>{idx++}</td><td class='mono'>{E(r.DeviceSn)}</td><td>{E(string.IsNullOrWhiteSpace(r.StationNo) ? "-" : r.StationNo)}</td>");
            sb.Append($"<td class='{(r.IsRePressed ? "warn b" : "muted")}'>{(r.IsRePressed ? "是" : "—")}</td>");
            sb.Append($"<td class='{cls} b'>{res}</td><td>{E(b.FirstFail ?? "")}</td>");
            sb.Append($"<td class='mono small'>{r.StartTime:HH:mm:ss}→{r.EndTime:HH:mm:ss}</td></tr>");
        }

        sb.Append("</tbody></table>");

        // 四、结论与建议
        sb.Append("<h2>四、结论与建议</h2>");
        sb.Append("<ul class='concl'>");
        sb.Append($"<li>批次 <b>{E(m.BatchNo)}</b> 共 {m.Boards.Count} 块，合格 <span class='ok b'>{pass}</span>、不合格 <span class='bad b'>{fail}</span>，直通率 <b>{rate:F1}%</b>。</li>");
        sb.Append($"<li>重压后通过 <b>{repressPass}</b> 块（占全部 {m.Boards.Count} 块样本的 <b>{repressRate:F1}%</b>），指经重压重测后判定通过的板。</li>");
        if (conclusions.Count == 0)
        {
            sb.Append("<li>各指标全部合格且余量充足，无异常项。</li>");
        }
        else
        {
            foreach (var c in conclusions)
            {
                sb.Append($"<li>{c}</li>");
            }
        }

        sb.Append("</ul>");

        sb.Append("<div class='foot'>本报告由系统按过程日志判定行自动汇总生成，评估为启发式结论，仅供参考。</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>启发式评估一条指标，flagged 表示需进结论区。</summary>
    /// <param name="mm">指标。</param>
    /// <param name="st">统计。</param>
    /// <param name="flagged">是否异常（进结论）。</param>
    /// <returns>评估文字。</returns>
    private static string Assess(Metric mm, Stats st, out bool flagged)
    {
        var parts = new List<string>();
        flagged = false;
        var lo = mm.Lo;
        var hi = mm.Hi;

        if (st.PassCount < st.N)
        {
            parts.Add($"{st.N - st.PassCount} 块越界");
            flagged = true;
        }

        // 贴上限 / 超上限
        if (!double.IsInfinity(hi))
        {
            var top = hi - st.Max;
            var thr = st.Sd > 0 ? st.Sd * 2 : (double.IsInfinity(lo) ? System.Math.Abs(hi) * 0.02 : (hi - lo) * 0.05);
            if (top < 0)
            {
                parts.Add($"上限超 {G(-top)}{mm.Unit}");
                flagged = true;
            }
            else if (top < thr)
            {
                parts.Add($"贴上限(余 {G(top)}{mm.Unit})");
                flagged = true;
            }
        }

        // 贴下限 / 低于下限
        if (!double.IsInfinity(lo))
        {
            var bot = st.Min - lo;
            var thr = st.Sd > 0 ? st.Sd * 2 : (double.IsInfinity(hi) ? System.Math.Abs(lo) * 0.02 : (hi - lo) * 0.05);
            if (bot < 0)
            {
                parts.Add($"下限差 {G(-bot)}{mm.Unit}");
                flagged = true;
            }
            else if (bot < thr)
            {
                parts.Add($"贴下限(余 {G(bot)}{mm.Unit})");
                flagged = true;
            }
        }

        // 离散度：σ 相对区间偏大
        if (!double.IsInfinity(lo) && !double.IsInfinity(hi) && hi > lo && st.Sd > (hi - lo) * 0.15)
        {
            parts.Add($"离散偏大(σ={st.Sd:G3})");
            flagged = true;
        }

        return parts.Count == 0 ? "合格，余量充足" : string.Join("；", parts);
    }

    /// <summary>限值文本。</summary>
    private static string Limit(Metric mm)
    {
        var l = double.IsNegativeInfinity(mm.Lo) ? "-∞" : mm.Lo.ToString("G", CultureInfo.InvariantCulture);
        var h = double.IsPositiveInfinity(mm.Hi) ? "+∞" : mm.Hi.ToString("G", CultureInfo.InvariantCulture);
        return $"[{l}, {h}]{E(mm.Unit)}";
    }

    /// <summary>数值紧凑格式。</summary>
    private static string G(double v)
    {
        if (double.IsPositiveInfinity(v))
        {
            return "+∞";
        }

        if (double.IsNegativeInfinity(v))
        {
            return "-∞";
        }

        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>SN 尾段（表格紧凑显示）。</summary>
    private static string Tail(string sn) => sn.Length > 6 ? sn[^6..] : sn;

    /// <summary>HTML 转义。</summary>
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>总览卡片。</summary>
    private static string Card(string label, string value, string cls) =>
        $"<div class='card {cls}'><div class='v'>{E(value)}</div><div class='l'>{E(label)}</div></div>";

    /// <summary>空批次简报。</summary>
    private static string RenderEmpty(ReportModel m)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHead($"批次 {m.BatchNo} 测试报告"));
        sb.Append("<h1>批次生产测试报告</h1>");
        sb.Append($"<div class='meta'><span><b>批次号</b>{E(m.BatchNo)}</span></div>");
        sb.Append("<p class='bad'>该批次在当前数据源下无测试记录。</p>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>HTML 头 + 内联样式 + 打印按钮。</summary>
    private static string HtmlHead(string title) => HeadTemplate.Replace("__TITLE__", E(title));

    /// <summary>HTML 头模板（标题用 __TITLE__ 占位，避免与 CSS 花括号冲突）。</summary>
    private const string HeadTemplate = """
        <!doctype html><html lang="zh"><head><meta charset="utf-8">
        <title>__TITLE__</title>
        <style>
        :root{--bd:#e3e7ee;--mut:#8a93a2;--ok:#1f9d55;--bad:#d64545;--warn:#c77d17;--pri:#2f6fed;--bg:#f5f7fa;}
        *{box-sizing:border-box}body{margin:0;background:var(--bg);color:#1e2430;font:14px/1.55 "Segoe UI","Microsoft YaHei",sans-serif}
        .wrap{max-width:1080px;margin:0 auto;padding:28px 22px 60px}
        h1{font-size:22px;margin:0 0 14px}h2{font-size:17px;margin:30px 0 10px;padding-left:9px;border-left:4px solid var(--pri)}
        h3{font-size:14.5px;margin:18px 0 7px;color:#33405a}
        .meta{display:flex;flex-wrap:wrap;gap:8px 22px;color:#46506a;font-size:13px;margin-bottom:16px}
        .meta span{color:#46506a}.meta b{color:var(--mut);font-weight:600;margin-right:6px}
        .cards{display:flex;gap:12px;flex-wrap:wrap;margin:6px 0 8px}
        .card{flex:1;min-width:120px;background:#fff;border:1px solid var(--bd);border-radius:5px;padding:14px 16px}
        .card .v{font-size:24px;font-weight:700}.card .l{color:var(--mut);font-size:12.5px;margin-top:2px}
        .card.ok .v{color:var(--ok)}.card.bad .v{color:var(--bad)}.card.warn .v{color:var(--warn)}
        table{border-collapse:collapse;width:100%;background:#fff;border:1px solid var(--bd);border-radius:5px;overflow:hidden;margin:4px 0 6px;font-size:13px}
        th,td{padding:6px 10px;text-align:left;border-bottom:1px solid var(--bd)}
        th{background:#eef2f8;color:#46506a;font-weight:600;white-space:nowrap}
        tr:last-child td{border-bottom:0}tbody tr:hover{background:#f7f9fc}
        .mono{font-family:"Cascadia Mono",Consolas,monospace}.small{font-size:12px}.muted,.mut{color:var(--mut)}
        .ok{color:var(--ok)}.bad{color:var(--bad)}.warn{color:var(--warn)}.b{font-weight:700}
        .scroll{overflow-x:auto}
        ul.concl{background:#fff;border:1px solid var(--bd);border-radius:5px;padding:12px 12px 12px 30px;margin:4px 0}
        ul.concl li{margin:3px 0}
        .foot{color:var(--mut);font-size:12px;margin-top:22px}
        .noprint{position:fixed;right:18px;top:14px}
        .noprint button{background:var(--pri);color:#fff;border:0;border-radius:4px;padding:7px 14px;cursor:pointer;font-size:13px}
        @media print{.noprint{display:none}body{background:#fff}.wrap{max-width:none}}
        </style></head><body>
        <div class="noprint"><button onclick="window.print()">打印 / 导出 PDF</button></div>
        <div class="wrap">
        """;
}

// ===================== 报告数据模型 =====================

/// <summary>一次判定采样。</summary>
/// <param name="Val">测量值。</param>
/// <param name="Lo">下限。</param>
/// <param name="Hi">上限。</param>
/// <param name="Pass">是否合格。</param>
internal readonly record struct Sample(double Val, double Lo, double Hi, bool Pass);

/// <summary>一条指标横向汇总（同名指标跨全批次）。</summary>
internal sealed class Metric
{
    /// <summary>指标名。</summary>
    public string Name { get; }

    /// <summary>单位。</summary>
    public string Unit { get; }

    /// <summary>下限（首个样本）。</summary>
    public double Lo { get; private set; } = double.NegativeInfinity;

    /// <summary>上限（首个样本）。</summary>
    public double Hi { get; private set; } = double.PositiveInfinity;

    /// <summary>SN → 采样。</summary>
    public Dictionary<string, Sample> Samples { get; } = new();

    /// <summary>用名与单位构造。</summary>
    /// <param name="name">指标名。</param>
    /// <param name="unit">单位。</param>
    public Metric(string name, string unit)
    {
        Name = name;
        Unit = unit;
    }

    /// <summary>登记一块板的采样（首个样本确定限值）。</summary>
    /// <param name="sn">SN。</param>
    /// <param name="s">采样。</param>
    public void Add(string sn, Sample s)
    {
        if (Samples.Count == 0)
        {
            Lo = s.Lo;
            Hi = s.Hi;
        }

        Samples[sn] = s;
    }

    /// <summary>计算统计量。</summary>
    /// <returns>统计。</returns>
    public Stats Stats()
    {
        var vals = Samples.Values.Select(v => v.Val).Where(v => !double.IsInfinity(v)).ToList();
        var n = vals.Count;
        if (n == 0)
        {
            return new Stats(0, 0, 0, 0, 0, Samples.Values.Count(v => v.Pass));
        }

        var mean = vals.Average();
        var sd = n > 1 ? System.Math.Sqrt(vals.Sum(x => (x - mean) * (x - mean)) / n) : 0;
        return new Stats(n, vals.Min(), vals.Max(), mean, sd, Samples.Values.Count(v => v.Pass));
    }
}

/// <summary>指标统计量。</summary>
/// <param name="N">样本数。</param>
/// <param name="Min">最小。</param>
/// <param name="Max">最大。</param>
/// <param name="Mean">均值。</param>
/// <param name="Sd">总体标准差。</param>
/// <param name="PassCount">合格数。</param>
internal readonly record struct Stats(int N, double Min, double Max, double Mean, double Sd, int PassCount);

/// <summary>测试项合格计数。</summary>
internal sealed class ItemStat
{
    /// <summary>合格数。</summary>
    public int Pass { get; set; }

    /// <summary>总数。</summary>
    public int Total { get; set; }
}

/// <summary>一块板的结果。</summary>
internal sealed class BoardResult
{
    /// <summary>主表记录。</summary>
    public MainTestRecord Record { get; }

    /// <summary>测试项列表。</summary>
    public List<StoredStepDetail> Items { get; } = new();

    /// <summary>首个不合格项描述（无则 null）。</summary>
    public string? FirstFail { get; set; }

    /// <summary>用主表记录构造。</summary>
    /// <param name="record">主表记录。</param>
    public BoardResult(MainTestRecord record)
    {
        Record = record;
    }
}

/// <summary>报告模型（保持测试项/指标首次出现顺序）。</summary>
internal sealed class ReportModel
{
    /// <summary>批次号。</summary>
    public string BatchNo { get; }

    /// <summary>数据源是否远程。</summary>
    public bool UseRemote { get; }

    /// <summary>批次记录数。</summary>
    public int RecordCount { get; }

    /// <summary>各板结果（按开始时间序）。</summary>
    public List<BoardResult> Boards { get; } = new();

    /// <summary>测试项合格统计（有序）。</summary>
    public List<KeyValuePair<string, ItemStat>> ItemStats { get; } = new();
    private readonly Dictionary<string, ItemStat> _itemIndex = new();

    /// <summary>各测试项下的指标（有序）。</summary>
    public List<KeyValuePair<string, List<Metric>>> MetricsByItem { get; } = new();
    private readonly Dictionary<string, List<Metric>> _itemMetrics = new();
    private readonly Dictionary<(string, string), Metric> _metricIndex = new();

    /// <summary>用批次号/数据源/记录数构造。</summary>
    /// <param name="batchNo">批次号。</param>
    /// <param name="useRemote">是否远程。</param>
    /// <param name="recordCount">记录数。</param>
    public ReportModel(string batchNo, bool useRemote, int recordCount)
    {
        BatchNo = batchNo;
        UseRemote = useRemote;
        RecordCount = recordCount;
    }

    /// <summary>登记一个测试项的合格情况。</summary>
    /// <param name="itemName">测试项名。</param>
    /// <param name="pass">是否合格。</param>
    public void NoteItem(string itemName, bool pass)
    {
        if (!_itemIndex.TryGetValue(itemName, out var stat))
        {
            stat = new ItemStat();
            _itemIndex[itemName] = stat;
            ItemStats.Add(new KeyValuePair<string, ItemStat>(itemName, stat));
        }

        stat.Total++;
        if (pass)
        {
            stat.Pass++;
        }
    }

    /// <summary>取（测试项,指标名）对应的指标汇总，首次出现即建并入序。</summary>
    /// <param name="item">测试项名。</param>
    /// <param name="name">指标名。</param>
    /// <param name="unit">单位。</param>
    /// <returns>指标汇总。</returns>
    public Metric Metric(string item, string name, string unit)
    {
        var key = (item, name);
        if (_metricIndex.TryGetValue(key, out var metric))
        {
            return metric;
        }

        metric = new Metric(name, unit);
        _metricIndex[key] = metric;
        if (!_itemMetrics.TryGetValue(item, out var list))
        {
            list = new List<Metric>();
            _itemMetrics[item] = list;
            MetricsByItem.Add(new KeyValuePair<string, List<Metric>>(item, list));
        }

        list.Add(metric);
        return metric;
    }
}
