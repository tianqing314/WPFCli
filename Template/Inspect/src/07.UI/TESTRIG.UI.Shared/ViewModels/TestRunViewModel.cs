using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Automation;
using TESTRIG.Core.Abstractions;
using TESTRIG.Core.Engine;
using TESTRIG.Devices;
using TESTRIG.Devices.Comm;
using TESTRIG.Infrastructure.Auth;
using TESTRIG.Infrastructure.Configuration;
using TESTRIG.Infrastructure.Data;
using TESTRIG.Infrastructure.Notifications;
using TESTRIG.UI.Shared.BoardExtras;
using TESTRIG.UI.Shared.Services;
using TESTRIG.UI.Shared.Views;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 测试运行页：连接状态栏 + 模式工具栏（全自动/手动/工位/计数）+ 多号位并行进度表 + 分工位日志。
/// </summary>
public partial class TestRunViewModel : ObservableObject
{
    /// <summary>
    /// 测试执行器。
    /// </summary>
    private readonly TestRunner _runner;

    /// <summary>
    /// 测试结果仓储。
    /// </summary>
    private readonly ITestResultStore _store;

    /// <summary>
    /// 当前针床清单。
    /// </summary>
    private readonly JigManifest _manifest;

    /// <summary>
    /// 连接管理器（共享设备连接）。
    /// </summary>
    private readonly ConnectionManager _conn;

    /// <summary>
    /// 连接配置仓储。
    /// </summary>
    private readonly IConnectionConfigStore _connStore;

    /// <summary>
    /// 整机测试编排器（手动单次 + 计数）。
    /// </summary>
    private readonly AutomationOrchestrator _orch;

    /// <summary>
    /// 通知服务（状态栏推送）。
    /// </summary>
    private readonly INotificationService _notify;

    /// <summary>
    /// 设备扫描器（串口/USB）。
    /// </summary>
    private readonly IDeviceScanner _scanner;

    /// <summary>
    /// 当前用户会话（操作员）。
    /// </summary>
    private readonly IUserSession _session;

    /// <summary>
    /// 全自动计数器（通过/不合格/复测/平均耗时）。
    /// </summary>
    private readonly AutomationCounters _counters = new();

    /// <summary>
    /// 整机运行/单测 共用，「停止」取消它。
    /// </summary>
    private CancellationTokenSource? _runCts;

    /// <summary>
    /// UI 线程调度器。
    /// </summary>
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    /// <summary>
    /// 构造运行页：按号位构建进度视图、初始化连接状态，并订阅执行器的进度/消息/采样事件。
    /// </summary>
    /// <param name="manifest">当前整机清单。</param>
    /// <param name="runner">测试执行器。</param>
    /// <param name="store">测试结果仓储。</param>
    /// <param name="conn">连接管理器。</param>
    /// <param name="connStore">连接配置仓储。</param>
    /// <param name="orch">整机测试编排器。</param>
    /// <param name="notify">通知服务。</param>
    /// <param name="scanner">设备扫描器。</param>
    /// <param name="session">用户会话。</param>
    /// <param name="boardExtras">板卡专属工具栏扩展的提供者集合（框架侧不认识具体板卡，逐个问谁支持当前针床）。</param>
    /// <param name="hwOptions">硬件选项（用于区分真机/仿真模式）。</param>
    public TestRunViewModel(JigManifest manifest, TestRunner runner, ITestResultStore store,
        ConnectionManager conn, IConnectionConfigStore connStore, AutomationOrchestrator orch,
        INotificationService notify, IDeviceScanner scanner, IUserSession session,
        IEnumerable<IBoardToolbarExtraProvider> boardExtras,
        HardwareOptions? hwOptions = null)
    {
        _manifest = manifest;
        _runner = runner;
        _store = store;
        _conn = conn;
        _connStore = connStore;
        _orch = orch;
        _notify = notify;
        _scanner = scanner;
        _session = session;
        _dispatcher = Application.Current.Dispatcher;

        // 本套针床若有专属工具栏扩展则挂上（没有就是 null，工具栏与原来一致）
        BoardToolbarExtra = boardExtras.FirstOrDefault(p => p.Supports(manifest))?.Create(manifest);

        Title = $"{manifest.DeviceFamily} · {manifest.BoardName}";
        foreach (var pos in manifest.Positions.OrderBy(p => p.Index))
        {
            Positions.Add(new PositionViewModel(pos, manifest.Steps));
        }

        foreach (var p in Positions)
        {
            // 单工位显示过程日志列
            p.ShowLog = Positions.Count == 1;
        }

        BoxConnected = conn.IsBoxConnected;
        _counters.Changed += () => _dispatcher.Invoke(RefreshCounters);

        _runner.StepChanged += OnStepChanged;
        _runner.Message += OnMessage;
        _runner.SampleReported += OnSample;
    }

