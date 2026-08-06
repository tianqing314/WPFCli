using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Infrastructure.Data;
using TESTRIG.UI.Shared.Services;
using TESTRIG.UI.Shared.Views;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 数据查看：按 时间范围(精确到秒)/批次号/测试结果 分页查询主表（页容量≤100）。
/// 双击一条 SN 记录 → 弹出该 SN 的测试项列表 → 双击/详情 → 复用运行时的测试项过程详情弹窗。
/// </summary>
public partial class DataQueryViewModel : ObservableObject
{
    /// <summary>
    /// 测试结果仓储。
    /// </summary>
    private readonly ITestResultStore _store;

    /// <summary>
    /// 当前测试页对应的被检型号（Manifest 的 Dut.Model）；空则不按型号过滤。
    /// 查询恒定按它过滤——只看当前切换到的设备型号的数据。
    /// </summary>
    private readonly string? _deviceModel;

    /// <summary>
    /// 注入仓储并首次加载查询结果。
    /// </summary>
    /// <param name="store">测试结果仓储。</param>
    /// <param name="deviceModel">当前测试页的被检型号（空则查全部型号）。</param>
    /// <param name="isAdmin">是否 admin/测试账号登录（决定删除图标是否显示）。</param>
    public DataQueryViewModel(ITestResultStore store, string? deviceModel = null, bool isAdmin = false)
    {
        _store = store;
        _deviceModel = string.IsNullOrWhiteSpace(deviceModel) ? null : deviceModel.Trim();
        IsAdmin = isAdmin;
        _ = LoadAsync();
    }

    /// <summary>
    /// 是否 admin/测试账号登录：仅 admin 的操作列显示删除图标（详情图标所有账号都显示）。
    /// </summary>
    public bool IsAdmin { get; }

    /// <summary>
    /// 型号范围提示（标题栏旁展示，说明本页只查当前型号）。
    /// </summary>
    public string ModelScopeText => _deviceModel is null ? "全部型号" : _deviceModel;

    // ===== 查询条件 =====

    /// <summary>
    /// 起始日期（默认近一个月：今天往前一个月）。
    /// </summary>
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddMonths(-1);

    /// <summary>
    /// 起始时间（默认 00:00:00）。
    /// </summary>
    [ObservableProperty] private DateTime? _fromTime = DateTime.Today.AddMonths(-1);

    /// <summary>
    /// 结束日期。
    /// </summary>
    [ObservableProperty] private DateTime _toDate = DateTime.Today;

    /// <summary>
    /// 结束时间（默认 23:59:59）。
    /// </summary>
    [ObservableProperty] private DateTime? _toTime = DateTime.Today.AddDays(1).AddSeconds(-1);

    /// <summary>
    /// 批次号（空则不过滤）。
    /// </summary>
    [ObservableProperty] private string _batchNo = "";

    /// <summary>
    /// 结果过滤索引：0=全部 1=通过 2=不合格。
    /// </summary>
    [ObservableProperty] private int _resultIndex;

    /// <summary>
    /// 结果过滤下拉选项。
    /// </summary>
    public string[] ResultOptions { get; } = ["全部", "通过", "不合格"];

    /// <summary>
    /// 数据源开关：false=本地 SQLite（默认），true=远程 MySQL。
    /// 打开时若远程库未配置则弹提示并回退本地。
    /// </summary>
    [ObservableProperty] private bool _useRemote;

    /// <summary>
    /// 远程库是否可用（供 UI 提示，不禁用开关——未配置时打开会提示后回退）。
    /// </summary>
    public bool RemoteAvailable => _store.RemoteAvailable;

    /// <summary>
    /// 加载遮罩副标题（随数据源变化）。
    /// </summary>
    public string SourceHint => UseRemote ? "连接远程 MySQL · 拉取结果集" : "连接本地 SQLite · 拉取结果集";

    /// <summary>
    /// 开关切换时的守卫：打开远程但未配置 → 提示并回退本地；有效切换则重查第一页。
    /// </summary>
    /// <param name="value">切换后的值。</param>
    partial void OnUseRemoteChanged(bool value)
    {
        OnPropertyChanged(nameof(SourceHint));
        if (value && !_store.RemoteAvailable)
        {
            AppDialog.Info("远程库未配置", "尚未配置远程数据库连接（RemoteSync 连接串）。\n请先在应用配置中填写远程库连接后再切换到远程数据。");
            UseRemote = false;
            return;
        }

        Page = 1;
        _ = LoadAsync();
    }

