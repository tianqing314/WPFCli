using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Core.Abstractions;
using TESTRIG.Infrastructure.Notifications;
using TESTRIG.Jigs;
using TESTRIG.UI.Shared.Services;
using TESTRIG.UI.Shared.Views;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 针床清单（TESTRIG）维护页：左侧 TESTRIG 列表（新增/删除），右侧选中清单的元信息 + 号位 + 测试项编辑，
/// 保存写回 <c>Manifests/&lt;设备&gt;/&lt;Key&gt;.json</c> 并即时重载目录。仅管理员入口。
/// </summary>
public partial class ManifestMaintenanceViewModel : ObservableObject
{
    /// <summary>
    /// 针床目录（读/存/删/重载）。
    /// </summary>
    private readonly JigCatalog _catalog;

    /// <summary>
    /// 全部已注册测试项处理器（供测试项 Kind 下拉，按设备族过滤）。
    /// </summary>
    private readonly IReadOnlyList<IStepHandler> _handlers;

    /// <summary>
    /// 通知服务（状态栏）。
    /// </summary>
    private readonly INotificationService _notify;

    /// <summary>
    /// 共享设备（标准模块）配置仓储（读写在 Manifests 下 .shared.json，工装级）。
    /// </summary>
    private readonly ISharedDeviceStore _sharedStore;

    /// <summary>
    /// 目录是否被改动过（增删改）——关闭时据此让主菜单重建。
    /// </summary>
    public bool CatalogChanged { get; private set; }

    /// <summary>
    /// 构造维护页 VM，载入现有清单列表。
    /// </summary>
    /// <param name="catalog">针床目录。</param>
    /// <param name="handlers">已注册测试项处理器。</param>
    /// <param name="notify">通知服务。</param>
    /// <param name="sharedStore">共享设备配置仓储。</param>
    public ManifestMaintenanceViewModel(JigCatalog catalog, IEnumerable<IStepHandler> handlers, INotificationService notify, ISharedDeviceStore sharedStore)
    {
        _catalog = catalog;
        _handlers = handlers.ToList();
        _notify = notify;
        _sharedStore = sharedStore;
        GroupsView = CollectionViewSource.GetDefaultView(Groups);
        GroupsView.Filter = o => o is ManifestGroupViewModel g && g.IsVisible;
        RefreshList(null);
    }

    /// <summary>
    /// 左侧 TESTRIG 列表（一级设备族分组，二级板子；支持搜索与整体展开/折叠）。
    /// </summary>
    public ObservableCollection<ManifestGroupViewModel> Groups { get; } = [];

    /// <summary>
    /// 设备族分组的过滤视图（按搜索文本隐藏不匹配的分组）。
    /// </summary>
    public ICollectionView GroupsView { get; }

    /// <summary>
    /// 搜索文本：按设备族 / 板名 / Key 模糊筛选。
    /// </summary>
    [ObservableProperty] private string _searchText = "";

    /// <summary>
    /// 搜索文本变化：对各分组应用过滤，命中分组自动展开，刷新视图与展开标志。
    /// </summary>
    /// <param name="value">新搜索文本。</param>
    partial void OnSearchTextChanged(string value)
    {
        foreach (var g in Groups)
        {
            g.ApplyFilter(value);
            if (!string.IsNullOrWhiteSpace(value) && g.IsVisible)
            {
                g.IsExpanded = true;
            }
        }

        GroupsView.Refresh();
        RecomputeAllExpanded();
    }

    /// <summary>
    /// 可见分组是否全部展开——驱动整体展开/折叠按钮图标。
    /// </summary>
    [ObservableProperty] private bool _allExpanded;

    /// <summary>
    /// 一键切换：全部展开 ↔ 全部折叠（仅作用于可见分组）。
    /// </summary>
    [RelayCommand]
    private void ToggleExpandAll()
    {
        var expand = !AllExpanded;
        foreach (var g in Groups)
        {
            if (g.IsVisible)
            {
                g.IsExpanded = expand;
            }
        }

        RecomputeAllExpanded();
    }

    /// <summary>
    /// 依可见分组的展开状态重算「全部展开」标志。
    /// </summary>
    private void RecomputeAllExpanded()
    {
        var vis = Groups.Where(g => g.IsVisible).ToList();
        AllExpanded = vis.Count > 0 && vis.All(g => g.IsExpanded);
    }

    /// <summary>
    /// 选中某块板（左侧二级项点击）→ 载入右侧编辑区。
    /// </summary>
    /// <param name="item">目标列表项。</param>
    [RelayCommand]
    private void SelectManifest(ManifestListItem? item)
    {
        if (item is not null)
        {
            SelectedItem = item;
        }
    }

