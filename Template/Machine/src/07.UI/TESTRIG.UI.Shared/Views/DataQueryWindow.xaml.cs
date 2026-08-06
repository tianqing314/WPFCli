using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 数据查询窗口（主表分页查询 + 双击进 SN 记录）。
/// </summary>
public partial class DataQueryWindow : ChromeWindow
{
    /// <summary>
    /// 注入数据查询 ViewModel 构造。
    /// </summary>
    /// <param name="vm">数据查询 ViewModel。</param>
    public DataQueryWindow(DataQueryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>
    /// 双击记录行 → 打开该 SN 的记录窗。用 Preview（隧道）在只读 TextBox 单元格消费点击前拿到双击，
    /// 保证单元格文本可选中复制的同时不丢「双击进 SN」交互。
    /// </summary>
    /// <param name="sender">事件源（DataGrid）。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void Record_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is { DataContext: RecordRowViewModel row } && DataContext is DataQueryViewModel vm)
        {
            vm.OpenSnRecordCommand.Execute(row);
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