    /// <summary>
    /// 每页条数。
    /// </summary>
    [ObservableProperty] private int _pageSize = 20;

    /// <summary>
    /// 每页条数下拉选项。
    /// </summary>
    public int[] PageSizeOptions { get; } = [20, 50, 100];

    // ===== 分页结果 =====

    /// <summary>
    /// 当前页记录集合。
    /// </summary>
    public ObservableCollection<RecordRowViewModel> Records { get; } = [];

    /// <summary>
    /// 当前页码（从 1 起）。
    /// </summary>
    [ObservableProperty] private int _page = 1;

    /// <summary>
    /// 命中总条数。
    /// </summary>
    [ObservableProperty] private int _total;

    /// <summary>
    /// 总页数（至少 1）。
    /// </summary>
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));

    /// <summary>
    /// 状态栏文字（条数/页数）。
    /// </summary>
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// 是否正在查询（驱动加载缓冲遮罩、按钮禁用防重复）。
    /// </summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// 查询：回到第一页并加载。
    /// </summary>
    [RelayCommand]
    private async Task Query()
    {
        Page = 1;
        await LoadAsync();
    }

    /// <summary>
    /// 上一页。
    /// </summary>
    [RelayCommand]
    private async Task PrevPage()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadAsync();
    }

    /// <summary>
    /// 下一页。
    /// </summary>
    [RelayCommand]
    private async Task NextPage()
    {
        if (Page >= PageCount)
        {
            return;
        }

        Page++;
        await LoadAsync();
    }

    /// <summary>
    /// 按当前条件与页码查询主表并刷新集合与状态。
    /// </summary>
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var filter = new TestRecordFilter(
                Combine(FromDate, FromTime),
                Combine(ToDate, ToTime),
                string.IsNullOrWhiteSpace(BatchNo) ? null : BatchNo.Trim(),
                ResultIndex switch { 1 => true, 2 => false, _ => (bool?)null },
                _deviceModel);

            var pageRes = await _store.QueryMainAsync(filter, Page, PageSize, UseRemote);
            Records.Clear();
            foreach (var r in pageRes.Items)
            {
                Records.Add(new RecordRowViewModel(r));
            }

            Total = pageRes.Total;
            OnPropertyChanged(nameof(PageCount));
            Status = $"[{(UseRemote ? "远程库" : "本地库")}·{ModelScopeText}] 共 {Total} 条，第 {Page}/{PageCount} 页";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 一键生成当前批次的生产测试报告（HTML）：需先填批次号；生成后保存并用默认浏览器打开。
    /// </summary>
    [RelayCommand]
    private async Task GenerateBatchReport()
    {
        var batch = BatchNo.Trim();
        if (string.IsNullOrEmpty(batch))
        {
            AppDialog.Info("请先填批次号", "批次报告按批次号汇总，请在上方「批次号」填入后再生成。");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var html = await new BatchReportBuilder(_store).BuildHtmlAsync(batch, UseRemote, _deviceModel);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存批次测试报告",
                FileName = $"批次{batch}_测试报告_{DateTime.Now:yyyyMMdd_HHmmss}.html",
                Filter = "HTML 报告 (*.html)|*.html|所有文件 (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true)
            {
                return;
            }

            await File.WriteAllTextAsync(dlg.FileName, html, Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.Info("生成报告失败", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 双击一条记录 → 该 SN 的测试项列表。
    /// </summary>
    /// <param name="row">被双击的记录行。</param>
    [RelayCommand]
    private void OpenSnRecord(RecordRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var vm = new SnRecordViewModel(_store, row.DeviceSn, UseRemote);
        new SnRecordWindow(vm) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 删除一条记录（仅 admin 可见入口）：二次确认后删掉当前数据源里该 SN 的主表行 + 全部测试详情行，然后刷新当前页。
    /// </summary>
    /// <param name="row">待删除的记录行。</param>
    [RelayCommand]
    private async Task DeleteRecord(RecordRowViewModel? row)
    {
        if (row is null || IsBusy)
        {
            return;
        }

        var source = UseRemote ? "远程数据库" : "本地数据库";
        if (!AppDialog.ConfirmDanger("删除测试记录",
                $"将从{source}永久删除该记录及其全部测试详情，删除后无法恢复。\n\n"
                + $"设备SN：{row.DeviceSn}\n批次号：{row.BatchNo}\n型号：{row.DeviceModel}\n结果：{row.ResultText}\n\n确认删除？"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = await _store.DeleteBySnAsync(row.DeviceSn, UseRemote);
            if (deleted.Main == 0 && deleted.Details == 0)
            {
                AppDialog.Info("未删除任何数据", $"{source}中未找到 SN「{row.DeviceSn}」的记录，可能已被删除。");
            }

            // 删掉本页最后一条时回退一页，避免停在空页
            if (Records.Count <= 1 && Page > 1)
            {
                Page--;
            }
        }
        catch (Exception ex)
        {
            AppDialog.Error("删除失败", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    /// <summary>
    /// 合并日期与时间为完整时间戳（时间缺省用 00:00:00）。
    /// </summary>
    /// <param name="date">日期。</param>
    /// <param name="time">时间。</param>
    /// <returns>合并后的时间戳。</returns>
    private static DateTime? Combine(DateTime date, DateTime? time)
    {
        return date.Date + (time?.TimeOfDay ?? TimeSpan.Zero);
    }
}

/// <summary>
/// 主表一行（带显示格式）。
/// </summary>
public sealed class RecordRowViewModel(MainTestRecord r)
{
    /// <summary>
    /// 设备序列号。
    /// </summary>
    public string DeviceSn { get; } = r.DeviceSn;

    /// <summary>
    /// 批次号。
    /// </summary>
    public string? BatchNo { get; } = r.BatchNo;

    /// <summary>
    /// 设备型号。
    /// </summary>
    public string? DeviceModel { get; } = r.DeviceModel;

    /// <summary>
    /// 结果文字（通过/不合格）。
    /// </summary>
    public string ResultText { get; } = r.IsPass ? "通过" : "不合格";

    /// <summary>
    /// 结果着色（通过绿/不合格红）。
    /// </summary>
    public Brush ResultBrush { get; } = r.IsPass ? Brushes.SeaGreen : Brushes.Firebrick;

    /// <summary>
    /// 重压标记文字（重压过显示「是」，否则空）。
    /// </summary>
    public string RePressText { get; } = r.IsRePressed ? "是" : "";

    /// <summary>
    /// 重压标记着色（重压橙色醒目）。
    /// </summary>
    public Brush RePressBrush { get; } = r.IsRePressed ? Brushes.DarkOrange : Brushes.Transparent;

    /// <summary>
    /// 操作员。
    /// </summary>
    public string? Operator { get; } = r.Operator;

    /// <summary>
    /// 开始时间（格式化）。
    /// </summary>
    public string StartTime { get; } = r.StartTime.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 结束时间（格式化）。
    /// </summary>
    public string EndTime { get; } = r.EndTime.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// 某 SN 的所有测试项列表。
/// </summary>
public partial class SnRecordViewModel : ObservableObject
{
    /// <summary>
    /// 测试结果仓储。
    /// </summary>
    private readonly ITestResultStore _store;

    /// <summary>
    /// 数据源：true 查远程 MySQL 库，false 查本地 SQLite（与查询页开关一致）。
    /// </summary>
    private readonly bool _useRemote;

    /// <summary>
    /// 注入仓储与 SN 并加载该 SN 的测试项。
    /// </summary>
    /// <param name="store">测试结果仓储。</param>
    /// <param name="sn">设备序列号。</param>
    /// <param name="useRemote">是否查远程库（与查询页开关一致）。</param>
    public SnRecordViewModel(ITestResultStore store, string sn, bool useRemote = false)
    {
        _store = store;
        DeviceSn = sn;
        _useRemote = useRemote;
        _ = LoadAsync();
    }

    /// <summary>
    /// 设备序列号。
    /// </summary>
    public string DeviceSn { get; }

    /// <summary>
    /// 该 SN 的测试项行集合。
    /// </summary>
    public ObservableCollection<SnStepRowViewModel> Items { get; } = [];

    /// <summary>
    /// 按 SN 加载测试项并编号填充集合。
    /// </summary>
    private async Task LoadAsync()
    {
        var steps = await _store.GetStepsBySnAsync(DeviceSn, _useRemote);
        Items.Clear();
        var order = 1;
        foreach (var s in steps)
        {
            Items.Add(new SnStepRowViewModel(order++, s));
        }
    }

    /// <summary>
    /// 详情/双击测试项 → 复用运行时的过程详情弹窗（过程日志 + 过程数据曲线）。
    /// </summary>
    /// <param name="row">被选中的测试项行。</param>
    [RelayCommand]
    private void ShowDetail(SnStepRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var vm = new StepDetailViewModel(row.Order, row.Stored);
        new StepDetailWindow(vm) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 一键导出所有测试项详情到 TXT 文件。
    /// </summary>
    [RelayCommand]
    private void Export()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出测试项详情",
            FileName = $"{DeviceSn}_测试项详情.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var writer = new StreamWriter(dlg.FileName, false, Encoding.UTF8);

            writer.WriteLine("=".PadRight(60, '='));
            writer.WriteLine("  SN 测试项详情");
            writer.WriteLine($"  设备 SN：{DeviceSn}");
            writer.WriteLine($"  导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"  测试项数：{Items.Count}");
            writer.WriteLine("=".PadRight(60, '='));
            writer.WriteLine();

            foreach (var item in Items)
            {
                writer.WriteLine($"[{item.Order}] {item.Name}");
                writer.WriteLine($"  {"状态：",-10}{item.StatusText}");
                writer.WriteLine($"  {"开始：",-10}{item.StartTime}");
                writer.WriteLine($"  {"结束：",-10}{item.EndTime}");
                if (!string.IsNullOrWhiteSpace(item.Stored.ProcessInfos))
                {
                    writer.WriteLine($"  {"过程日志：",-10}");
                    foreach (var line in item.Stored.ProcessInfos.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        writer.WriteLine($"    {line.Trim()}");
                    }
                }
                if (!string.IsNullOrWhiteSpace(item.Stored.ErrorMessage))
                {
                    writer.WriteLine($"  {"错误信息：",-10}{item.Stored.ErrorMessage}");
                }
                if (!string.IsNullOrWhiteSpace(item.Stored.ProcessDataJson))
                {
                    writer.WriteLine($"  {"过程数据：",-10}有 {item.Stored.ProcessDataJson.Length} 字节");
                }
                writer.WriteLine();
            }

            writer.WriteLine("-".PadRight(60, '-'));
            writer.WriteLine("导出完成");

            MessageBox.Show($"已导出到：{dlg.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>
/// SN 测试项列表一行。
/// </summary>
public sealed class SnStepRowViewModel(int order, StoredStepDetail s)
{
    /// <summary>
    /// 序号。
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// 原始存储的测试项详情。
    /// </summary>
    public StoredStepDetail Stored { get; } = s;

    /// <summary>
    /// 测试项名称。
    /// </summary>
    public string Name { get; } = s.TestItemName;

    /// <summary>
    /// 状态文字（结果状态映射为中文）。
    /// </summary>
    public string StatusText { get; } = s.ResultStatus switch
    {
        "Success" => "通过",
        "MetricFail" => "指标异常",
        "HardwareError" => "工装通讯异常",
        "CommunicationError" => "被检通讯异常",
        _ => s.ResultStatus,
    };

    /// <summary>
    /// 状态着色（通过绿/其余红）。
    /// </summary>
    public Brush StatusBrush { get; } = s.ResultStatus == "Success" ? Brushes.SeaGreen : Brushes.Firebrick;

    /// <summary>
    /// 开始时间（格式化）。
    /// </summary>
    public string StartTime { get; } = s.StartTime.ToString("HH:mm:ss");

    /// <summary>
    /// 结束时间（格式化）。
    /// </summary>
    public string EndTime { get; } = s.EndTime.ToString("HH:mm:ss");
}