    /// <summary>
    /// 当前选中的列表项。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ManifestListItem? _selectedItem;

    /// <summary>
    /// 当前编辑中的清单（新建或选中加载）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    private ManifestEditModel? _current;

    /// <summary>
    /// 当前编辑清单里选中的测试项（编辑/删除/上移下移目标）。
    /// </summary>
    [ObservableProperty] private StepEditModel? _selectedStep;

    /// <summary>
    /// 当前编辑清单里选中的号位（删除目标）。
    /// </summary>
    [ObservableProperty] private PositionEditModel? _selectedPosition;

    /// <summary>
    /// 当前编辑清单里选中的共享设备（删除目标）。
    /// </summary>
    [ObservableProperty] private ToolDeviceEditModel? _selectedSharedDevice;

    /// <summary>
    /// 通讯库实例下拉项（来源 refdlls 的 Xmas11.Comm.Devices.*.dll，反射枚举一次）。
    /// </summary>
    public IReadOnlyList<CommLibraryEntry> CommLibraries { get; } = CommLibraryScanner.Scan();

    /// <summary>
    /// 新增一条共享设备（标准模块）行（默认串口，未落盘）。
    /// </summary>
    [RelayCommand]
    private void AddSharedDevice()
    {
        if (Current is null)
        {
            return;
        }

        var row = new ToolDeviceEditModel
        {
            Key = $"STD{Current.SharedDevices.Count + 1}",
            Name = "标准模块",
            Model = CommLibraries.FirstOrDefault()?.Model ?? "",
            Link = LinkType.Serial,
        };
        Current.SharedDevices.Add(row);
        SelectedSharedDevice = row;
    }

    /// <summary>
    /// 删除选中的共享设备行。
    /// </summary>
    [RelayCommand]
    private void RemoveSharedDevice()
    {
        if (Current is null || SelectedSharedDevice is null)
        {
            return;
        }

        Current.SharedDevices.Remove(SelectedSharedDevice);
        SelectedSharedDevice = null;
    }

    /// <summary>
    /// 是否有清单在编辑区。
    /// </summary>
    public bool HasCurrent => Current is not null;

    /// <summary>
    /// 是否选中了列表项。
    /// </summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>
    /// 选中列表项变化 → 从目录加载为可编辑模型。
    /// </summary>
    /// <param name="value">新选中项。</param>
    partial void OnSelectedItemChanged(ManifestListItem? value)
    {
        if (value is null)
        {
            return;
        }

        var m = _catalog.Find(value.Key);
        if (m is null)
        {
            return;
        }

        Current = ManifestEditModel.From(m);
        // 共享设备（标准模块）：独立配置优先（测试项维护写入的 .shared.json），否则回落 manifest 默认（References 转换）
        Current.SharedDevices.Clear();
        var shared = _sharedStore.Load(m.DeviceFamily, m.Key) ?? m.ToolDevices;
        foreach (var t in shared)
        {
            Current.SharedDevices.Add(ToolDeviceEditModel.From(t));
        }
        SelectedStep = null;
        SelectedPosition = null;
    }

