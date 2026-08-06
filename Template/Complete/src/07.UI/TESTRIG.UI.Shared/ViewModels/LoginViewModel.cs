using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Infrastructure.Auth;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 登录视图模型：调 <see cref="IAuthService"/> 校验，成功触发 <see cref="LoginSucceeded"/>。
/// 默认回填上次登录账号密码；<see cref="History"/> 提供近 10 人快速选择（选中即回填账号密码）。
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    /// <summary>
    /// 认证服务。
    /// </summary>
    private readonly IAuthService _auth;

    /// <summary>
    /// 登录历史存储（近 10 人账号密码）。
    /// </summary>
    private readonly ILoginHistoryStore _history;

    /// <summary>
    /// 注入认证服务与登录历史构造，默认回填上次登录账号密码。
    /// </summary>
    /// <param name="auth">认证服务。</param>
    /// <param name="history">登录历史存储。</param>
    public LoginViewModel(IAuthService auth, ILoginHistoryStore history)
    {
        _auth = auth;
        _history = history;
        History = new ObservableCollection<LoginCredential>(history.Recent);
        if (history.Last is { } last)
        {
            _userName = last.UserName;
            _password = last.Password;
        }
    }

    /// <summary>
    /// 登录页版本号显示（取入口程序集装配版本前三位，随升级自动变化）。
    /// </summary>
    public string VersionText
    {
        get
        {
            var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            var ver = v is null ? new Version(1, 0, 0) : new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
            return $"v{ver.ToString(3)} · 康斯特智能";
        }
    }

    /// <summary>
    /// 近 10 人登录历史（最近在前），登录页快速选择用。
    /// </summary>
    public ObservableCollection<LoginCredential> History { get; }

    /// <summary>
    /// 当前选中的历史账号：选中即回填账号密码并同步密码框。
    /// </summary>
    [ObservableProperty] private LoginCredential? _selectedCredential;

    /// <summary>
    /// 密码被程序化改写（历史回填/选择）时触发，供 PasswordBox 手动同步（PasswordBox 不支持绑定）。
    /// </summary>
    public event EventHandler<string>? PasswordFilled;

    /// <summary>
    /// 选中历史账号 → 回填账号密码 + 通知密码框同步。
    /// </summary>
    /// <param name="value">选中的历史凭据。</param>
    partial void OnSelectedCredentialChanged(LoginCredential? value)
    {
        if (value is null)
        {
            return;
        }

        UserName = value.UserName;
        Password = value.Password;
        PasswordFilled?.Invoke(this, value.Password);
    }

    /// <summary>
    /// 用户名（默认 admin，供演示）。
    /// </summary>
    [ObservableProperty] private string _userName = "admin";

    /// <summary>
    /// 密码（默认 123456，供演示）。
    /// </summary>
    [ObservableProperty] private string _password = "123456";

    /// <summary>
    /// 错误提示（登录失败时显示）。
    /// </summary>
    [ObservableProperty] private string? _error;

    /// <summary>
    /// 是否登录中（禁用按钮/转圈）。
    /// </summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// 是否已登录成功。
    /// </summary>
    public bool Succeeded { get; private set; }

    /// <summary>
    /// 登录用户显示名（真实姓名或登录名）。
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// 是否测试账号（admin）登录：数据只落本地不上传云端。
    /// </summary>
    public bool IsTestAccount { get; private set; }

    /// <summary>
    /// 登录成功时触发，宿主据此关闭登录窗。
    /// </summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>
    /// 执行登录：校验用户名/密码，成功置状态并触发事件，失败置错误提示。
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            var result = await _auth.AuthenticateAsync(UserName, Password);
            if (result.Success)
            {
                Succeeded = true;
                DisplayName = result.DisplayName;
                IsTestAccount = result.IsTestAccount;
                _history.Record(UserName, Password);
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Error = result.Error ?? "登录失败";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
