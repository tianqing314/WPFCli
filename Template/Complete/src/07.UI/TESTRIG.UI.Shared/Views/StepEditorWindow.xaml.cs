using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 测试项编辑器窗口（模态）。确认/取消由 VM 的 <see cref="StepEditorViewModel.CloseRequested"/> 驱动关闭。
/// </summary>
public partial class StepEditorWindow : ChromeWindow
{
    /// <summary>
    /// 用 VM 构造，订阅关闭请求。
    /// </summary>
    /// <param name="vm">编辑器 VM。</param>
    public StepEditorWindow(StepEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok =>
        {
            DialogResult = ok;
            Close();
        };
    }
}