    /// <summary>
    /// 刷新左侧列表；可指定刷新后要选中的 Key。
    /// </summary>
    /// <param name="selectKey">刷新后选中的清单 Key（null 则不选）。</param>
    private void RefreshList(string? selectKey)
    {
        // 记住刷新前的展开状态，避免重建后整片折叠。
        var expandedFamilies = Groups.Where(g => g.IsExpanded).Select(g => g.Device)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Groups.Clear();
        foreach (var grp in _catalog.Jigs
                     .OrderBy(j => j.DeviceFamily)
                     .ThenBy(j => j.BoardName)
                     .GroupBy(j => j.DeviceFamily))
        {
            var g = new ManifestGroupViewModel
            {
                Device = grp.Key,
                IsExpanded = expandedFamilies.Count == 0 || expandedFamilies.Contains(grp.Key),
            };
            foreach (var j in grp)
            {
                g.Items.Add(new ManifestListItem(j.Key, j.DeviceFamily, j.BoardName, j.Steps.Count));
            }

            g.ApplyFilter(SearchText);
            Groups.Add(g);
        }

        GroupsView.Refresh();
        RecomputeAllExpanded();

        if (selectKey is not null)
        {
            var hit = Groups.SelectMany(g => g.Items)
                .FirstOrDefault(m => string.Equals(m.Key, selectKey, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                // 保证命中项所在分组展开、可见。
                var owner = Groups.First(g => g.Items.Contains(hit));
                owner.IsExpanded = true;
                SelectedItem = hit;
            }
        }
    }

    /// <summary>
    /// 当前清单设备族下可用的测试项 Kind（含通用处理器：DeviceFamily 为空）。
    /// </summary>
    public IReadOnlyList<string> CurrentKinds
    {
        get
        {
            var family = Current?.DeviceFamily ?? "";
            return _handlers
                .Where(h => string.IsNullOrEmpty(h.DeviceFamily)
                            || string.Equals(h.DeviceFamily, family, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Kind)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();
        }
    }

    /// <summary>
    /// 新建空白清单进入编辑区（未落盘）。
    /// </summary>
    [RelayCommand]
    private void NewManifest()
    {
        SelectedItem = null;
        var m = new ManifestEditModel
        {
            Key = "NewBoard",
            DeviceFamily = "NewDevice",
            BoardName = "新板子",
            DutName = "被检板",
            DutModel = "",
        };
        m.Positions.Add(new PositionEditModel { Index = 1, Name = "1号位" });
        Current = m;
        SelectedStep = null;
        SelectedPosition = null;
        OnPropertyChanged(nameof(CurrentKinds));
    }

    /// <summary>
    /// 删除选中清单（磁盘文件一并删除，需确认）。
    /// </summary>
    [RelayCommand]
    private void DeleteManifest()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        if (!AppDialog.ConfirmDanger("删除 TESTRIG", $"确认删除清单【{item.Device} · {item.Board}】(Key={item.Key})？\n将删除对应 JSON 文件，不可恢复。"))
        {
            return;
        }

        if (_catalog.Delete(item.Key))
        {
            CatalogChanged = true;
            _notify.Notify($"已删除 TESTRIG：{item.Board}");
            Current = null;
            RefreshList(null);
        }
        else
        {
            AppDialog.Error("删除失败", "未找到对应文件，可能已被移除。");
        }
    }

    /// <summary>
    /// 保存当前清单（校验后写回磁盘并重载）。
    /// </summary>
    [RelayCommand]
    private void SaveManifest()
    {
        var m = Current;
        if (m is null)
        {
            return;
        }

        var error = m.Validate();
        if (error is not null)
        {
            AppDialog.Error("无法保存", error);
            return;
        }

        // Key 冲突校验（新建或改名到已存在的其它清单 Key）
        var conflict = _catalog.Jigs.Any(j =>
            string.Equals(j.Key, m.Key, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(j.Key, m.OriginalKey, StringComparison.OrdinalIgnoreCase));
        if (conflict)
        {
            AppDialog.Error("无法保存", $"已存在相同 Key 的清单：{m.Key}，请改用其它 Key。");
            return;
        }

        try
        {
            _catalog.Save(m.ToManifest(), m.OriginalKey);
            // 共享设备（标准模块）独立配置：存在即完全取代 manifest 默认（可删除），运行时与连接配置页均以它为准
            _sharedStore.Save(m.DeviceFamily.Trim(), m.Key.Trim(), m.SharedDevices.Select(s => s.ToDescriptor()).ToList());
            m.OriginalKey = m.Key;
            CatalogChanged = true;
            _notify.Notify($"已保存 TESTRIG：{m.BoardName}");
            RefreshList(m.Key);
            AppToast.Success($"已保存：{m.BoardName}", "清单与共享设备配置已写回 JSON 并重载目录");
        }
        catch (Exception ex)
        {
            AppDialog.Error("保存失败", ex.Message);
        }
    }

    /// <summary>
    /// 放弃当前编辑（回到未选中；不落盘）。
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        var discarded = Current is not null;
        Current = null;
        SelectedItem = null;
        if (discarded)
        {
            AppToast.Success("已放弃修改", "未保存的编辑内容已还原");
        }
    }

    // ===== 号位 =====

    /// <summary>
    /// 新增号位（序号取现有最大 +1）：弹编辑器（含端点），确认后追加。
    /// </summary>
    [RelayCommand]
    private void AddPosition()
    {
        if (Current is null)
        {
            return;
        }

        var idx = Current.Positions.Count == 0 ? 1 : Current.Positions.Max(p => p.Index) + 1;
        var working = new PositionEditModel { Index = idx, Name = $"{idx}号位" };
        if (EditPositionDialog(working))
        {
            Current.Positions.Add(working);
        }
    }

    /// <summary>
    /// 编辑选中号位（含连接端点）：弹编辑器（传入副本），确认后回写。
    /// </summary>
    /// <param name="pos">目标号位（命令参数优先，否则用选中项）。</param>
    [RelayCommand]
    private void EditPosition(PositionEditModel? pos)
    {
        var target = pos ?? SelectedPosition;
        if (Current is null || target is null)
        {
            return;
        }

        var working = target.Clone();
        if (EditPositionDialog(working))
        {
            target.CopyFrom(working);
        }
    }

    /// <summary>
    /// 删除选中号位。
    /// </summary>
    [RelayCommand]
    private void DeletePosition()
    {
        if (Current is null || SelectedPosition is null)
        {
            return;
        }

        Current.Positions.Remove(SelectedPosition);
        SelectedPosition = null;
    }

    /// <summary>
    /// 弹出号位编辑器窗口；返回是否确认。
    /// </summary>
    /// <param name="working">编辑的工作副本。</param>
    /// <returns>用户是否点了确定。</returns>
    private bool EditPositionDialog(PositionEditModel working)
    {
        var vm = new PositionEditorViewModel(working);
        var win = new PositionEditorWindow(vm) { Owner = Application.Current.MainWindow };
        win.ShowDialog();
        return vm.Confirmed;
    }

    // ===== 测试项 =====

    /// <summary>
    /// 新增测试项：弹编辑器，确认后追加。
    /// </summary>
    [RelayCommand]
    private void AddStep()
    {
        if (Current is null)
        {
            return;
        }

        var working = new StepEditModel { Kind = CurrentKinds.FirstOrDefault() ?? "" };
        if (EditStepDialog(working))
        {
            Current.Steps.Add(working);
        }
    }

    /// <summary>
    /// 查看/编辑选中测试项：弹编辑器（传入副本），确认后回写。
    /// </summary>
    /// <param name="step">目标测试项（命令参数优先，否则用选中项）。</param>
    [RelayCommand]
    private void EditStep(StepEditModel? step)
    {
        var target = step ?? SelectedStep;
        if (Current is null || target is null)
        {
            return;
        }

        var working = target.Clone();
        if (EditStepDialog(working))
        {
            target.CopyFrom(working);
        }
    }

    /// <summary>
    /// 删除选中测试项（需确认）。
    /// </summary>
    [RelayCommand]
    private void DeleteStep()
    {
        if (Current is null || SelectedStep is null)
        {
            return;
        }

        if (!AppDialog.ConfirmDanger("删除测试项", $"确认删除测试项【{SelectedStep.Name}】？"))
        {
            return;
        }

        Current.Steps.Remove(SelectedStep);
        SelectedStep = null;
    }

    /// <summary>
    /// 选中测试项上移（执行顺序 = 列表位置）。
    /// </summary>
    [RelayCommand]
    private void MoveStepUp()
    {
        if (Current is null || SelectedStep is null)
        {
            return;
        }

        var i = Current.Steps.IndexOf(SelectedStep);
        if (i > 0)
        {
            Current.Steps.Move(i, i - 1);
        }
    }

    /// <summary>
    /// 选中测试项下移。
    /// </summary>
    [RelayCommand]
    private void MoveStepDown()
    {
        if (Current is null || SelectedStep is null)
        {
            return;
        }

        var i = Current.Steps.IndexOf(SelectedStep);
        if (i >= 0 && i < Current.Steps.Count - 1)
        {
            Current.Steps.Move(i, i + 1);
        }
    }

    /// <summary>
    /// 弹出测试项编辑器窗口；返回是否确认。
    /// </summary>
    /// <param name="working">编辑的工作副本。</param>
    /// <returns>用户是否点了确定。</returns>
    private bool EditStepDialog(StepEditModel working)
    {
        var vm = new StepEditorViewModel(working, CurrentKinds);
        var win = new StepEditorWindow(vm) { Owner = Application.Current.MainWindow };
        win.ShowDialog();
        return vm.Confirmed;
    }
}

/// <summary>
/// 左侧列表项（轻量）。
/// </summary>
/// <param name="Key">清单 Key。</param>
/// <param name="Device">设备族。</param>
/// <param name="Board">板名。</param>
/// <param name="StepCount">测试项数。</param>
public sealed record ManifestListItem(string Key, string Device, string Board, int StepCount);

/// <summary>
/// 左侧一级分组：设备族（DeviceFamily）。含展开/折叠、搜索过滤，复用登录后设备树的交互。
/// </summary>
public sealed partial class ManifestGroupViewModel : ObservableObject
{
    /// <summary>
    /// 设备族名。
    /// </summary>
    public required string Device { get; init; }

