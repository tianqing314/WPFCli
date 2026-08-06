using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TESTRIG.Core.Abstractions;
using TESTRIG.Infrastructure.Notifications;
using TESTRIG.Jigs;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 主壳：左侧"设备→板子"两级菜单（搜索/折叠/拖动排序，顺序本地持久化）+ 右侧内容区 + 底部状态栏。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// DI 服务提供者，用于强类型构造板子运行 VM。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 针床清单目录。
    /// </summary>
    private readonly JigCatalog _catalog;

    /// <summary>
    /// 菜单顺序持久化文件路径。
    /// </summary>
    private static readonly string OrderFile = Path.Combine(AppContext.BaseDirectory, "Config", "menu_order.json");

    /// <summary>
    /// 最近使用板子持久化文件路径（仅存板 Key，跨会话保留；显示时按 Key 从当前目录树解析出完整板项）。
    /// </summary>
    private static readonly string RecentFile = Path.Combine(AppContext.BaseDirectory, "Config", "recent_boards.json");

    /// <summary>
    /// 最近使用板子的 Key（最新在前，最多 3 个）。<see cref="RecentBoards"/> 由它按当前目录树解析得到，
    /// 保证卡片始终引用现存的完整板项（避免维护重载后残留旧/空实例）。
    /// </summary>
    private List<string> _recentKeys = [];

    /// <summary>
    /// JSON 序列化选项（缩进输出）。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// 加载顺序期间抑制保存，避免回填触发的集合变化又写盘。
    /// </summary>
    private bool _suppressSave;

    /// <summary>
    /// 在线升级服务。
    /// </summary>
    private readonly Services.UpdateService _update;

    /// <summary>
    /// 构造主壳：按目录构建设备/板子两级菜单、应用本地顺序、挂接过滤视图与顺序持久化订阅，
    /// 并启动升级自动检查循环。
    /// </summary>
    /// <param name="services">DI 服务提供者。</param>
    /// <param name="catalog">针床清单目录。</param>
    /// <param name="notifications">通知服务（推送状态栏文字）。</param>
    /// <param name="update">在线升级服务。</param>
    public MainViewModel(IServiceProvider services, JigCatalog catalog, INotificationService notifications, Services.UpdateService update)
    {
        _services = services;
        _catalog = catalog;
        _update = update;

        var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        AppVersion = $"v{ver}";
        var dispatcher = Application.Current.Dispatcher;
        notifications.Notified += m => dispatcher.Invoke(() => StatusText = m);

        foreach (var group in catalog.ByDevice())
        {
            var device = new DeviceGroupViewModel { Device = group.Key };
            foreach (var jig in group)
            {
                device.Boards.Add(ToBoardItem(jig));
            }

            Devices.Add(device);
        }

        // 应用本地保存的设备/板子顺序
        LoadOrder();

        DevicesView = CollectionViewSource.GetDefaultView(Devices);
        DevicesView.Filter = o => o is DeviceGroupViewModel g && g.IsVisible;

        // 订阅：展开状态 → 刷新 toggle 图标；集合顺序变化 → 持久化
        foreach (var d in Devices)
        {
            Hook(d);
        }

        Devices.CollectionChanged += (_, _) => { RehookAll(); SaveOrder(); };
        RecomputeAllExpanded();

        // 载入最近使用（跨会话），按 Key 从目录树解析成完整板卡卡片
        LoadRecent();
        RebuildRecent();

        // 升级自动检查（30s 后首查 + 周期复查）；CI 冒烟运行不参与
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TESTRIG_AUTORUN_TASK")))
        {
            _ = AutoUpdateLoopAsync();
        }
    }

    // ===== 在线升级 =====

    /// <summary>
    /// 有可用新版本（头像触发器与菜单项红点）。
    /// </summary>
    [ObservableProperty] private bool _updateBadge;

    /// <summary>
    /// 是否存在可回滚的上一版本（决定菜单项可见性，启动时定型）。
    /// </summary>
    public bool RollbackAvailable => _update.RollbackAvailable;

    /// <summary>
    /// 自动检查循环：启动 30s 后首查，此后按配置间隔复查；
    /// 有更新亮红点 + Toast，强更且空闲时直接弹更新对话框。
    /// </summary>
    private async Task AutoUpdateLoopAsync()
    {
        if (!_update.Config.AutoCheck)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(30));
        while (true)
        {
            if (await _update.CheckAsync() && _update.HasUpdate)
            {
                UpdateBadge = true;

                // 整机测试为有人值守场景：弹窗可被操作员看到并处理，直接按强制/非强制分流。
                if (_update.IsMandatory && !AnyBoardBusy)
                {
                    OpenUpdateDialog();
                }
                else
                {
                    Services.AppToast.Success($"发现新版本 v{_update.Latest!.Version}", "点右上角头像菜单 →「检查更新」安装");
                }
            }

            await Task.Delay(TimeSpan.FromHours(Math.Max(1, _update.Config.CheckIntervalHours)));
        }
    }

    /// <summary>
    /// 手动检查更新（头像菜单）：无更新/失败用 Toast 反馈，有更新直接弹对话框。
    /// </summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (!await _update.CheckAsync())
        {
            Services.AppToast.Error("检查更新失败", $"无法连接升级服务器（{_update.Config.ServerBase}）");
            return;
        }

        if (!_update.HasUpdate)
        {
            UpdateBadge = false;
            Services.AppToast.Success("已是最新版本", $"当前 v{_update.CurrentVersion.ToString(3)}");
            return;
        }

        UpdateBadge = true;
        OpenUpdateDialog();
    }

    /// <summary>
    /// 回滚到上一版本（产线可用）：测试中拦截 → 危险确认 → 拉起升级器互换目录并重启。
    /// </summary>
    [RelayCommand]
    private void RollbackVersion()
    {
        if (AnyBoardBusy)
        {
            Services.AppDialog.Error("无法回滚", "有板卡正在测试中，请先停止测试。");
            return;
        }

        if (!_update.RollbackAvailable)
        {
            Services.AppDialog.Info("无法回滚", "没有可回滚的上一版本。");
            return;
        }

        if (!Services.AppDialog.ConfirmDanger(
                "回滚版本",
                $"确认回滚到上一版本吗？\n当前 v{_update.CurrentVersion.ToString(3)} 将被换下（之后可再前滚）。\n配置与测试数据保持不变。",
                "回 滚"))
        {
            return;
        }

        try
        {
            _update.RollbackAndRestart();
        }
        catch (Exception ex)
        {
            Services.AppDialog.Error("回滚失败", ex.Message);
        }
    }

    /// <summary>
    /// 打开更新对话框（模态；强更时不可关闭）。
    /// </summary>
    private void OpenUpdateDialog()
    {
        var vm = new UpdateViewModel(_update, () => AnyBoardBusy);
        new Views.UpdateWindow(vm) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    /// <summary>
    /// 当前登录用户名（供状态栏显示）。
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// 登录用户名首字母（大写），供标题栏头像方块显示；用户名为空时回退为「?」。
    /// </summary>
    public string UserInitial => string.IsNullOrWhiteSpace(UserName) ? "?" : UserName.Trim()[..1].ToUpperInvariant();

    /// <summary>
    /// 标题栏头像触发器最小宽度：按用户名估宽自适应（WindowChrome 标题区存在测量缺陷，
    /// 自动测量恒得头像宽，故按 中文≈14px/西文≈8px 估算兜底）。
    /// </summary>
    public double TriggerMinWidth
    {
        get
        {
            double w = 0;
            foreach (var ch in UserName)
            {
                w += ch > 0x2E80 ? 14 : 8;
            }

            // 头像 26 + 间距 15 + 箭头 14 + 内边距 16 + 边框 2
            return 73 + w;
        }
    }

    /// <summary>
    /// 当前是否为 admin/测试账号（决定手册预览入口是否可见）。由 App 在登录后设置。
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 注销请求（由标题栏合并菜单触发，App 层负责隐藏主窗、重新走登录流程）。
    /// </summary>
    public event Action? LogoutRequested;

    /// <summary>
    /// 是否有板卡正在测试中（测试中禁止注销）。
    /// </summary>
    public bool AnyBoardBusy => _openBoards.Values.Any(b => b.IsBusy);

    /// <summary>
    /// 发起注销（视图层确认后调用）。
    /// </summary>
    public void RequestLogout() => LogoutRequested?.Invoke();

    /// <summary>
    /// 重新登录后更新当前用户并刷新标题栏头像/管理员入口的绑定。
    /// </summary>
    /// <param name="userName">新用户名。</param>
    /// <param name="isAdmin">是否 admin/测试账号。</param>
    public void UpdateUser(string userName, bool isAdmin)
    {
        UserName = userName;
        IsAdmin = isAdmin;
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserInitial));
        OnPropertyChanged(nameof(TriggerMinWidth));
        OnPropertyChanged(nameof(IsAdmin));

        // 注销重登 = 新会话：回设备选择入口页（联动展开 docked 抽屉、隐藏板名 chip）。
        // 已打开板的页面缓存保留，再次选同一板可复用状态。
        CurrentContent = null;
        CurrentBoardFamily = "";
        CurrentBoardName = "";
        IsDeviceDrawerOpen = false;
    }

    /// <summary>
    /// 打开「系统介绍」预览窗（内容取自仓库根 README.md，仅 admin 入口可见）。
    /// </summary>
    [RelayCommand]
    private void OpenManual()
    {
        var vm = new ManualViewModel("系统介绍", "Docs/README.md");
        new Views.ManualWindow(vm) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 打开「TESTRIG / 测试项 维护」窗口（仅 admin 入口可见）。关闭后若清单有增删改则重建左侧菜单。
    /// </summary>
    [RelayCommand]
    private void OpenManifestMaintenance()
    {
        var vm = ActivatorUtilities.CreateInstance<ManifestMaintenanceViewModel>(_services);
        new Views.ManifestMaintenanceWindow(vm) { Owner = Application.Current.MainWindow }.ShowDialog();
        if (vm.CatalogChanged)
        {
            RebuildMenu();
        }
    }

    /// <summary>
    /// 打开「证书 / 合格证」窗口（出厂检验模板专属：生成/预览/打印合格证）。
    /// </summary>
    [RelayCommand]
    private void OpenCertificate()
    {
        var store = _services.GetRequiredService<TESTRIG.Infrastructure.Data.ITestResultStore>();
        new Views.CertificateWindow(new CertificateViewModel(store, "")) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>
    /// 维护后重建左侧「设备→板子」菜单（目录已在保存/删除时重载）：重建分组、复用本地顺序、
    /// 清空已打开板子缓存（清单可能已改/删，避免复用旧清单），若当前显示的是板子页则清空回首页。
    /// </summary>
    private void RebuildMenu()
    {
        _suppressSave = true;
        Devices.Clear();
        foreach (var group in _catalog.ByDevice())
        {
            var device = new DeviceGroupViewModel { Device = group.Key };
            foreach (var jig in group)
            {
                device.Boards.Add(ToBoardItem(jig));
            }

            Devices.Add(device);
        }

        _suppressSave = false;
        LoadOrder();
        if (Devices.Count > 0 && !Devices.Any(d => d.IsExpanded))
        {
            Devices[0].IsExpanded = true;
        }

        RecomputeAllExpanded();
        DevicesView.Refresh();

        // 目录重载后按 Key 重解析最近使用，卡片改指向新板项，避免残留旧/空实例
        RebuildRecent();

        _openBoards.Clear();
        if (CurrentContent is TestRunViewModel)
        {
            CurrentContent = null;
        }
    }

    /// <summary>
    /// 清单 → 菜单板项：填 Key/板名/设备族/描述，并按清单算卡片 meta 行
    /// （通讯方式 · 号位数 · 测试项数，对应设计稿 05 最近使用卡片 rc-meta）。
    /// </summary>
    /// <param name="jig">针床清单。</param>
    /// <returns>菜单板项。</returns>
    private static BoardItemViewModel ToBoardItem(JigManifest jig)
    {
        var link = jig.Positions.FirstOrDefault()?.Comm?.Link ?? jig.Dut.Comm?.Link;
        var linkText = link switch
        {
            LinkType.Serial => "串口",
            LinkType.Usb => "USB",
            LinkType.Ethernet => "网络",
            _ => null,
        };
        var meta = $"{jig.Positions.Count} 号位 · {jig.Steps.Count} 测试项";
        return new BoardItemViewModel
        {
            Key = jig.Key,
            BoardName = jig.BoardName,
            Device = jig.DeviceFamily,
            Description = jig.Description,
            Meta = linkText is null ? meta : $"{linkText} · {meta}",
        };
    }

    /// <summary>
    /// 应用版本号（形如 v1.0.0）。
    /// </summary>
    public string AppVersion { get; }

    /// <summary>
    /// 设备分组集合（左侧两级菜单顶层）。
    /// </summary>
    public ObservableCollection<DeviceGroupViewModel> Devices { get; } = [];

    /// <summary>
    /// 设备菜单的过滤视图（按搜索文本隐藏不匹配的设备组）。
    /// </summary>
    public ICollectionView DevicesView { get; }

    /// <summary>
    /// 搜索文本：按设备型号 / TESTRIG 名称模糊筛选。
    /// </summary>
    [ObservableProperty] private string _searchText = "";

    /// <summary>
    /// 搜索文本变化时对各设备组应用过滤、自动展开命中组并刷新视图。
    /// </summary>
    /// <param name="value">新的搜索文本。</param>
    partial void OnSearchTextChanged(string value)
    {
        foreach (var d in Devices)
        {
            d.ApplyFilter(value);

            // 搜索命中的组自动展开
            if (!string.IsNullOrWhiteSpace(value) && d.IsVisible) d.IsExpanded = true;
        }
        DevicesView.Refresh();
        RecomputeAllExpanded();
    }

    /// <summary>
    /// 当前（可见组）是否全部展开——驱动 toggle 按钮图标。
    /// </summary>
    [ObservableProperty] private bool _allExpanded;

    /// <summary>
    /// 一个按钮切换：全部展开 ↔ 全部折叠。
    /// </summary>
    [RelayCommand]
    private void ToggleExpandAll()
    {
        var expand = !AllExpanded;
        foreach (var d in Devices)
        {
            if (d.IsVisible)
            {
                d.IsExpanded = expand;
            }
        }

        RecomputeAllExpanded();
    }

    /// <summary>
    /// 依可见组的展开状态重算「全部展开」标志。
    /// </summary>
    private void RecomputeAllExpanded()
    {
        var vis = Devices.Where(d => d.IsVisible).ToList();
        AllExpanded = vis.Count > 0 && vis.All(d => d.IsExpanded);
    }

    // ===== 顺序持久化 =====

    /// <summary>
    /// 订阅单个设备组的展开状态与板子集合变化。
    /// </summary>
    /// <param name="d">设备组视图模型。</param>
    private void Hook(DeviceGroupViewModel d)
    {
        d.PropertyChanged += OnGroupPropertyChanged;
        d.Boards.CollectionChanged += OnBoardsChanged;
    }

    /// <summary>
    /// 设备集合重排后，解绑再重挂所有设备组的订阅。
    /// </summary>
    private void RehookAll()
    {
        foreach (var d in Devices)
        {
            d.PropertyChanged -= OnGroupPropertyChanged;
            d.Boards.CollectionChanged -= OnBoardsChanged;
            Hook(d);
        }
    }

    /// <summary>
    /// 设备组展开状态变化时重算「全部展开」标志。
    /// </summary>
    /// <param name="s">事件源。</param>
    /// <param name="e">属性变化参数。</param>
    private void OnGroupPropertyChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceGroupViewModel.IsExpanded))
        {
            RecomputeAllExpanded();
        }
    }

    /// <summary>
    /// 板子集合变化时持久化顺序。
    /// </summary>
    /// <param name="s">事件源。</param>
    /// <param name="e">集合变化参数。</param>
    private void OnBoardsChanged(object? s, NotifyCollectionChangedEventArgs e)
    {
        SaveOrder();
    }

    /// <summary>
    /// 保存当前设备与板子顺序到本地文件（失败不影响使用）。
    /// </summary>
    private void SaveOrder()
    {
        if (_suppressSave)
        {
            return;
        }

        try
        {
            var data = new MenuOrder
            {
                Devices = Devices.Select(d => d.Device).ToList(),
                Boards = Devices.ToDictionary(d => d.Device, d => d.Boards.Select(b => b.Key).ToList()),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(OrderFile)!);
            File.WriteAllText(OrderFile, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch { /* 持久化失败不影响使用 */ }
    }

    /// <summary>
    /// 从本地文件加载并应用设备与板子顺序（文件缺失或损坏则用默认顺序）。
    /// </summary>
    private void LoadOrder()
    {
        try
        {
            if (!File.Exists(OrderFile))
            {
                return;
            }

            var data = JsonSerializer.Deserialize<MenuOrder>(File.ReadAllText(OrderFile));
            if (data is null)
            {
                return;
            }

            _suppressSave = true;

            if (data.Devices is { Count: > 0 })
            {
                var ordered = Devices.OrderBy(d => IndexOr(data.Devices, d.Device)).ToList();
                Devices.Clear();
                foreach (var d in ordered)
                {
                    Devices.Add(d);
                }
            }
            foreach (var d in Devices)
            {
                if (data.Boards is null || !data.Boards.TryGetValue(d.Device, out var order) || order.Count == 0)
                {
                    continue;
                }

                var ob = d.Boards.OrderBy(b => IndexOr(order, b.Key)).ToList();
                d.Boards.Clear();
                foreach (var b in ob)
                {
                    d.Boards.Add(b);
                }
            }
        }
        catch { /* 顺序文件损坏则用默认顺序 */ }
        finally { _suppressSave = false; }
    }

    /// <summary>
    /// 返回 key 在列表中的下标，不存在则返回 int.MaxValue（排到末尾）。
    /// </summary>
    /// <param name="list">顺序列表。</param>
    /// <param name="key">查找的键。</param>
    /// <returns>下标或 int.MaxValue。</returns>
    private static int IndexOr(List<string> list, string key)
    {
        var i = list.IndexOf(key);
        return i < 0 ? int.MaxValue : i;
    }

    /// <summary>
    /// 右侧内容区当前显示的内容（板子运行 VM 或提示文字）。
    /// </summary>
    [ObservableProperty] private object? _currentContent;

    /// <summary>
    /// 内容变化时联动派生状态：是否已选板 → 左侧 docked 抽屉显隐 / 入口空画布。
    /// </summary>
    /// <param name="value">新内容。</param>
    partial void OnCurrentContentChanged(object? value)
    {
        OnPropertyChanged(nameof(HasBoard));
        OnPropertyChanged(nameof(ShowDockedDrawer));
        OnPropertyChanged(nameof(IsEntryVisible));

        // 非运行态页面时隐藏任务栏进度
        if (value is not TestRunViewModel vm || !vm.IsBusy)
        {
            TaskbarProgressValue = 0;
            TaskbarProgressState = TaskbarItemProgressState.None;
        }
    }

    /// <summary>
    /// 是否已选中并打开某块板（决定 docked 抽屉收起、顶部 ☰ 板名 chip 显示）。
    /// </summary>
    public bool HasBoard => CurrentContent is TestRunViewModel;

    /// <summary>
    /// 是否显示左侧 docked 抽屉（未选板的入口态展开；选板后收起为 0）。
    /// </summary>
    public bool ShowDockedDrawer => !HasBoard;

    /// <summary>
    /// 是否显示入口空画布（未选任何内容时）。入口画布放正常可视树用 Visibility 切换，
    /// 不用 Content=null 的 ContentTemplate 承载——那会把 null DataContext 压进模板树，模板内绑定全部失效。
    /// </summary>
    public bool IsEntryVisible => CurrentContent is null;

    /// <summary>
    /// 底部状态栏文字。
    /// </summary>
    [ObservableProperty] private string _statusText = "就绪";

    /// <summary>
    /// 任务栏进度值（0.0~1.0），从当前测试页面镜像。
    /// </summary>
    [ObservableProperty] private double _taskbarProgressValue;

    /// <summary>
    /// 任务栏进度状态：Normal 绿 / Error 红 / None 隐藏。
    /// </summary>
    [ObservableProperty] private TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;

    /// <summary>
    /// 浮层设备抽屉是否打开（选板后经顶部 ☰ chip 二次唤出，带遮罩，不挤压布局）。
    /// </summary>
    [ObservableProperty] private bool _isDeviceDrawerOpen;

    /// <summary>
    /// 当前板名称（顶部 ☰ chip 显示）。
    /// </summary>
    [ObservableProperty] private string _currentBoardName = "";

    /// <summary>
    /// 当前板所属设备家族（顶部 ☰ chip 等宽显示）。
    /// </summary>
    [ObservableProperty] private string _currentBoardFamily = "";

    /// <summary>
    /// 最近使用的板子（入口空画布快捷卡，去重、最多 3 个，最新在前）。
    /// </summary>
    public ObservableCollection<BoardItemViewModel> RecentBoards { get; } = [];

    /// <summary>
    /// 是否有最近使用记录（无则入口不显示「最近使用」区块）。
    /// </summary>
    public bool HasRecent => RecentBoards.Count > 0;

    /// <summary>
    /// 唤出浮层设备抽屉（二次切换板子）。
    /// </summary>
    [RelayCommand]
    private void ShowDeviceDrawer() => IsDeviceDrawerOpen = true;

    /// <summary>
    /// 关闭浮层设备抽屉。
    /// </summary>
    [RelayCommand]
    private void CloseDeviceDrawer() => IsDeviceDrawerOpen = false;

    /// <summary>
    /// 记录最近使用的板子：Key 去重后置顶、最多 3 个，持久化并按当前目录树重解析卡片。
    /// </summary>
    /// <param name="item">被打开的板子。</param>
    private void PushRecent(BoardItemViewModel item)
    {
        _recentKeys.RemoveAll(k => string.Equals(k, item.Key, StringComparison.Ordinal));
        _recentKeys.Insert(0, item.Key);
        if (_recentKeys.Count > 3)
        {
            _recentKeys.RemoveRange(3, _recentKeys.Count - 3);
        }

        SaveRecent();
        RebuildRecent();
    }

    /// <summary>
    /// 按 <see cref="_recentKeys"/> 从当前目录树解析出完整板项，重建 <see cref="RecentBoards"/>——
    /// 卡片始终引用现存板项（跨会话载入、维护重载后均刷新），Key 已不存在的记录跳过。
    /// </summary>
    private void RebuildRecent()
    {
        RecentBoards.Clear();
        foreach (var key in _recentKeys)
        {
            var board = Devices.SelectMany(d => d.Boards).FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.Ordinal));
            if (board is not null)
            {
                RecentBoards.Add(board);
            }
        }

        OnPropertyChanged(nameof(HasRecent));
    }

    /// <summary>
    /// 保存最近使用板 Key 列表（失败不影响使用）。
    /// </summary>
    private void SaveRecent()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecentFile)!);
            File.WriteAllText(RecentFile, JsonSerializer.Serialize(_recentKeys, JsonOpts));
        }
        catch { /* 持久化失败不影响使用 */ }
    }

    /// <summary>
    /// 载入最近使用板 Key 列表（文件缺失或损坏则为空）。
    /// </summary>
    private void LoadRecent()
    {
        try
        {
            if (!File.Exists(RecentFile))
            {
                return;
            }

            _recentKeys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFile)) ?? [];
        }
        catch { _recentKeys = []; }
    }

    /// <summary>
    /// 已打开的板子页面缓存（按板 Key）：切走再切回复用同一 VM，保持计数/运行/开关等状态不重置。
    /// </summary>
    private readonly Dictionary<string, TestRunViewModel> _openBoards = [];

    /// <summary>
    /// 正在打开板子（覆盖式加载缓冲可见）。
    /// </summary>
    [ObservableProperty] private bool _isBoardLoading;

    /// <summary>
    /// 加载缓冲上显示的板子名。
    /// </summary>
    [ObservableProperty] private string _boardLoadingText = "";

    /// <summary>
    /// 打开板子：先亮覆盖式加载缓冲（对应设计稿 10），构造/复用运行 VM 并等首轮布局渲染完成后再收起，
    /// 避免选板到主测试页之间出现无反馈的卡顿。
    /// </summary>
    /// <param name="item">被打开的板子菜单项。</param>
    [RelayCommand]
    private async Task OpenBoardAsync(BoardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        BoardLoadingText = $"{item.Device} · {item.BoardName}";

        // 延迟亮缓冲层：150ms 内完成（缓存/已预热）就完全不闪缓冲，超时才显示转圈
        using var fastPath = new System.Threading.CancellationTokenSource();
        _ = ShowLoadingIfSlowAsync(fastPath.Token);
        try
        {
            if (!_openBoards.TryGetValue(item.Key, out var vm))
            {
                var manifest = _catalog.Find(item.Key);
                if (manifest is null)
                {
                    CurrentContent = $"未找到针床清单：{item.Key}";
                    return;
                }

                // 强类型构造，无字符串反射；放后台线程执行，UI 线程只剩视图装载，
                // 缓冲层转圈不被构造成本卡住（VM 构造链无 UI 亲和，工位画刷已冻结）
                vm = await Task.Run(() => ActivatorUtilities.CreateInstance<TestRunViewModel>(_services, manifest));

                // 运行状态变化 → 对应菜单项闪烁提示
                var board = item;
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TestRunViewModel.IsBusy))
                    {
                        board.IsRunning = vm.IsBusy;
                        // 运行结束 → 清除任务栏进度
                        if (!vm.IsBusy)
                        {
                            TaskbarProgressValue = 0;
                            TaskbarProgressState = TaskbarItemProgressState.None;
                        }
                    }
                    else if (e.PropertyName == nameof(TestRunViewModel.TaskbarProgressValue))
                    {
                        TaskbarProgressValue = vm.TaskbarProgressValue;
                    }
                    else if (e.PropertyName == nameof(TestRunViewModel.TaskbarProgressState))
                    {
                        TaskbarProgressState = vm.TaskbarProgressState;
                    }
                };
                _openBoards[item.Key] = vm;
            }
            CurrentContent = vm;

            // 顶部 ☰ chip 显示 + 最近使用；打开板即收起 docked 抽屉、关闭浮层抽屉
            CurrentBoardFamily = item.Device;
            CurrentBoardName = item.BoardName;
            PushRecent(item);
            IsDeviceDrawerOpen = false;

            // 等测试页完成首轮布局/渲染（ContextIdle）再收覆盖层
            await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
        finally
        {
            fastPath.Cancel();
            IsBoardLoading = false;
        }
    }

    /// <summary>
    /// 打开板子超过 150ms 仍未完成时才亮出覆盖式缓冲层（快路径完全不闪）。
    /// </summary>
    /// <param name="token">快路径完成即取消。</param>
    private async Task ShowLoadingIfSlowAsync(System.Threading.CancellationToken token)
    {
        try
        {
            await Task.Delay(150, token);
            IsBoardLoading = true;
        }
        catch (TaskCanceledException)
        {
            // 快路径已完成，无需缓冲层
        }
    }
}

/// <summary>
/// 设备菜单顺序的持久化数据。
/// </summary>
internal sealed class MenuOrder
{
    /// <summary>
    /// 设备顺序（型号列表）。
    /// </summary>
    public List<string> Devices { get; set; } = [];

    /// <summary>
    /// 各设备下的板子顺序（型号 → 板 Key 列表）。
    /// </summary>
    public Dictionary<string, List<string>> Boards { get; set; } = [];
}
