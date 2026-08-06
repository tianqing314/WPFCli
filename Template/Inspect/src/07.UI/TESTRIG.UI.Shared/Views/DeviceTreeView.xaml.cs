using System.Windows.Controls;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 设备「型号 → 板子」两级树（搜索 / 全部展开折叠 / 拖动排序）。
/// docked 抽屉与浮层抽屉共用；DataContext 继承自外层 <c>MainViewModel</c>。
/// </summary>
public partial class DeviceTreeView : UserControl
{
    /// <summary>
    /// 初始化组件。
    /// </summary>
    public DeviceTreeView()
    {
        InitializeComponent();
    }
}
