using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;
using TESTRIG.UI.Shared.Services;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 主窗口。承载设备/板子导航与右侧测试内容区。
/// </summary>
public partial class MainWindow : ChromeWindow
{
    /// <summary>
    /// 注入主 ViewModel 构造。
    /// </summary>
    /// <param name="vm">主 ViewModel。</param>
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 预热：主窗空闲时离屏装载一次带行的 DataGrid，把首次打开板子时
        // DataGrid 行/列/单元格生成的 JIT 成本提前吃掉（不实例化 TestRunView——它含
        // RelativeSource=ChromeWindow 的绑定，离屏无宿主窗会产生找不到源的绑定错误）。
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(WarmUpDataGrid));
    }

    /// <summary>
    /// 离屏实例化并测量一个带行 DataGrid，触发行/列/单元格模板的 JIT 预热（结果直接丢弃）。
    /// </summary>
    private static void WarmUpDataGrid()
    {
        var size = new Size(1400, 900);
        var grid = new System.Windows.Controls.DataGrid
        {
            AutoGenerateColumns = true,
            ItemsSource = new[] { new { A = "预热", B = 1 }, new { A = "预热", B = 2 } },
        };
        grid.Measure(size);
        grid.Arrange(new Rect(size));
    }

    /// <summary>
    /// 跳过关闭确认（注销后取消登录的程序退出路径使用）。
    /// </summary>
    private bool _suppressCloseConfirm;

    /// <summary>
    /// 不弹确认框直接关闭主窗（注销流程内部使用，避免退出时再问一次）。
    /// </summary>
    public void CloseWithoutConfirm()
    {
        _suppressCloseConfirm = true;
        Close();
    }

    /// <summary>
    /// 关闭主窗口前弹确认框（防误触标题栏关闭 / Alt+F4 意外退出）；用户取消则不退出。
    /// 自动化/冒烟运行（设了 <c>TESTRIG_AUTORUN_TASK</c>）与注销退出路径跳过确认。
    /// 确认退出后执行当前板的整体测试后。
    /// </summary>
    /// <param name="e">关闭事件参数。</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _suppressCloseConfirm)
        {
            return;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TESTRIG_AUTORUN_TASK")))
        {
            return;
        }

        if (!AppDialog.Confirm("退出确认", "确认退出 TESTRIG 整机测试平台吗？"))
        {
            e.Cancel = true;
            return;
        }

        // 用户确认退出后，异步执行当前板的整体测试后
        _ = RunPostTestForActiveBoardAsync();
    }

    /// <summary>
    /// 异步执行当前活动板的整体测试后（关闭前收尾）。
    /// </summary>
    private async Task RunPostTestForActiveBoardAsync()
    {
        try
        {
            if (DataContext is MainViewModel vm && vm.CurrentContent is TestRunViewModel testVm)
            {
                await testVm.RunPostTestAsync();
            }
        }
        catch
        {
            // 关闭阶段不抛异常
        }
    }

    /// <summary>
    /// 合并菜单：注销登录。测试运行中禁止注销；确认后交由 App 层回登录页。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void OnLogout(object sender, RoutedEventArgs e)
    {
        MenuTrigger.IsChecked = false;
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.AnyBoardBusy)
        {
            AppDialog.Error("无法注销", "有板卡正在测试中，请先停止测试再注销。");
            return;
        }

        if (AppDialog.Confirm("注销确认", $"确认注销当前账号 {vm.UserName} 并返回登录页吗？"))
        {
            vm.RequestLogout();
        }
    }

    /// <summary>
    /// 合并菜单：点击功能项后收起下拉浮层。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void CloseMenu(object sender, RoutedEventArgs e) => MenuTrigger.IsChecked = false;

    /// <summary>
    /// 合并菜单窗口控制：最小化。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        MenuTrigger.IsChecked = false;
        SystemCommands.MinimizeWindow(this);
    }

    /// <summary>
    /// 合并菜单窗口控制：最大化 ↔ 还原。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        MenuTrigger.IsChecked = false;
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    /// <summary>
    /// 合并菜单窗口控制：退出（走关闭确认）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void OnExit(object sender, RoutedEventArgs e)
    {
        MenuTrigger.IsChecked = false;
        Close();
    }
}