    // ===== 模式工具栏 / 计数器 =====

    /// <summary>
    /// 把「是否正在测试」同步给板卡专属工具栏扩展。
    /// </summary>
    private void SyncExtraBusy()
    {
        if (BoardToolbarExtra is { } extra)
        {
            extra.IsBusy = IsBusy;
        }
    }

    /// <summary>
    /// 通过计数。
    /// </summary>
    [ObservableProperty] private int _passedCount;

    /// <summary>
    /// 不合格计数。
    /// </summary>
    [ObservableProperty] private int _failedCount;

    /// <summary>
    /// 复测计数。
    /// </summary>
    [ObservableProperty] private int _retriedCount;

    /// <summary>
    /// 平均单块耗时文字。
    /// </summary>
    [ObservableProperty] private string _avgText = "0.0s";

    /// <summary>
    /// 任务栏进度值（0.0~1.0）。
    /// </summary>
    [ObservableProperty] private double _taskbarProgressValue;

    /// <summary>
    /// 任务栏进度状态：Normal（测试中绿色）、Error（不合格变红）、None（闲置）。
    /// </summary>
    [ObservableProperty] private TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;

    /// <summary>
    /// 正在测试（整机手动运行）。
    /// </summary>
    public bool IsBusy => IsRunning;

    /// <summary>
    /// 开始/结束 合一按钮的文字。
    /// </summary>
    public string StartStopText => IsBusy ? "结束测试" : "开始测试";

    /// <summary>
    /// 测试进行中不允许切换 全自动/手动 模式。
    /// </summary>
    public bool CanToggleMode => !IsBusy;

    /// <summary>
    /// 从计数器刷新界面计数与平均耗时。
    /// </summary>
    private void RefreshCounters()
    {
        PassedCount = _counters.Passed;
        FailedCount = _counters.Failed;
        RetriedCount = _counters.Retried;
        AvgText = $"{_counters.AvgSeconds:0.0}s";
    }

    /// <summary>
    /// 批次号：支持手动输入。
    /// </summary>
    [ObservableProperty] private string _batchNumber = "";

    /// <summary>
    /// 全自动新一块拼版开始：刷新各号位 SN 并清空上一块板的进度/过程信息/曲线。
    /// </summary>
    /// <param name="sns">号位 → SN 映射。</param>
    private void StartNewCycle(IReadOnlyDictionary<int, string> sns)
    {
        foreach (var pos in Positions)
        {
            if (sns.TryGetValue(pos.Index, out var sn))
            {
                pos.Reset();
                pos.SerialNumber = sn;
            }
        }
        // 新板测试开始时任务栏进度归零，之前完成的进度条不残留
        TaskbarProgressValue = 0;
        TaskbarProgressState = TaskbarItemProgressState.None;
    }

    /// <summary>
    /// 每次运行为各勾选号位生成唯一 SN（yyyyMMddHHmmss-号位号，保证每块板唯一）。
    /// </summary>
    private void GenerateSerialNumbers()
    {
        var selected = Positions.Where(p => p.IsSelected).Select(p => p.Index).ToList();
        var sns = SerialNumberFactory.Generate(selected);
        foreach (var p in Positions.Where(p => p.IsSelected))
        {
            p.SerialNumber = sns[p.Index];
        }
    }

