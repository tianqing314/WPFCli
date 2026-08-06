using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TESTRIG.Core.Abstractions;
using TESTRIG.Infrastructure.Data;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 测试项过程详情：
/// 运行中（未完成）→ 直接镜像该测试项的实时日志与实时采集曲线；
/// 已完成 → 从数据库按 SN+测试项加载历史的过程日志与过程数据曲线。
/// </summary>
public partial class StepDetailViewModel : ObservableObject
{
    /// <summary>
    /// 运行页传入的实时单元格（数据查看路径为 null）。
    /// </summary>
    private readonly StepCellViewModel? _cell;

    /// <summary>
    /// 被检序列号（历史加载用）。
    /// </summary>
    private readonly string? _sn;

    /// <summary>
    /// 结果存储（历史加载用）。
    /// </summary>
    private readonly ITestResultStore? _store;

    /// <summary>
    /// 是否已预加载（数据查看路径构造时已加载）。
    /// </summary>
    private bool _preloaded;

    /// <summary>
    /// 过程数据反序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 运行页双击：传入实时单元格 + SN + 库，由 <see cref="InitializeAsync"/> 决定实时/历史。
    /// </summary>
    /// <param name="cell">实时测试项单元格。</param>
    /// <param name="sn">被检序列号。</param>
    /// <param name="store">结果存储。</param>
    public StepDetailViewModel(StepCellViewModel cell, string? sn, ITestResultStore store)
    {
        _cell = cell;
        _sn = sn;
        _store = store;
        Order = cell.Order;
        Name = cell.Name;
        Status = cell.Status;
        StatusBrush = cell.StatusBrush;
        Description = cell.Description;
        ConditionsText = cell.ConditionsText;
        ErrorMessage = cell.ErrorMessage;
    }

    /// <summary>
    /// 数据查看：直接用库里取出的历史明细构建（无需实时单元格）。
    /// </summary>
    /// <param name="order">测试项序号。</param>
    /// <param name="stored">历史明细。</param>
    public StepDetailViewModel(int order, StoredStepDetail stored)
    {
        Order = order;
        Name = stored.TestItemName;
        Status = "";
        StatusBrush = Brushes.Gray;
        LoadFromStored(stored);
        _preloaded = true;
    }

    /// <summary>
    /// 测试项序号。
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// 测试项名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 结果状态（中文）。
    /// </summary>
    [ObservableProperty] private string _status;

    /// <summary>
    /// 状态颜色。
    /// </summary>
    [ObservableProperty] private Brush _statusBrush;

    /// <summary>
    /// 来源提示（实时 / 历史记录）。
    /// </summary>
    [ObservableProperty] private string _sourceHint = "";

    /// <summary>
    /// 测试项描述（运行页路径有；历史记录路径库未存，为空）。
    /// </summary>
    public string Description { get; private set; } = "";

    /// <summary>
    /// 条件指标（多行文本；运行页路径有，历史记录路径为空）。
    /// </summary>
    public string ConditionsText { get; private set; } = "";

    /// <summary>
    /// 错误信息（不通过/异常时显示，通过时为空）。
    /// </summary>
    [ObservableProperty] private string _errorMessage = "";

    /// <summary>
    /// 过程日志（实时或从库加载）。
    /// </summary>
    public ObservableCollection<LogLineViewModel> Details { get; private set; } = [];

    /// <summary>
    /// 过程数据曲线（实时增长，或从库一次性加载）。
    /// </summary>
    [ObservableProperty] private ProcessDataSeries? _series;

    /// <summary>
    /// 是否有过程数据（有采样点才为真）。无则界面隐藏「过程数据」曲线区。
    /// </summary>
    public bool HasProcessData => Series is { TimeSec.Count: > 0 };

    /// <summary>
    /// Series 变化时同步通知 <see cref="HasProcessData"/>（实时曲线首个采样点到达时才显示曲线区）。
    /// </summary>
    /// <param name="value">新曲线。</param>
    partial void OnSeriesChanged(ProcessDataSeries? value)
    {
        OnPropertyChanged(nameof(HasProcessData));
    }

    /// <summary>
    /// 初始化：已完成且库中有记录走历史模式，否则实时模式。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_preloaded || _cell is null)
        {
            // 数据查看路径已直接加载
            return;
        }
        // 已完成且能从库取到 → 历史模式
        if (_cell.IsFinished && !string.IsNullOrEmpty(_sn) && _store is not null)
        {
            var stored = await _store.GetLatestStepAsync(_sn, _cell.Key);
            if (stored is not null)
            {
                LoadFromStored(stored);
                return;
            }
        }
        // 否则 → 实时模式（运行中，或本次会话尚未落库）
        LoadLive();
    }

    /// <summary>
    /// 实时模式：绑定实时单元格的日志/曲线，订阅其变化。
    /// </summary>
    private void LoadLive()
    {
        SourceHint = "实时";
        // 同一实例，绑定后实时追加
        Details = _cell!.Details;
        Series = _cell.ProcessData;
        ErrorMessage = _cell.ErrorMessage;
        OnPropertyChanged(nameof(Details));
        // 曲线随采样点增长
        _cell.PropertyChanged += OnCellChanged;
    }

    /// <summary>
    /// 实时单元格属性变化 → 同步曲线/状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">属性变化参数。</param>
    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StepCellViewModel.ProcessData))
        {
            Series = _cell!.ProcessData;
        }
        else if (e.PropertyName == nameof(StepCellViewModel.Status))
        {
            Status = _cell!.Status;
            StatusBrush = _cell.StatusBrush;
        }
        else if (e.PropertyName == nameof(StepCellViewModel.ErrorMessage))
        {
            ErrorMessage = _cell!.ErrorMessage;
        }
    }

    /// <summary>
    /// 历史模式：从库明细加载日志与曲线。
    /// </summary>
    /// <param name="stored">历史明细。</param>
    private void LoadFromStored(StoredStepDetail stored)
    {
        SourceHint = $"历史记录（数据库） {stored.StartTime:yyyy-MM-dd HH:mm:ss}";
        Status = ZhStatus(stored.ResultStatus);
        StatusBrush = stored.ResultStatus == "Success" ? Brushes.SeaGreen : Brushes.Firebrick;
        ErrorMessage = stored.ErrorMessage ?? "";

        var lines = new ObservableCollection<LogLineViewModel>();
        foreach (var raw in (stored.ProcessInfos ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // 入库格式："HH:mm:ss.fff message"
            if (raw.Length > 13 && raw[12] == ' ')
            {
                lines.Add(new LogLineViewModel(raw[..12], raw[13..]));
            }
            else
            {
                lines.Add(new LogLineViewModel("", raw));
            }
        }

        Details = lines;
        OnPropertyChanged(nameof(Details));

        if (!string.IsNullOrEmpty(stored.ProcessDataJson))
        {
            Series = JsonSerializer.Deserialize<ProcessDataSeries>(stored.ProcessDataJson, Json);
        }
    }

    /// <summary>
    /// 结果状态英文 → 中文。
    /// </summary>
    /// <param name="status">英文状态。</param>
    /// <returns>中文状态。</returns>
    private static string ZhStatus(string status)
    {
        return status switch
        {
            "Success" => "通过",
            "MetricFail" => "指标异常",
            "HardwareError" => "工装通讯异常",
            "CommunicationError" => "被检通讯异常",
            _ => status,
        };
    }
}
