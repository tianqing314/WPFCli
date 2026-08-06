using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 菜单一级：设备（DeviceFamily）。支持展开/折叠、搜索过滤。
/// </summary>
public sealed partial class DeviceGroupViewModel : ObservableObject
{
    /// <summary>
    /// 设备家族名。
    /// </summary>
    public required string Device { get; init; }

    /// <summary>
    /// 该设备下的板子集合。
    /// </summary>
    public ObservableCollection<BoardItemViewModel> Boards { get; } = [];

    /// <summary>
    /// Expander 是否展开。
    /// </summary>
    [ObservableProperty] private bool _isExpanded;

    /// <summary>
    /// 搜索过滤后该设备组是否可见。
    /// </summary>
    [ObservableProperty] private bool _isVisible = true;

    /// <summary>
    /// 当前搜索文本。
    /// </summary>
    private string _filter = "";

    /// <summary>
    /// 板子过滤视图缓存。
    /// </summary>
    private ICollectionView? _boardsView;

    /// <summary>
    /// 组内板子的过滤视图（按搜索文本模糊匹配板名/设备名）。
    /// </summary>
    public ICollectionView BoardsView => _boardsView ??= BuildBoardsView();

    /// <summary>
    /// 构建板子过滤视图。
    /// </summary>
    /// <returns>集合视图。</returns>
    private ICollectionView BuildBoardsView()
    {
        var v = CollectionViewSource.GetDefaultView(Boards);
        v.Filter = o => o is BoardItemViewModel b && (
            string.IsNullOrWhiteSpace(_filter)
            || b.BoardName.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || Device.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        return v;
    }

    /// <summary>
    /// 应用搜索文本，刷新组内板子视图并更新本组可见性。
    /// </summary>
    /// <param name="text">搜索文本。</param>
    public void ApplyFilter(string? text)
    {
        _filter = text ?? "";
        BoardsView.Refresh();
        var deviceMatch = string.IsNullOrWhiteSpace(_filter) || Device.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        var anyBoard = Boards.Any(b => b.BoardName.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        IsVisible = deviceMatch || anyBoard;
    }
}

/// <summary>
/// 菜单二级：板子（来自针床清单，纯数据，非反射）。
/// </summary>
public sealed partial class BoardItemViewModel : ObservableObject
{
    /// <summary>
    /// 任务 Key（=manifest.Key）。
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 板子名称。
    /// </summary>
    public required string BoardName { get; init; }

    /// <summary>
    /// 所属设备家族。
    /// </summary>
    public required string Device { get; init; }

    /// <summary>
    /// 板子描述。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 卡片 meta 行（最近使用卡片第三行，形如「串口 · 1 号位 · 11 测试项」，对应设计稿 05 rc-meta）。
    /// </summary>
    public string Meta { get; init; } = "";

    /// <summary>
    /// 该板子的测试任务是否正在运行（菜单项闪烁提示；配合已打开页面缓存实现切换后状态保持）。
    /// </summary>
    [ObservableProperty] private bool _isRunning;
}