    /// <summary>
    /// 该设备族下的板子。
    /// </summary>
    public ObservableCollection<ManifestListItem> Items { get; } = [];

    /// <summary>
    /// 是否展开。
    /// </summary>
    [ObservableProperty] private bool _isExpanded = true;

    /// <summary>
    /// 搜索过滤后本分组是否可见。
    /// </summary>
    [ObservableProperty] private bool _isVisible = true;

    /// <summary>
    /// 当前搜索文本。
    /// </summary>
    private string _filter = "";

    /// <summary>
    /// 板子过滤视图缓存。
    /// </summary>
    private ICollectionView? _itemsView;

    /// <summary>
    /// 组内板子的过滤视图（按搜索文本模糊匹配板名 / 设备族 / Key）。
    /// </summary>
    public ICollectionView ItemsView => _itemsView ??= BuildItemsView();

    /// <summary>
    /// 构建板子过滤视图。
    /// </summary>
    /// <returns>集合视图。</returns>
    private ICollectionView BuildItemsView()
    {
        var v = CollectionViewSource.GetDefaultView(Items);
        v.Filter = o => o is ManifestListItem m && Match(m, _filter);
        return v;
    }

    /// <summary>
    /// 应用搜索文本，刷新组内板子视图并更新本组可见性。
    /// </summary>
    /// <param name="text">搜索文本。</param>
    public void ApplyFilter(string? text)
    {
        _filter = text ?? "";
        ItemsView.Refresh();
        var deviceMatch = string.IsNullOrWhiteSpace(_filter)
            || Device.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        IsVisible = deviceMatch || Items.Any(m => Match(m, _filter));
    }