    /// <summary>
    /// 仅跑勾选的号位；全选则不过滤。带各号位 SN + 落库元数据。
    /// </summary>
    /// <returns>运行选项。</returns>
    private RunOptions BuildOptions()
    {
        var selected = Positions.Where(p => p.IsSelected).ToList();
        var positions = selected.Count == Positions.Count ? null : selected.Select(p => p.Index).ToList();
        var sns = selected.Where(p => !string.IsNullOrEmpty(p.SerialNumber))
                          .ToDictionary(p => p.Index, p => p.SerialNumber!);
        return new RunOptions
        {
            Positions = positions,
            SerialNumbers = sns.Count > 0 ? sns : null,
            BatchNo = BatchNumber,
            Operator = _session.Operator,

            // 未全选测试项 = 只跑勾选的项（调试）；全选返回 null 表示跑全部
            StepKeys = SelectedStepKeys(),
        };
    }

    /// <summary>
    /// 参与测试的号位里，是否所有测试项都被勾选。未全选 = 调试运行，结果不落库。
    /// </summary>
    private bool AllStepsSelected =>
        Positions.Where(p => p.IsSelected).SelectMany(p => p.Steps).All(s => s.IsSelected);

    /// <summary>
    /// 勾选要测的测试项 Key（全选返回 null 表示不过滤=跑全部）。仅取参与测试号位里勾选的项，去重。
    /// </summary>
    /// <returns>勾选的测试项 Key 集合，或 null。</returns>
    private IReadOnlyCollection<string>? SelectedStepKeys()
    {
        if (AllStepsSelected)
        {
            return null;
        }
        return Positions.Where(p => p.IsSelected)
                        .SelectMany(p => p.Steps)
                        .Where(s => s.IsSelected)
                        .Select(s => s.Key)
                        .Distinct()
                        .ToList();
    }

