using System.Windows;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 共享设备通讯参数编辑弹窗（串口：波特率/数据位/停止位/校验位；网口：端口号）。
/// </summary>
public partial class CommParamsWindow : ChromeWindow
{
    /// <summary>
    /// 弹窗视图模型。
    /// </summary>
    public ViewModels.CommParamsEditModel ViewModel { get; }

    /// <summary>
    /// 构造通讯参数编辑弹窗。
    /// </summary>
    /// <param name="vm">弹窗视图模型（副本，确定时回写行）。</param>
    public CommParamsWindow(ViewModels.CommParamsEditModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        vm.RequestClose += (_, ok) =>
        {
            DialogResult = ok;
            Close();
        };
        InitializeComponent();
    }
}