    /// <summary>
    /// 单项是否命中搜索文本（板名 / 设备族 / Key）。
    /// </summary>
    /// <param name="m">列表项。</param>
    /// <param name="filter">搜索文本。</param>
    /// <returns>是否命中。</returns>
    private static bool Match(ManifestListItem m, string filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || m.Board.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || m.Device.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || m.Key.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 可编辑的清单模型（与不可变 <see cref="JigManifest"/> 互转）。
/// </summary>
public partial class ManifestEditModel : ObservableObject
{
    /// <summary>
    /// 编辑前的原 Key（新建为 null）；保存时用于改名删旧文件。
    /// </summary>
    public string? OriginalKey { get; set; }

    /// <summary>清单唯一 Key。</summary>
    [ObservableProperty] private string _key = "";

    /// <summary>设备族（菜单一级）。</summary>
    [ObservableProperty] private string _deviceFamily = "";

    /// <summary>板名（菜单二级）。</summary>
    [ObservableProperty] private string _boardName = "";

    /// <summary>描述。</summary>
    [ObservableProperty] private string _description = "";

    /// <summary>被检设备名。</summary>
    [ObservableProperty] private string _dutName = "";

    /// <summary>被检型号：一板一型号，决定 DUT 驱动 + 结果落库的 DeviceModel，不可与其它板重复。</summary>
    [ObservableProperty] private string _dutModel = "";

    /// <summary>
    /// 被检连接端点（维护页不编辑，保存时原样保留；端点在连接配置页维护）。
    /// </summary>
    public CommEndpoint? DutComm { get; set; }

    /// <summary>号位集合。</summary>
    public ObservableCollection<PositionEditModel> Positions { get; } = [];

    /// <summary>测试项集合。</summary>
    public ObservableCollection<StepEditModel> Steps { get; } = [];

    /// <summary>共享设备（标准模块）集合：整机等模板每套工装的共享设备清单（测试项维护中配置）。</summary>
    public ObservableCollection<ToolDeviceEditModel> SharedDevices { get; } = [];

    /// <summary>
    /// 从不可变清单构建可编辑模型。
    /// </summary>
    /// <param name="m">清单。</param>
    /// <returns>可编辑模型。</returns>
    public static ManifestEditModel From(JigManifest m)
    {
        var em = new ManifestEditModel
        {
            OriginalKey = m.Key,
            Key = m.Key,
            DeviceFamily = m.DeviceFamily,
            BoardName = m.BoardName,
            Description = m.Description,
            DutName = m.Dut.Name,
            DutModel = m.Dut.Model,
            DutComm = m.Dut.Comm,
        };
        foreach (var p in m.Positions)
        {
            em.Positions.Add(new PositionEditModel { Index = p.Index, Name = p.Name, Comm = p.Comm });
        }
        foreach (var s in m.Steps)
        {
            em.Steps.Add(StepEditModel.From(s));
        }
        return em;
    }

    /// <summary>
    /// 转回不可变清单（用于落盘）。
    /// </summary>
    /// <returns>清单。</returns>
    public JigManifest ToManifest()
    {
        return new JigManifest
        {
            Key = Key.Trim(),
            DeviceFamily = DeviceFamily.Trim(),
            BoardName = BoardName.Trim(),
            Description = Description.Trim(),
            Dut = new DeviceDescriptor(DutName.Trim(), DutModel.Trim(), DutComm),
            Positions = Positions.Select(p => new PositionDescriptor(p.Index, p.Name.Trim()) { Comm = p.Comm }).ToList(),
            Steps = Steps.Select(s => s.ToStep()).ToList(),
            ToolDevices = SharedDevices.Select(s => s.ToDescriptor()).ToList(),
        };
    }

    /// <summary>
    /// 校验必填项，返回首个错误信息（无误返回 null）。
    /// </summary>
    /// <returns>错误信息或 null。</returns>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Key)) { return "Key 不能为空。"; }
        if (string.IsNullOrWhiteSpace(DeviceFamily)) { return "设备族不能为空。"; }
        if (string.IsNullOrWhiteSpace(BoardName)) { return "板名不能为空。"; }
        if (string.IsNullOrWhiteSpace(DutModel)) { return "被检型号不能为空。"; }
        if (Positions.Count == 0) { return "至少需要一个号位。"; }
        if (Steps.Count == 0) { return "至少需要一个测试项。"; }
        if (Steps.Any(s => string.IsNullOrWhiteSpace(s.Key) || string.IsNullOrWhiteSpace(s.Kind) || string.IsNullOrWhiteSpace(s.Name)))
        {
            return "存在测试项的 Key/Kind/名称为空。";
        }
        if (SharedDevices.Any(d => string.IsNullOrWhiteSpace(d.Key) || string.IsNullOrWhiteSpace(d.Model)))
        {
            return "存在共享设备的 Key/通讯库实例为空。";
        }
        if (SharedDevices.GroupBy(d => d.Key.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            return "共享设备的 Key 不能重复。";
        }
        return null;
    }
}

/// <summary>
/// 可编辑共享设备（标准模块）行：通讯库实例（Model）/名称/通讯方式（串口|网口）/串口或网口参数/序列号（可空）。
/// </summary>
public partial class ToolDeviceEditModel : ObservableObject
{
    /// <summary>实例键（如 DPSEX1/DPSEX2），处理器按此获取。</summary>
    [ObservableProperty] private string _key = "";

    /// <summary>标准设备名称（共享设备显示，如"正压模块"）。</summary>
    [ObservableProperty] private string _name = "";

    /// <summary>型号（通讯库实例去 Base 后缀，如 DPSEXBase → DPSEX），按 [DutDriver] 匹配驱动。</summary>
    [ObservableProperty] private string _model = "";

    /// <summary>通讯方式。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsEthernet))]
    private LinkType _link = LinkType.Serial;

    /// <summary>串口波特率。</summary>
    [ObservableProperty] private int _baud = 4800;

    /// <summary>串口数据位。</summary>
    [ObservableProperty] private int _dataBits = 8;

    /// <summary>串口停止位。</summary>
    [ObservableProperty] private string _stopBits = "Two";

    /// <summary>串口校验位。</summary>
    [ObservableProperty] private string _parity = "None";

    /// <summary>网口 IP。</summary>
    [ObservableProperty] private string _ip = "";

    /// <summary>网口端口。</summary>
    [ObservableProperty] private int _port = 1030;

    /// <summary>序列号（DevSn，可空 = 连接不校验序列号，按 IsExist 判定）。</summary>
    [ObservableProperty] private string? _serialNumber;

    /// <summary>是否串口通讯（XAML 可见性）。</summary>
    public bool IsSerial => Link == LinkType.Serial;

    /// <summary>是否网口通讯（XAML 可见性）。</summary>
    public bool IsEthernet => Link == LinkType.Ethernet;

    /// <summary>通讯方式（XAML 下拉）。</summary>
    public IReadOnlyList<KeyValuePair<LinkType, string>> LinkOptions { get; } =
    [
        new(LinkType.Serial, "串口"),
        new(LinkType.Ethernet, "网口"),
    ];

    /// <summary>波特率下拉。</summary>
    public int[] BaudOptions { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    /// <summary>数据位下拉。</summary>
    public int[] DataBitsOptions { get; } = [5, 6, 7, 8];

    /// <summary>停止位下拉。</summary>
    public string[] StopBitsOptions { get; } = ["One", "Two"];

    /// <summary>校验位下拉。</summary>
    public string[] ParityOptions { get; } = ["None", "Odd", "Even"];

    /// <summary>
    /// 转不可变描述（落盘用）。物理链路留空——实际 COM 在连接配置页按工装选择。
    /// </summary>
    /// <returns>标准模块描述。</returns>
    public ToolDeviceDescriptor ToDescriptor()
    {
        return new ToolDeviceDescriptor(Key.Trim(), Name.Trim(), Model.Trim())
        {
            Comm = Link switch
            {
                LinkType.Ethernet => CommEndpoint.OfEthernet(Ip.Trim(), Port),
                _ => CommEndpoint.OfSerial("", new SerialParams(Baud, DataBits, StopBits, Parity)),
            },
            SerialNumber = string.IsNullOrWhiteSpace(SerialNumber) ? null : SerialNumber.Trim(),
        };
    }

    /// <summary>
    /// 从不可变描述构建可编辑行。
    /// </summary>
    /// <param name="d">标准模块描述。</param>
    /// <returns>可编辑行。</returns>
    public static ToolDeviceEditModel From(ToolDeviceDescriptor d)
    {
        var em = new ToolDeviceEditModel
        {
            Key = d.Key,
            Name = d.Name,
            Model = d.Model,
            SerialNumber = d.SerialNumber,
        };
        if (d.Comm is not null)
        {
            em.Link = d.Comm.Link;
            em.Ip = d.Comm.Ip ?? "";
            em.Port = d.Comm.Port ?? 1030;
            if (d.Comm.Serial is not null)
            {
                em.Baud = d.Comm.Serial.Baud;
                em.DataBits = d.Comm.Serial.DataBits;
                em.StopBits = d.Comm.Serial.StopBits;
                em.Parity = d.Comm.Serial.Parity;
            }
        }
        return em;
    }
}

/// <summary>
/// 可编辑号位。
/// </summary>
public partial class PositionEditModel : ObservableObject
{
    /// <summary>号位序号。</summary>
    [ObservableProperty] private int _index;

    /// <summary>号位名称。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private string _name = "";

    /// <summary>
    /// 连接端点（可在号位编辑器里改：无/网络/串口/USB）。
    /// </summary>
    public CommEndpoint? Comm { get; set; }

    /// <summary>
    /// 端点可读描述（列表显示）。
    /// </summary>
    public string CommText => Comm?.Describe() ?? "（无端点）";

    /// <summary>号位显示名（列表）。</summary>
    public string Display => $"{Index}　{Name}";

    /// <summary>
    /// 端点变化后刷新描述（编辑器确认时调用）。
    /// </summary>
    public void NotifyCommChanged()
    {
        OnPropertyChanged(nameof(CommText));
    }

    /// <summary>深拷贝（编辑器工作副本）。</summary>
    /// <returns>副本。</returns>
    public PositionEditModel Clone()
    {
        return new PositionEditModel { Index = Index, Name = Name, Comm = Comm };
    }

    /// <summary>用另一号位覆盖本对象（编辑器确认回写）。</summary>
    /// <param name="o">源。</param>
    public void CopyFrom(PositionEditModel o)
    {
        Index = o.Index;
        Name = o.Name;
        Comm = o.Comm;
        NotifyCommChanged();
    }
}

/// <summary>
/// 可编辑测试项。
/// </summary>
public partial class StepEditModel : ObservableObject
{
    /// <summary>测试项唯一标识。</summary>
    [ObservableProperty] private string _key = "";

    /// <summary>处理器类型。</summary>
    [ObservableProperty] private string _kind = "";

    /// <summary>显示名。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private string _name = "";

    /// <summary>描述。</summary>
    [ObservableProperty] private string _description = "";

    /// <summary>测试项 GUID（保持稳定）。</summary>
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>设置项集合（key→value）。</summary>
    public ObservableCollection<SettingEditModel> Settings { get; } = [];

    /// <summary>参数集合。</summary>
    public ObservableCollection<ParameterEditModel> Parameters { get; } = [];

    /// <summary>判定条件集合。</summary>
    public ObservableCollection<ConditionEditModel> Conditions { get; } = [];

    /// <summary>列表显示名（含 Kind）。</summary>
    public string Display => Name;

    /// <summary>条件/参数/设置项 摘要（列表次要信息）。</summary>
    public string Summary => $"Kind={Kind}　条件×{Conditions.Count}　参数×{Parameters.Count}　设置×{Settings.Count}";

    /// <summary>
    /// 从不可变测试项构建。
    /// </summary>
    /// <param name="s">测试项。</param>
    /// <returns>可编辑测试项。</returns>
    public static StepEditModel From(StepDescriptor s)
    {
        var em = new StepEditModel
        {
            Key = s.Key,
            Kind = s.Kind,
            Name = s.Name,
            Description = s.Description,
            Guid = s.Guid,
        };
        foreach (var kv in s.Settings)
        {
            em.Settings.Add(new SettingEditModel { Key = kv.Key, Value = kv.Value });
        }
        foreach (var p in s.Parameters)
        {
            em.Parameters.Add(new ParameterEditModel { Name = p.Name, Value = p.Value, Unit = p.Unit ?? "" });
        }
        foreach (var c in s.Conditions)
        {
            em.Conditions.Add(ConditionEditModel.From(c));
        }
        return em;
    }

    /// <summary>
    /// 转回不可变测试项。
    /// </summary>
    /// <returns>测试项。</returns>
    public StepDescriptor ToStep()
    {
        return new StepDescriptor
        {
            Key = Key.Trim(),
            Kind = Kind.Trim(),
            Name = Name.Trim(),
            Description = Description.Trim(),
            Guid = Guid,
            Settings = Settings.Where(s => !string.IsNullOrWhiteSpace(s.Key))
                               .ToDictionary(s => s.Key.Trim(), s => s.Value),
            Parameters = Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name))
                                   .Select(p => new ParameterDescriptor(p.Name.Trim(), p.Value, string.IsNullOrWhiteSpace(p.Unit) ? null : p.Unit.Trim()))
                                   .ToList(),
            Conditions = Conditions.Select(c => c.ToCondition()).ToList(),
        };
    }

    /// <summary>
    /// 深拷贝（编辑器工作副本，取消不影响原对象）。
    /// </summary>
    /// <returns>副本。</returns>
    public StepEditModel Clone()
    {
        return From(ToStep());
    }

    /// <summary>
    /// 用另一测试项内容覆盖本对象（编辑器确认回写）。
    /// </summary>
    /// <param name="o">源。</param>
    public void CopyFrom(StepEditModel o)
    {
        Key = o.Key;
        Kind = o.Kind;
        Name = o.Name;
        Description = o.Description;
        Guid = o.Guid;
        Settings.Clear();
        foreach (var s in o.Settings) { Settings.Add(new SettingEditModel { Key = s.Key, Value = s.Value }); }
        Parameters.Clear();
        foreach (var p in o.Parameters) { Parameters.Add(new ParameterEditModel { Name = p.Name, Value = p.Value, Unit = p.Unit }); }
        Conditions.Clear();
        foreach (var c in o.Conditions) { Conditions.Add(new ConditionEditModel { Kind = c.Kind, Name = c.Name, Min = c.Min, Max = c.Max, Expected = c.Expected, Unit = c.Unit }); }
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(Summary));
    }
}

/// <summary>
/// 可编辑设置项（key→value）。
/// </summary>
public partial class SettingEditModel : ObservableObject
{
    /// <summary>键。</summary>
    [ObservableProperty] private string _key = "";

