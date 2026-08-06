using System.Windows.Controls;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// TESTRIG / 测试项 维护窗口（仅管理员）。
/// </summary>
public partial class ManifestMaintenanceWindow : ChromeWindow
{
    /// <summary>
    /// 用 VM 构造。
    /// </summary>
    /// <param name="vm">维护页 VM。</param>
    public ManifestMaintenanceWindow(ManifestMaintenanceViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>
    /// 双击测试项行 → 编辑该项。
    /// </summary>
    /// <param name="sender">事件源（DataGridRow）。</param>
    /// <param name="e">鼠标事件。</param>
    private void StepRow_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ManifestMaintenanceViewModel vm
            && sender is DataGridRow { Item: StepEditModel step }
            && vm.EditStepCommand.CanExecute(step))
        {
            vm.EditStepCommand.Execute(step);
        }
    }

    /// <summary>
    /// 双击号位行 → 编辑该号位（含连接端点）。
    /// </summary>
    /// <param name="sender">事件源（DataGridRow）。</param>
    /// <param name="e">鼠标事件。</param>
    private void PositionRow_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ManifestMaintenanceViewModel vm
            && sender is DataGridRow { Item: PositionEditModel pos }
            && vm.EditPositionCommand.CanExecute(pos))
        {
            vm.EditPositionCommand.Execute(pos);
        }
    }
}
