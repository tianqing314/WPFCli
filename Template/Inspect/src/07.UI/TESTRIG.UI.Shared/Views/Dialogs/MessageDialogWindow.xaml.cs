using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views.Dialogs;

/// <summary>
/// 通用消息对话框（图标 + 文本 + 确定/取消）。
/// </summary>
public partial class MessageDialogWindow : ChromeWindow
{
    /// <summary>
    /// 构造消息对话框。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <param name="icon">图标。</param>
    /// <param name="iconBrush">图标颜色。</param>
    /// <param name="showCancel">是否显示取消按钮。</param>
    /// <param name="okText">确定按钮文本。</param>
    public MessageDialogWindow(string title, string message, PackIconKind icon, Brush iconBrush, bool showCancel, string okText, bool danger = false)
    {
        InitializeComponent();
        Title = title;
        TitleIcon = icon;
        MsgText.Text = message;
        DlgIcon.Kind = icon;
        DlgIcon.Foreground = iconBrush;
        OkBtn.Content = okText;
        CancelBtn.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

        // 破坏性操作：确定按钮改红（危险模态）
        if (danger && TryFindResource("Btn.Danger") is Style dangerStyle)
        {
            OkBtn.Style = dangerStyle;
        }

        // 语义化：标题栏与图标底色按图标色（信息/确认=蓝，错误=红）联动
        TitleBarBrush = iconBrush;
        if (iconBrush is SolidColorBrush scb)
        {
            IconBox.Background = new SolidColorBrush(Tint(scb.Color, 0.86));
        }
    }

    /// <summary>
    /// 把颜色向白色混合，得到浅底色（factor 越大越浅）。
    /// </summary>
    /// <param name="c">基色。</param>
    /// <param name="factor">向白混合比例（0~1）。</param>
    /// <returns>浅色。</returns>
    private static Color Tint(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)(c.R + (255 - c.R) * factor),
            (byte)(c.G + (255 - c.G) * factor),
            (byte)(c.B + (255 - c.B) * factor));
    }

    /// <summary>
    /// 确定：DialogResult=true 并关闭。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 取消：DialogResult=false 并关闭。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
