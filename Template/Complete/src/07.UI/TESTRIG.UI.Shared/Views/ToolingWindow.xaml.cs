using System.Windows;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 工装/治具管理窗口（组件模板专属）：台账查看/增删/保存。
/// </summary>
public partial class ToolingWindow : ChromeWindow
{
    /// <summary>
    /// 构造工装管理窗口。
    /// </summary>
    /// <param name="vm">工装管理视图模型。</param>
    public ToolingWindow(ToolingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
