using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 单 SN 记录窗口（该 SN 各测试项 + 双击看详情）。
/// </summary>
public partial class SnRecordWindow : ChromeWindow
{
    /// <summary>
    /// 注入 SN 记录 ViewModel 构造。
    /// </summary>
    /// <param name="vm">SN 记录 ViewModel。</param>
    public SnRecordWindow(SnRecordViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>
    /// 双击测试项行 → 弹出该项详情。用 Preview（隧道）在只读 TextBox 单元格消费点击前拿到双击，
    /// 保证单元格文本可选中复制的同时不丢「双击看详情」交互。
    /// </summary>
    /// <param name="sender">事件源（DataGrid）。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is { DataContext: SnStepRowViewModel row } && DataContext is SnRecordViewModel vm)
        {
            vm.ShowDetailCommand.Execute(row);
        }
    }

    /// <summary>从命中元素向上找所属 DataGridRow。</summary>
    /// <param name="src">命中的可视元素。</param>
    /// <returns>所属行，找不到返回 null。</returns>
    private static DataGridRow? FindRow(DependencyObject? src)
    {
        while (src is not null and not DataGridRow)
        {
            src = VisualTreeHelper.GetParent(src);
        }

        return src as DataGridRow;
    }
}
