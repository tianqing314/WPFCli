using System.Windows.Controls;
using System.Windows.Input;
using TESTRIG.UI.Shared.ViewModels;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 测试运行视图（号位 × 测试项表格 + 实时日志/曲线）。
/// </summary>
public partial class TestRunView : UserControl
{
    /// <summary>
    /// 构造测试运行视图。
    /// </summary>
    public TestRunView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 双击测试项行 → 调 VM 命令弹出该项过程信息。
    /// </summary>
    /// <param name="sender">事件源（DataGridRow）。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void StepRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { DataContext: StepCellViewModel cell } &&
            DataContext is TestRunViewModel vm)
        {
            vm.ShowStepDetailCommand.Execute(cell);
        }
    }
}
