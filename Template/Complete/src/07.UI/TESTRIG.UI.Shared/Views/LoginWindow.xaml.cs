using System.Windows;
using System.Windows.Input;
using TESTRIG.UI.Shared.ViewModels;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 登录窗口（无边框卡片）。PasswordBox 不可绑定，手动同步到 ViewModel。
/// </summary>
public partial class LoginWindow : Window
{
    /// <summary>
    /// 登录 ViewModel。
    /// </summary>
    private readonly LoginViewModel _vm;

    /// <summary>
    /// 注入登录 ViewModel 构造，订阅登录成功关闭，回填默认密码。
    /// </summary>
    /// <param name="vm">登录 ViewModel。</param>
    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.LoginSucceeded += (_, _) => { DialogResult = true; Close(); };
        // 历史账号选择/回填时，程序化改写的密码同步进不可绑定的 PasswordBox
        _vm.PasswordFilled += (_, pwd) => PasswordBox.Password = pwd;
        // 默认密码（上次登录或演示默认），回填后触发同步
        PasswordBox.Password = vm.Password;
    }

    /// <summary>
    /// 密码框变化时手动同步到 ViewModel（PasswordBox.Password 不支持绑定）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Password = PasswordBox.Password;
        }
    }

    /// <summary>
    /// 无边框窗口：按住卡片空白处可拖动。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
