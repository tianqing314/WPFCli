using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 测试项详情窗口（过程信息 + 过程数据曲线）。
/// </summary>
public partial class StepDetailWindow : ChromeWindow
{
    /// <summary>
    /// 注入测试项详情 ViewModel 构造。
    /// </summary>
    /// <param name="vm">测试项详情 ViewModel。</param>
    public StepDetailWindow(StepDetailViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