    /// <summary>值。</summary>
    [ObservableProperty] private string _value = "";
}

/// <summary>
/// 可编辑参数。
/// </summary>
public partial class ParameterEditModel : ObservableObject
{
    /// <summary>参数名。</summary>
    [ObservableProperty] private string _name = "";

    /// <summary>参数值。</summary>
    [ObservableProperty] private string _value = "";

    /// <summary>单位。</summary>
    [ObservableProperty] private string _unit = "";
}

/// <summary>
/// 可编辑判定条件。Min/Max/Expected/Unit 存字符串，落盘时按 Kind 解析。
/// </summary>
public partial class ConditionEditModel : ObservableObject
{
    /// <summary>条件类型：Range/Value/Text。</summary>
    [ObservableProperty] private string _kind = "Range";

    /// <summary>条件名称。</summary>
    [ObservableProperty] private string _name = "";

    /// <summary>下限（Range 用）。</summary>
    [ObservableProperty] private string _min = "";

    /// <summary>上限（Range 用）。</summary>
    [ObservableProperty] private string _max = "";

    /// <summary>期望值（Value/Text 用）。</summary>
    [ObservableProperty] private string _expected = "";

    /// <summary>单位。</summary>
    [ObservableProperty] private string _unit = "";

    /// <summary>
    /// 从不可变条件构建。
    /// </summary>
    /// <param name="c">条件。</param>
    /// <returns>可编辑条件。</returns>
    public static ConditionEditModel From(ConditionDescriptor c)
    {
        return new ConditionEditModel
        {
            Kind = c.Kind,
            Name = c.Name,
            Min = c.Min?.ToString(CultureInfo.InvariantCulture) ?? "",
            Max = c.Max?.ToString(CultureInfo.InvariantCulture) ?? "",
            Expected = c.Expected ?? "",
            Unit = c.Unit ?? "",
        };
    }

    /// <summary>
    /// 转回不可变条件。
    /// </summary>
    /// <returns>条件。</returns>
    public ConditionDescriptor ToCondition()
    {
        double? min = double.TryParse(Min, NumberStyles.Any, CultureInfo.InvariantCulture, out var mn) ? mn : null;
        double? max = double.TryParse(Max, NumberStyles.Any, CultureInfo.InvariantCulture, out var mx) ? mx : null;
        return new ConditionDescriptor
        {
            Kind = string.IsNullOrWhiteSpace(Kind) ? "Range" : Kind.Trim(),
            Name = Name.Trim(),
            Min = min,
            Max = max,
            Expected = string.IsNullOrWhiteSpace(Expected) ? null : Expected.Trim(),
            Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim(),
        };
    }
}
