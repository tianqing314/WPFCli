using System.ComponentModel;
using System.Windows;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 在线升级对话框：非强更可「暂不」关闭；强更（mandatory/minVersion）不可关闭，必须升级。
/// </summary>
public partial class UpdateWindow : ChromeWindow
{
    /// <summary>
    /// 视图模型。
    /// </summary>
    private readonly UpdateViewModel _vm;

    /// <summary>
    /// 注入视图模型构造；强更时隐藏标题栏 ✕。
    /// </summary>
    /// <param name="vm">更新视图模型。</param>
    public UpdateWindow(UpdateViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        if (vm.IsMandatory)
        {
            ShowCaptionButtons = false;
        }
    }

    /// <summary>
    /// 暂不：关闭对话框（仅非强更可见）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 关闭拦截：强更不许关（下载中关闭则取消下载）。
    /// </summary>
    /// <param name="e">关闭事件参数。</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_vm.IsMandatory && Application.Current?.MainWindow?.IsVisible == true)
        {
            e.Cancel = true;
            return;
        }

        _vm.CancelPending();
    }
}
