using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using MaterialDesignThemes.Wpf;

namespace TESTRIG.UI.Shared.Views.Chrome;

/// <summary>
/// 无边框现代窗口基类：浅蓝自绘标题栏 + 圆角 + 阴影 + 最小/最大/关闭 + 拖动。
/// 外观由 DesignSystem.xaml 中 ChromeWindow 的隐式 ControlTemplate 提供。
/// </summary>
public class ChromeWindow : Window
{
    /// <summary>
    /// <see cref="CanMaximize"/> 依赖属性。
    /// </summary>
    public static readonly DependencyProperty CanMaximizeProperty =
        DependencyProperty.Register(nameof(CanMaximize), typeof(bool), typeof(ChromeWindow), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="TitleIcon"/> 依赖属性。
    /// </summary>
    public static readonly DependencyProperty TitleIconProperty =
        DependencyProperty.Register(nameof(TitleIcon), typeof(PackIconKind), typeof(ChromeWindow),
            new PropertyMetadata(PackIconKind.Application));

    /// <summary>
    /// <see cref="TitleBarContent"/> 依赖属性。
    /// </summary>
    public static readonly DependencyProperty TitleBarContentProperty =
        DependencyProperty.Register(nameof(TitleBarContent), typeof(object), typeof(ChromeWindow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>
    /// <see cref="ShowCaptionButtons"/> 依赖属性。
    /// </summary>
    public static readonly DependencyProperty ShowCaptionButtonsProperty =
        DependencyProperty.Register(nameof(ShowCaptionButtons), typeof(bool), typeof(ChromeWindow), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="TitleBarBrush"/> 依赖属性。
    /// </summary>
    public static readonly DependencyProperty TitleBarBrushProperty =
        DependencyProperty.Register(nameof(TitleBarBrush), typeof(System.Windows.Media.Brush), typeof(ChromeWindow), new PropertyMetadata(null));

    /// <summary>
    /// 覆盖默认样式键，套用 DesignSystem.xaml 的隐式模板。
    /// </summary>
    static ChromeWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeWindow), new FrameworkPropertyMetadata(typeof(ChromeWindow)));
    }

    /// <summary>
    /// 构造无边框窗口：设 WindowChrome、标题栏命令绑定、最大化不盖任务栏钩子、应用图标。
    /// </summary>
    public ChromeWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;

        var chrome = new WindowChrome
        {
            // 顶部 40px 由系统当作标题栏可拖动
            CaptionHeight = 40,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        };
        WindowChrome.SetWindowChrome(this, chrome);

        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this), (_, e) => e.CanExecute = CanMaximize));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this), (_, e) => e.CanExecute = CanMaximize));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));

        SourceInitialized += (_, _) =>
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowProc);

        // 应用图标（任务栏 / Alt-Tab / 窗口）
        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/TESTRIG.App;component/Resources/app.ico", UriKind.Absolute));
        }
        catch
        {
            // 设计时或资源缺失时忽略
        }
    }

    /// <summary>
    /// 是否允许最大化（弹窗设 false 即隐藏最大化按钮、固定大小）。
    /// </summary>
    public bool CanMaximize
    {
        get => (bool)GetValue(CanMaximizeProperty);
        set => SetValue(CanMaximizeProperty, value);
    }

    /// <summary>
    /// 标题栏左侧图标。
    /// </summary>
    public PackIconKind TitleIcon
    {
        get => (PackIconKind)GetValue(TitleIconProperty);
        set => SetValue(TitleIconProperty, value);
    }

    /// <summary>
    /// 标题栏右侧自定义内容（主窗放合并菜单头像触发器；子窗留空）。置于原生控制按钮之外的最右侧。
    /// </summary>
    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    /// <summary>
    /// 是否显示原生标题栏控制按钮（最小/最大/关闭）。主窗设 false，控制入口改由头像下拉承载。
    /// </summary>
    public bool ShowCaptionButtons
    {
        get => (bool)GetValue(ShowCaptionButtonsProperty);
        set => SetValue(ShowCaptionButtonsProperty, value);
    }

    /// <summary>
    /// 标题栏背景（默认品牌蓝；对话框可按语义改为红/琥珀等）。
    /// </summary>
    public System.Windows.Media.Brush? TitleBarBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(TitleBarBrushProperty);
        set => SetValue(TitleBarBrushProperty, value);
    }

    /// <summary>
    /// 窗口消息钩子：处理 WM_GETMINMAXINFO，最大化时限制到显示器工作区（不盖任务栏）。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="msg">消息。</param>
    /// <param name="wParam">wParam。</param>
    /// <param name="lParam">lParam。</param>
    /// <param name="handled">是否已处理。</param>
    /// <returns>消息结果。</returns>
    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO)
        {
            return IntPtr.Zero;
        }

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, 0x00000002 /* MONITOR_DEFAULTTONEAREST */);
        if (monitor != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref mi);
            mmi.ptMaxPosition.x = Math.Abs(mi.rcWork.left - mi.rcMonitor.left);
            mmi.ptMaxPosition.y = Math.Abs(mi.rcWork.top - mi.rcMonitor.top);
            mmi.ptMaxSize.x = Math.Abs(mi.rcWork.right - mi.rcWork.left);
            mmi.ptMaxSize.y = Math.Abs(mi.rcWork.bottom - mi.rcWork.top);
            mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
            mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
        }
        Marshal.StructureToPtr(mmi, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>
    /// Win32：取窗口所在显示器句柄。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="flags">标志。</param>
    /// <returns>显示器句柄。</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    /// <summary>
    /// Win32：取显示器信息（含工作区）。
    /// </summary>
    /// <param name="hMonitor">显示器句柄。</param>
    /// <param name="lpmi">显示器信息（输出）。</param>
    /// <returns>是否成功。</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Win32 POINT（x/y 坐标）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        /// <summary>X 坐标。</summary>
        public int x;

        /// <summary>Y 坐标。</summary>
        public int y;
    }

    /// <summary>
    /// Win32 RECT（左/上/右/下）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        /// <summary>左/上/右/下边界。</summary>
        public int left, top, right, bottom;
    }

    /// <summary>
    /// Win32 MINMAXINFO（最大化/最小/最大跟踪尺寸）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        /// <summary>保留、最大尺寸、最大位置、最小跟踪尺寸、最大跟踪尺寸。</summary>
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    /// <summary>
    /// Win32 MONITORINFO（显示器区域与工作区）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        /// <summary>结构大小。</summary>
        public int cbSize;

        /// <summary>显示器整体区域、工作区。</summary>
        public RECT rcMonitor, rcWork;

        /// <summary>标志位。</summary>
        public uint dwFlags;
    }
}
