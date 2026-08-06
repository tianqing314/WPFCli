using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TESTRIG.Automation;
using TESTRIG.Core.Engine;
using TESTRIG.Devices;
using TESTRIG.Infrastructure;
using TESTRIG.Infrastructure.Auth;
using TESTRIG.Jigs;
using TESTRIG.TestSteps;
using TESTRIG.UI.Shared.BoardExtras;
using TESTRIG.UI.Shared.Services;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views;
using Serilog;

namespace TESTRIG.App;

/// <summary>
/// 应用入口：构建 DI 主机、注册各层服务，走登录 → 主窗流程，并支持跳过登录/自动化冒烟。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 通用主机（承载 DI 容器与配置）。
    /// </summary>
    private IHost? _host;

    /// <summary>
    /// 启动：挂全局异常日志，执行引导；引导失败记录并重抛。
    /// </summary>
    /// <param name="e">启动参数。</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WPF 默认用 en-US 渲染 DatePicker 日历/TimePicker（与操作系统区域无关），导致年月日显示英文。
        // 全局覆盖 Language 为 zh-CN，令日历弹窗的月份/星期表头按中文渲染。
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN")));

        // DatePicker 文本框的显示格式取自 CurrentCulture（非 Language DP）。用定制 zh-CN 把长日期
        // 模式改成含星期（默认 zh-CN 长日期为「yyyy年M月d日」不带星期），配合控件 SelectedDateFormat=Long，
        // 即显示为「2026年7月12日 星期日」。项目全局无其它长日期用法，改此模式不会误伤别处。
        var zh = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("zh-CN").Clone();
        zh.DateTimeFormat.LongDatePattern = "yyyy'年'M'月'd'日' dddd";
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = zh;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = zh;
        System.Threading.Thread.CurrentThread.CurrentCulture = zh;
        System.Threading.Thread.CurrentThread.CurrentUICulture = zh;

        DispatcherUnhandledException += (_, args) => { LogCrash(args.Exception); args.Handled = false; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        // 现场诊断：设 TESTRIG_BINDING_TRACE=1 时把 WPF 数据绑定错误落到 logs/binding-trace.log
        // （.NET 8 不再从 app.config 读 <system.diagnostics>，只能代码注册；未设环境变量时零开销）。
        if (Environment.GetEnvironmentVariable("TESTRIG_BINDING_TRACE") == "1")
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
            System.IO.Directory.CreateDirectory(dir);
            var listener = new System.Diagnostics.TextWriterTraceListener(System.IO.Path.Combine(dir, "binding-trace.log"));
            listener.WriteLine($"[binding-trace started {DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            listener.Flush();
            System.Diagnostics.PresentationTraceSources.Refresh();
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;
            System.Diagnostics.Trace.AutoFlush = true;
        }

        try
        {
            Bootstrap();
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    /// <summary>
    /// 把崩溃异常追加写入 startup_error.txt（写盘失败则忽略）。
    /// </summary>
    /// <param name="ex">异常。</param>
    private static void LogCrash(Exception? ex)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "startup_error.txt"),
                DateTime.Now + Environment.NewLine + (ex?.ToString() ?? "(null)") + Environment.NewLine + new string('=', 60) + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>
    /// 初始化 Serilog：过程/异常日志按天滚动落盘到应用目录下 logs/（同时输出控制台）。
    /// </summary>
    private static void InitLogging()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDir, "pcba-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                encoding: System.Text.Encoding.UTF8,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console()
            .CreateLogger();
    }

    /// <summary>
    /// 引导：构建主机并注册各层服务，建库，按需跳过登录，显示主窗，并触发自动化冒烟。
    /// </summary>
    private void Bootstrap()
    {
        InitLogging();
        Log.Information("=== 应用启动 ===");
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                services.AddPcbaInfrastructure(ctx.Configuration);
                services.AddTestEngine();

                // 硬件选项：真机开关 + 环境温度服务地址（子设备端点在 connections.json）
                var hw = new TESTRIG.Devices.HardwareOptions
                {
                    UseReal = ctx.Configuration.GetValue("Pcba:Hardware:UseReal", false),
                    EnvTempBaseUrl = ctx.Configuration["Pcba:EnvTemp:BaseUrl"] ?? "http://192.168.0.130:5050",
                    VersionVerifyBaseUrl = ctx.Configuration["Pcba:VersionVerify:BaseUrl"] ?? "http://192.168.0.134:10001",
                };

                // 共享标准盒/PLC + 被检驱动注册表
                services.AddPcbaDevices(hw);
                services.AddSingleton(hw);
                if (hw.UseReal || Environment.GetEnvironmentVariable("TESTRIG_REAL_HARDWARE") == "1")
                {
                    services.AddSingleton<TESTRIG.Devices.Abstractions.IBleScanner, TESTRIG.Devices.BleWin.Win10BleScanner>();
                }

                // 所有测试项处理器
                services.AddPcbaTestSteps();

                // PLC 自动化编排
                services.AddPcbaAutomation();

                // 针床目录（扫描 Manifests）
                services.AddPcbaJigs();

                // 板卡专属工具栏扩展（如 PS02/A20 的烧录板类型来源）
                services.AddPcbaBoardExtras();

                services.AddSingleton<TESTRIG.UI.Shared.Services.UpdateService>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginWindow>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Services.EnsurePcbaDatabase();

        // GUI 下放慢过程数据采集节拍，便于双击测试项观察实时曲线绘制（头测/CI 保持 0 快速跑）
        TESTRIG.TestSteps.ProcessDataSimulator.StreamIntervalMs = 60;

        // 登录窗关闭到主窗显示之间会出现"零窗口"瞬间；默认 OnLastWindowClose 会在此刻退出整个程序。
        // 故先显式关闭、待主窗显示后再切回 OnMainWindowClose。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 开发/自动化验证用：设 TESTRIG_SKIP_LOGIN=1 可跳过登录直达主界面。
        var skipLogin = Environment.GetEnvironmentVariable("TESTRIG_SKIP_LOGIN") == "1";
        string userName = "dev";

        // 跳过登录属开发/测试场景，视为测试账号（数据只落本地不上传云端）
        var isTestAccount = true;
        if (!skipLogin)
        {
            var login = _host.Services.GetRequiredService<LoginWindow>();
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            var loginVm = login.DataContext as LoginViewModel;
            userName = loginVm?.DisplayName ?? "";
            isTestAccount = loginVm?.IsTestAccount ?? false;

            // 测试账号（admin）数据只落本地不上传远程库，进入前须确认
            if (isTestAccount && !AppDialog.Confirm("提示", "当前为测试账号 admin，无法保存数据到远程库，测试数据只落本地。是否确认进入系统？"))
            {
                Shutdown();
                return;
            }
        }

        // 强制更新拦截：登录后、进主界面前。数据库结构/通讯协议不兼容的版本必须先升级，
        // 否则旧客户端会往产线写坏数据。CI 冒烟（TESTRIG_AUTORUN_TASK）不联网，跳过。
        var update = _host.Services.GetRequiredService<UpdateService>();
        update.Operator = userName;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TESTRIG_AUTORUN_TASK"))
            && BlockedByMandatoryUpdate(update))
        {
            Shutdown();
            return;
        }

        var mainVm = _host.Services.GetRequiredService<MainViewModel>();
        mainVm.UserName = userName;
        mainVm.IsAdmin = isTestAccount;

        // 落库 operator + 测试账号标志（测试账号数据只落本地不上传云端）
        var session = _host.Services.GetRequiredService<IUserSession>();
        session.Operator = userName;
        session.IsTestAccount = isTestAccount;

        var main = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = main;

        // 合并菜单「注销登录」：隐藏主窗回登录页，登录成功换身份返回，取消则退出程序
        mainVm.LogoutRequested += () => HandleLogout(mainVm, main);
        main.Show();

        // 主窗已显示，恢复正常退出策略
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // CI/自动化冒烟：TESTRIG_AUTORUN_TASK=<板Key> 时自动打开并运行该板，跑完可选自动退出。
        var autorunKey = Environment.GetEnvironmentVariable("TESTRIG_AUTORUN_TASK");
        if (!string.IsNullOrEmpty(autorunKey))
        {
            _ = Dispatcher.InvokeAsync(() => AutoRunAsync(mainVm, autorunKey));
        }
    }

    /// <summary>
    /// 强更拦截：检查到强制更新则弹出不可关闭的更新对话框，不升级就不放行进主界面。
    /// </summary>
    /// <param name="update">升级服务。</param>
    /// <returns>true = 应当阻止进入主界面。</returns>
    private static bool BlockedByMandatoryUpdate(UpdateService update)
    {
        // 检查扔到线程池并限时：升级服务器不通或慢时不能把启动卡死在这里
        var probe = Task.Run(async () => await update.CheckAsync() && update.IsMandatory);
        if (!probe.Wait(TimeSpan.FromSeconds(8)) || !probe.Result)
        {
            return false;
        }

        // 强更对话框关不掉（✕ 与「暂不」都隐藏），此处主窗尚未显示故无 Owner；
        // 此刻不可能有板卡在测试，busy 恒为 false。
        var vm = new UpdateViewModel(update, () => false);
        new UpdateWindow(vm).ShowDialog();

        // 能走到这行说明对话框已收场：装了的话升级器已接管、本进程该退出；
        // 没装成也一律不放行——强更的意义就在于旧版本不许继续用。
        return true;
    }

    /// <summary>
    /// 注销：隐藏主窗回登录页循环。登录成功 → 更新会话与主壳用户并重新显示主窗；
    /// 取消/关闭登录页 → 视为退出程序（跳过主窗关闭确认）。
    /// </summary>
    /// <param name="mainVm">主壳视图模型。</param>
    /// <param name="main">主窗口。</param>
    private void HandleLogout(MainViewModel mainVm, MainWindow main)
    {
        // 登录窗独立存在期间不能因“无主窗可见”而退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        main.Hide();

        while (true)
        {
            var login = _host!.Services.GetRequiredService<LoginWindow>();
            if (login.ShowDialog() != true)
            {
                main.CloseWithoutConfirm();
                Shutdown();
                return;
            }

            var loginVm = login.DataContext as LoginViewModel;
            var userName = loginVm?.DisplayName ?? "";
            var isTestAccount = loginVm?.IsTestAccount ?? false;

            // 测试账号（admin）数据只落本地不上传远程库，进入前须确认；不确认则回登录页重选
            if (isTestAccount && !AppDialog.Confirm("提示", "当前为测试账号 admin，无法保存数据到远程库，测试数据只落本地。是否确认进入系统？"))
            {
                continue;
            }

            mainVm.UpdateUser(userName, isTestAccount);
            var session = _host.Services.GetRequiredService<IUserSession>();
            session.Operator = userName;
            session.IsTestAccount = isTestAccount;

            main.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            return;
        }
    }

    /// <summary>
    /// 自动化冒烟：打开指定板，连接标准盒并手动单次运行，写结果文件，按需退出。
    /// </summary>
    /// <param name="mainVm">主壳视图模型。</param>
    /// <param name="boardKey">板 Key。</param>
    private async Task AutoRunAsync(MainViewModel mainVm, string boardKey)
    {
        var item = mainVm.Devices.SelectMany(d => d.Boards).FirstOrDefault(b => b.Key == boardKey);
        await mainVm.OpenBoardCommand.ExecuteAsync(item);
        if (mainVm.CurrentContent is TestRunViewModel vm)
        {
            // 满足开始前校验：连接标准盒 + 填批次号
            await _host!.Services.GetRequiredService<ConnectionManager>().ConnectBoxAsync();
            vm.BatchNumber = "AUTORUN";

            // 手动单次，不接 PLC
            vm.AutoMode = false;
            await vm.StartStopCommand.ExecuteAsync(null);
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "autorun_result.txt"),
                    $"{boardKey} => 通过:{vm.PassedCount} 失败:{vm.FailedCount}");
            }
            catch { }
        }
        if (Environment.GetEnvironmentVariable("TESTRIG_EXIT_AFTER") == "1")
        {
            Shutdown();
        }
    }

    /// <summary>
    /// 退出：释放主机资源。
    /// </summary>
    /// <param name="e">退出参数。</param>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== 应用退出 ===");
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