    /// <summary>
    /// 开始任务前置校验：自动连接共享设备（标准盒），连接失败才提示去手动配置；
    /// 再校验批次号。通过返回 true。
    /// </summary>
    /// <returns>校验通过返回 true。</returns>
    private async Task<bool> ValidateBeforeStartAsync()
    {
        // 1. 自动连接共享设备（标准盒）
        try { await _conn.ConnectBoxAsync(); } catch { }

        BoxConnected = _conn.IsBoxConnected;

        if (!_conn.IsBoxConnected)
        {
            AppDialog.Error("无法开始测试",
                "共享设备（标准盒）连接失败，当前共享设备连接配置信息可能无效。\n" +
                "请打开【连接配置】手动配置正确后再连接。");
            return false;
        }

        // 2. 校验批次号
        if (string.IsNullOrWhiteSpace(BatchNumber))
        {
            AppDialog.Info("无法开始测试", "请先填写批次号（手动输入）。");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 开始/结束 合一：未运行时启动整机手动运行；运行中则停止。
    /// 每次开始自动重置计数与号位数据。
    /// 允许并发执行：手动测试运行期间需要再次点击本按钮来停止，否则按钮会被命令自身禁用。
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task StartStop()
    {
        // 运行中 → 结束
        if (IsBusy)
        {
            _runCts?.Cancel();
            return;
        }

        // 开始前校验：自动连接共享设备（标准盒）+ 批次号
        if (!await ValidateBeforeStartAsync())
        {
            return;
        }

        // 开始前自动重置数据 + 生成本次 SN
        _counters.Reset();
        foreach (var p in Positions)
        {
            p.Reset();
        }

        GenerateSerialNumbers();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        // 未全选测试项 = 调试运行，本次结果不记录数据库
        var persist = AllStepsSelected;
        if (!persist)
        {
            _notify.Notify("调试模式：未全选测试项，本次结果不记录数据库");
        }

        // 板卡专属扩展的每轮开场提醒（如 A20 人工指定了烧录板类型，防换料后忘记切回来）
        if (BoardToolbarExtra?.RunStartNotice is { } extraNotice)
        {
            _notify.Notify(extraNotice);
        }

        // 整机：手动单次运行（无 PLC 自动化，操作员人工上下料）
        IsRunning = true;
        try
        {
            // 引擎对「停止」与通讯异常均优雅返回已跑数据（不抛），故正常路径即可落盘
            var result = await Task.Run(() => _orch.RunManualAsync(_manifest, _counters, BuildOptions(), ct));
            if (persist)
            {
                await _store.SaveAsync(result);
            }
            _notify.Notify(ct.IsCancellationRequested
                ? $"{_manifest.BoardName} 测试已停止{(persist ? "（已保存已测数据）" : "（调试模式，未记录数据库）")}"
                : $"{_manifest.BoardName} 测试结束：{(result.Passed ? "全部通过 ✓" : "存在不合格 ✗")}{(persist ? "" : "（调试模式，未记录数据库）")}");
        }
        catch (OperationCanceledException) { _notify.Notify($"{_manifest.BoardName} 测试已停止"); }
        catch (Exception ex) { _notify.Notify($"{_manifest.BoardName} 异常：{ex.Message}"); }
        finally { IsRunning = false; StopLingeringSteps(); }
    }

    /// <summary>
    /// 单项单测：仅在指定号位跑该测试项。
    /// </summary>
    /// <param name="cell">目标测试项单元格。</param>
    [RelayCommand]
    private async Task SingleTest(StepCellViewModel? cell)
    {
        if (cell is null || IsBusy)
        {
            return;
        }

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        // 单测不经整跑前置校验：先确保共享标准盒已连（碰继电器/326 的单测需要）；被检由驱动惰性自连
        try { await _conn.ConnectBoxAsync(); } catch { }
        BoxConnected = _conn.IsBoxConnected;

        var options = new RunOptions { Positions = [cell.PositionIndex], StepKeys = [cell.Key] };
        try { await Task.Run(() => _runner.RunAsync(_manifest, options, ct)); }
        catch (OperationCanceledException) { }
        finally { StopLingeringSteps(); }
    }

    // ===== 板卡专属工具栏扩展位（框架侧对具体板卡零感知）=====

    /// <summary>
    /// 当前板卡的专属工具栏扩展视图模型；无匹配的板为 null（此时工具栏与原来完全一致）。
    /// 内容由 <see cref="IBoardToolbarExtraProvider"/> 的实现提供、View 由 <c>BoardExtrasTemplates.xaml</c> 的
    /// DataTemplate 按 VM 类型自动挑选——**本页不认识任何一块具体的板**，新增板卡专属操作无需改本文件。
    /// </summary>
    public IBoardToolbarExtra? BoardToolbarExtra { get; }

    // 顶部连接状态栏

    /// <summary>
    /// 标准盒是否已连接。
    /// </summary>
    [ObservableProperty] private bool _boxConnected;

    /// <summary>
    /// 整体测试后：在应用关闭前执行，委托给引擎的 <see cref="TestRunner.RunPostTestAsync"/>。
    /// </summary>
    public async Task RunPostTestAsync()
    {
        await _runner.RunPostTestAsync(_manifest);
    }

    /// <summary>
    /// 打开测试数据查看（分页查询主表 → SN 测试项 → 过程详情）。
    /// </summary>
    [RelayCommand]
    private void OpenDataQuery()
    {
        // 只查当前测试页对应型号的数据（Manifest 的 Dut.Model 与落库的 DeviceModel 同源）
        // 删除记录入口只对 admin/测试账号放开
        new DataQueryWindow(new DataQueryViewModel(_store, _manifest.Dut.Model, _session.IsTestAccount)) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 打开连接配置窗口，关闭后同步顶部连接状态栏。
    /// </summary>
    [RelayCommand]
    private void OpenConnectionConfig()
    {
        var vm = new ConnectionConfigViewModel(_connStore, _scanner, _conn, _manifest);
        new ConnectionConfigWindow(vm) { Owner = Application.Current.MainWindow }.ShowDialog();

        // 与配置窗口内的连接动作关联：关闭后同步顶部状态栏
        BoxConnected = _conn.IsBoxConnected;
    }

    /// <summary>
    /// 页面标题（设备型号 · 板名）。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 各号位进度视图集合。
    /// </summary>
    public ObservableCollection<PositionViewModel> Positions { get; } = [];

    /// <summary>
    /// 是否正在手动单次运行。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    [NotifyPropertyChangedFor(nameof(CanToggleMode))]
    private bool _isRunning;

    /// <summary>
    /// 手动单次运行状态变化：同步给板卡专属扩展（运行中禁改）。
    /// </summary>
    /// <param name="value">是否正在手动运行。</param>
    partial void OnIsRunningChanged(bool value)
    {
        SyncExtraBusy();
    }

    /// <summary>
    /// 双击测试项 → 弹出该项过程信息：运行中实时显示，已完成则从数据库加载。
    /// </summary>
    /// <param name="cell">被双击的测试项单元格。</param>
    [RelayCommand]
    private async Task ShowStepDetail(StepCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        var sn = Positions.FirstOrDefault(p => p.Index == cell.PositionIndex)?.SerialNumber;
        var vm = new StepDetailViewModel(cell, sn, _store);
        await vm.InitializeAsync();
        new StepDetailWindow(vm) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 收尾保险：把任何仍停在「测试中」（转圈）的测试项置为已停止，防止停止/异常后图标一直转。
    /// 引擎通常已为被中断项发完成事件，此处兜底覆盖未收到完成事件的边界情况。
    /// </summary>
    private void StopLingeringSteps()
    {
        foreach (var p in Positions)
        {
            p.StopRunningCells();
        }
        TaskbarProgressValue = 0;
        TaskbarProgressState = TaskbarItemProgressState.None;
    }

    /// <summary>
    /// 执行器步骤进度变化 → 投递到 UI 线程更新对应号位。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">步骤进度参数。</param>
    private void OnStepChanged(object? sender, StepProgressEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            Positions.FirstOrDefault(p => p.Index == e.PositionIndex)?.Update(e);
            UpdateTaskbarProgress();
        });
    }

    /// <summary>
    /// 执行器实时消息 → 投递到 UI 线程追加到对应号位。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">实时消息参数。</param>
    private void OnMessage(object? sender, RealtimeMessageEventArgs e)
    {
        _dispatcher.Invoke(() =>
        Positions.FirstOrDefault(p => p.Index == e.PositionIndex)?.AddMessage(e));
    }

    /// <summary>
    /// 执行器采样点 → 用非阻塞 BeginInvoke 投递，避免阻塞采集线程/拖慢测试。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">采样参数。</param>
    private void OnSample(object? sender, SampleEventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        Positions.FirstOrDefault(p => p.Index == e.PositionIndex)?.AddSample(e));
    }

    /// <summary>
    /// 遍历所有号位的所有测试项，汇总已完成步数并检查是否出现不合格/异常，
    /// 据此更新 <see cref="TaskbarProgressValue"/> 和 <see cref="TaskbarProgressState"/>。
    /// </summary>
    private void UpdateTaskbarProgress()
    {
        int total = 0, done = 0;
        int hasFail = 0;
        foreach (var pos in Positions)
        {
            foreach (var step in pos.Steps)
            {
                total++;
                switch (step.Status)
                {
                    case "通过":
                    case "跳过":
                        done++;
                        break;
                    case "不合格":
                        done++;
                        hasFail = 1;
                        break;
                    case "异常":
                        done++;
                        hasFail = 2;
                        break;
                }
            }
        }
        TaskbarProgressValue = total > 0 ? (double)done / total : 0;
        TaskbarProgressState = hasFail==2 ?TaskbarItemProgressState.Paused :(hasFail==1?  TaskbarItemProgressState.Error : TaskbarItemProgressState.Normal);
    }
}

/// <summary>
/// 单个号位的进度视图：SN、勾选、测试项单元格集合与实时更新。
/// </summary>
public partial class PositionViewModel : ObservableObject
{
    /// <summary>
    /// 按号位描述与步骤列表构建号位进度视图。
    /// </summary>
    /// <param name="pos">号位描述。</param>
    /// <param name="steps">测试项步骤列表。</param>
    public PositionViewModel(PositionDescriptor pos, IReadOnlyList<StepDescriptor> steps)
    {
        Index = pos.Index;
        Name = pos.Name;
        foreach (var s in steps.OrderBy(s => s.Order))
        {
            var cell = new StepCellViewModel(pos.Index, s.Order, s.Key, s.Name, s.Description, FormatConditions(s.Conditions));
            // 子项勾选变化 → 表头全选框三态刷新
            cell.PropertyChanged += OnStepPropertyChanged;
            Steps.Add(cell);
        }
    }

