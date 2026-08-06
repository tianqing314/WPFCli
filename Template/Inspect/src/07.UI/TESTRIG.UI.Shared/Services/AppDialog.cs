using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using TESTRIG.UI.Shared.Views.Dialogs;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 应用内 Material 风格对话框，替代系统 MessageBox。
/// </summary>
public static class AppDialog
{
    /// <summary>
    /// 按资源键取画刷，找不到返回灰色。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <returns>画刷。</returns>
    private static Brush Brush(string key)
    {
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    /// <summary>
    /// 确认对话框（确认/取消），返回是否确认。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <returns>是否确认。</returns>
    public static bool Confirm(string title, string message)
    {
        return Show(title, message, PackIconKind.HelpCircleOutline, Brush("Brush.Primary"), showCancel: true, okText: "确 认");
    }

    /// <summary>
    /// 危险确认对话框（红色危险模态，用于删除等破坏性操作）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <param name="okText">确定按钮文本。</param>
    /// <returns>是否确认。</returns>
    public static bool ConfirmDanger(string title, string message, string okText = "删 除")
    {
        return Show(title, message, PackIconKind.AlertOutline, Brush("Brush.Danger"), showCancel: true, okText: okText, danger: true);
    }

    /// <summary>
    /// 信息提示（仅"知道了"）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    public static void Info(string title, string message)
    {
        Show(title, message, PackIconKind.InformationOutline, Brush("Brush.Primary"), showCancel: false, okText: "知道了");
    }

    /// <summary>
    /// 错误提示（仅"知道了"）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    public static void Error(string title, string message)
    {
        Show(title, message, PackIconKind.AlertCircleOutline, Brush("Brush.Danger"), showCancel: false, okText: "知道了");
    }

    /// <summary>
    /// 弹出对话框（居中于主窗或屏幕），返回是否点了确定。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <param name="icon">图标。</param>
    /// <param name="iconBrush">图标颜色。</param>
    /// <param name="showCancel">是否显示取消。</param>
    /// <param name="okText">确定按钮文本。</param>
    /// <returns>是否确认。</returns>
    private static bool Show(string title, string message, PackIconKind icon, Brush iconBrush, bool showCancel, string okText, bool danger = false)
    {
        var dlg = new MessageDialogWindow(title, message, icon, iconBrush, showCancel, okText, danger);
        var owner = Application.Current?.MainWindow;
        if (owner != null && owner.IsLoaded && !ReferenceEquals(owner, dlg))
        {
            dlg.Owner = owner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dlg.ShowDialog() == true;
    }
}
