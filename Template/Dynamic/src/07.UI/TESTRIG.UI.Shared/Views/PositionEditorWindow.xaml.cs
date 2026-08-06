using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 号位编辑器窗口（模态）：号位序号/名称 + 完整连接端点。确认/取消由 VM 驱动关闭。
/// </summary>
public partial class PositionEditorWindow : ChromeWindow
{
    /// <summary>
    /// 用 VM 构造，订阅关闭请求。
    /// </summary>
    /// <param name="vm">号位编辑器 VM。</param>
    public PositionEditorWindow(PositionEditorViewModel vm)
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