    /// <summary>
    /// 设置全选时抑制子项回调，避免逐项刷新表头。
    /// </summary>
    private bool _suppressAllChanged;

    /// <summary>
    /// 子项勾选变化时刷新表头全选框状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">属性变化参数。</param>
    private void OnStepPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_suppressAllChanged && e.PropertyName == nameof(StepCellViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(AllStepsChecked));
        }
    }

    /// <summary>
    /// 表头全选框（三态）：全选=true、全不选=false、部分选中=null。设 true/false 时批量勾选/取消所有测试项。
    /// </summary>
    public bool? AllStepsChecked
    {
        get
        {
            if (Steps.Count > 0 && Steps.All(s => s.IsSelected))
            {
                return true;
            }
            if (Steps.All(s => !s.IsSelected))
            {
                return false;
            }
            return null;
        }
        set
        {
            // 三态复选框点击循环 全选(true)→null→全不选(false)：从全选点击会传 null，视为全不选，避免卡住无法切换
            var target = value ?? false;
            _suppressAllChanged = true;
            foreach (var s in Steps)
            {
                s.IsSelected = target;
            }
            _suppressAllChanged = false;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 把判定条件格式化成操作员可读的多行文本（Range→区间，Text/Value→期望值）。
    /// </summary>
    /// <param name="conditions">判定条件集合。</param>
    /// <returns>多行文本（无条件返回空串）。</returns>
    private static string FormatConditions(IReadOnlyList<ConditionDescriptor> conditions)
    {
        if (conditions.Count == 0)
        {
            return "";
        }

        var lines = conditions.Select(c =>
        {
            var unit = string.IsNullOrEmpty(c.Unit) ? "" : c.Unit;
            var body = c.Kind.Equals("Range", StringComparison.OrdinalIgnoreCase)
                ? $"{c.Min}~{c.Max}{unit}"
                : $"= {c.Expected}{unit}";
            return string.IsNullOrEmpty(c.Name) ? body : $"{c.Name}：{body}";
        });
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 号位索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 号位名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 是否纳入测试（全自动/运行 仅跑勾选号位）。
    /// </summary>
    [ObservableProperty] private bool _isSelected = true;

    /// <summary>
    /// 本号位 SN（每次运行自动生成 yyyyMMddHHmmss）。
    /// </summary>
    [ObservableProperty] private string _serialNumber = "";

    /// <summary>
    /// 是否显示"过程日志"列（单工位时为 true，用于填充右侧空白）。
    /// </summary>
    public bool ShowLog { get; set; }

    /// <summary>
    /// 本号位的测试项单元格集合。
    /// </summary>
    public ObservableCollection<StepCellViewModel> Steps { get; } = [];

    /// <summary>
    /// 清空 SN 与各测试项状态。
    /// </summary>
    public void Reset()
    {
        SerialNumber = "";
        foreach (var s in Steps)
        {
            s.Reset();
        }
    }

    /// <summary>
    /// 依步骤进度更新对应测试项（运行中/结果）。
    /// </summary>
    /// <param name="e">步骤进度参数。</param>
    public void Update(StepProgressEventArgs e)
    {
        var cell = Steps.FirstOrDefault(c => string.Equals(c.Key, e.Step.Key, StringComparison.OrdinalIgnoreCase));
        if (cell is null)
        {
            return;
        }

        if (e.Phase == StepPhase.Running)
        {
            cell.SetRunning();
        }
        else if (e.Result is not null)
        {
            cell.SetResult(e.Result);
        }
    }

    /// <summary>
    /// 把本号位仍在「测试中」的测试项置为已停止（收尾兜底，解除转圈）。
    /// </summary>
    public void StopRunningCells()
    {
        foreach (var s in Steps)
        {
            s.StopIfRunning();
        }
    }

    /// <summary>
    /// 实时消息只进对应测试项明细（双击查看）；无归属项的工位级消息忽略。
    /// </summary>
    /// <param name="e">实时消息参数。</param>
    public void AddMessage(RealtimeMessageEventArgs e)
    {
        if (string.IsNullOrEmpty(e.StepKey))
        {
            return;
        }

        var line = new LogLineViewModel(e.At, e.Message, e.Level);
        Steps.FirstOrDefault(c => string.Equals(c.Key, e.StepKey, StringComparison.OrdinalIgnoreCase))?.AddDetail(line);
    }

    /// <summary>
    /// 实时采集点进对应测试项的曲线。
    /// </summary>
    /// <param name="e">采样参数。</param>
    public void AddSample(SampleEventArgs e)
    {
        Steps.FirstOrDefault(c => string.Equals(c.Key, e.StepKey, StringComparison.OrdinalIgnoreCase))?.AddSample(e);
    }
}

/// <summary>
/// 单个测试项单元格：状态/测量值/着色 + 过程日志明细 + 实时曲线。
/// </summary>
public partial class StepCellViewModel(int positionIndex, int order, string key, string name, string description = "", string conditionsText = "") : ObservableObject
{
    /// <summary>
    /// 所属号位索引。
    /// </summary>
    public int PositionIndex { get; } = positionIndex;

    /// <summary>
    /// 测试项顺序。
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// 测试项 Key。
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// 测试项名称。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 测试项描述（详情弹窗展示，操作员可读）。
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// 条件指标（多行文本，详情弹窗展示）。
    /// </summary>
    public string ConditionsText { get; } = conditionsText;

    /// <summary>
    /// 是否勾选纳入测试（默认选中）。调试时可只选部分测试项；未全选则本次为调试运行，结果不落库。
    /// </summary>
    [ObservableProperty] private bool _isSelected = true;

    /// <summary>
    /// 错误信息（不通过/异常时的摘要或明细；通过时为空）。
    /// </summary>
    [ObservableProperty] private string _errorMessage = "";

    /// <summary>
    /// 测试项状态文字（待测/测试中/通过/不合格/跳过/异常）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    private string _status = "待测";

    /// <summary>
    /// 测量值文字。
    /// </summary>
    [ObservableProperty] private string? _measured;

    /// <summary>
    /// 状态着色。
    /// </summary>
    [ObservableProperty] private Brush _statusBrush = Brushes.Gray;

    /// <summary>
    /// 该测试项的完整测试过程信息（双击弹窗展示）。
    /// </summary>
    public ObservableCollection<LogLineViewModel> Details { get; } = [];

    /// <summary>
    /// 该测试项的实时采集数据曲线（随采样点增长；无采集时为 null）。
    /// </summary>
    [ObservableProperty] private ProcessDataSeries? _processData;

    /// <summary>
    /// 是否已完成（用于决定弹窗是实时还是从库加载）。
    /// </summary>
    public bool IsFinished => Status is not ("待测" or "测试中");

    // 实时采集累积

    /// <summary>
    /// 采样时间轴累积。
    /// </summary>
    private readonly List<double> _sampleTime = [];

    /// <summary>
    /// 各通道采样值累积。
    /// </summary>
    private List<List<double>> _sampleChannels = [];

    /// <summary>
    /// 采样通道名。
    /// </summary>
    private string[] _channelNames = [];

    /// <summary>
    /// 采样单位。
    /// </summary>
    private string _sampleUnit = "V";

    /// <summary>
    /// 最新一条过程日志（单工位「过程日志」列显示）。
    /// </summary>
    public string LatestLog => Details.Count > 0 ? Details[^1].Message : "";

    /// <summary>
    /// 追加一条过程日志并通知最新日志刷新。
    /// </summary>
    /// <param name="line">日志行。</param>
    public void AddDetail(LogLineViewModel line) { Details.Add(line); OnPropertyChanged(nameof(LatestLog)); }

    /// <summary>
    /// 追加一个采样点并重建曲线数据序列。
    /// </summary>
    /// <param name="e">采样参数。</param>
    public void AddSample(SampleEventArgs e)
    {
        if (_sampleTime.Count == 0)
        {
            _channelNames = e.ChannelNames.ToArray();
            _sampleUnit = e.Unit;
            _sampleChannels = _channelNames.Select(_ => new List<double>()).ToList();
        }
        _sampleTime.Add(e.TimeSec);
        for (var i = 0; i < _sampleChannels.Count && i < e.Values.Count; i++)
        {
            _sampleChannels[i].Add(e.Values[i]);
        }

        ProcessData = new ProcessDataSeries
        {
            Unit = _sampleUnit,
            TimeSec = _sampleTime.ToList(),
            Channels = _channelNames.Select((n, i) => new ProcessChannel(n, _sampleChannels[i].ToList())).ToList(),
        };
    }

    /// <summary>
    /// 复位单元格状态、测量值、着色、明细与采集累积。
    /// </summary>
    public void Reset()
    {
        Status = "待测"; Measured = null; StatusBrush = Brushes.Gray; Details.Clear(); OnPropertyChanged(nameof(LatestLog));
        ProcessData = null; ErrorMessage = "";
        _sampleTime.Clear(); _sampleChannels = []; _channelNames = [];
    }

    /// <summary>
    /// 置为「测试中」状态与着色，清空上次错误信息。
    /// </summary>
    public void SetRunning() { Status = "测试中"; StatusBrush = Brushes.DarkOrange; ErrorMessage = ""; }

    /// <summary>
    /// 若仍处于「测试中」（转圈），置为「已停止」以解除转圈（停止/异常收尾兜底）。
    /// </summary>
    public void StopIfRunning()
    {
        if (Status == "测试中")
        {
            Status = "已停止";
            StatusBrush = Brushes.Gray;
        }
    }

    /// <summary>
    /// 依步骤结果置测量值、状态文字与着色；不通过/异常时记录错误信息。
    /// </summary>
    /// <param name="r">步骤结果。</param>
    public void SetResult(StepResult r)
    {
        Measured = r.MeasuredValue;
        (Status, StatusBrush) = r.Outcome switch
        {
            StepOutcome.Pass => ("通过", Brushes.SeaGreen),
            StepOutcome.Fail => ("不合格", Brushes.Firebrick),
            StepOutcome.Skip => ("跳过", Brushes.Gray),
            _ => ("异常", Brushes.Firebrick),
        };
        // 通过时无错误信息；否则取明细（异常堆栈）优先，回退结果摘要
        ErrorMessage = r.Outcome == StepOutcome.Pass ? "" : (r.Detail ?? r.Summary ?? "");
    }
}

/// <summary>
/// 一行过程日志（时间 + 文本 + 按级别着色）。
/// </summary>
public sealed class LogLineViewModel
{
    /// <summary>
    /// 时间文字。
    /// </summary>
    public string Time { get; }

    /// <summary>
    /// 日志文本。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 按级别着色。
    /// </summary>
    public Brush Brush { get; }

    /// <summary>
    /// 由时间戳/文本/级别构造一行日志（实时场景）。
    /// </summary>
    /// <param name="at">时间戳。</param>
    /// <param name="message">日志文本。</param>
    /// <param name="level">日志级别。</param>
    public LogLineViewModel(DateTime at, string message, RealtimeLevel level)
    {
        Time = at.ToString("HH:mm:ss.fff");
        Message = message;
        Brush = level switch
        {
            RealtimeLevel.Success => Brushes.SeaGreen,
            RealtimeLevel.Warn => Brushes.DarkGoldenrod,
            RealtimeLevel.Error => Brushes.Firebrick,
            _ => Brushes.Black,
        };
    }

    /// <summary>
    /// 从数据库过程日志（已含时间前缀）还原一行。
    /// </summary>
    /// <param name="time">时间文字。</param>
    /// <param name="message">日志文本。</param>
    public LogLineViewModel(string time, string message)
    {
        Time = time;
        Message = message;
        Brush = Brushes.Black;
    }
}
