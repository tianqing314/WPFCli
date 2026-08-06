using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 轻量 Toast（对应设计稿 08 ③）：活动窗口右上角浮出、非阻断、自动消失。
/// 白卡 + 左侧 3px 语义色条 + 彩色图标方块 + 标题/副文。
/// </summary>
public static class AppToast
{
    /// <summary>
    /// 当前显示中的 Toast（新 Toast 会顶替旧的）。
    /// </summary>
    private static Popup? _current;

    /// <summary>
    /// 成功/信息类反馈（绿色 ✓）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="detail">副文（可空则不显示）。</param>
    public static void Success(string title, string? detail = null) => Show(title, detail, ok: true);

    /// <summary>
    /// 失败类反馈（红色 ✕）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="detail">副文（可空则不显示）。</param>
    public static void Error(string title, string? detail = null) => Show(title, detail, ok: false);

    /// <summary>
    /// 按资源键取画刷，找不到返回灰色。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <returns>画刷。</returns>
    private static Brush Res(string key)
    {
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    /// <summary>
    /// 组装并显示 Toast：挂在活动窗口右上，入场淡入下滑，停留 2.2s 后淡出关闭。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="detail">副文。</param>
    /// <param name="ok">语义：true 绿 / false 红。</param>
    private static void Show(string title, string? detail, bool ok)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        if (owner is null)
        {
            return;
        }

        // 顶替上一条
        if (_current is not null)
        {
            _current.IsOpen = false;
            _current = null;
        }

        var accent = ok ? Res("Brush.Success") : Res("Brush.Danger");

        // 彩色图标方块
        var iconBox = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(3),
            Background = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new PackIcon
            {
                Kind = ok ? PackIconKind.Check : PackIconKind.Close,
                Width = 15,
                Height = 15,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        // 标题 + 副文
        var text = new StackPanel { Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, Foreground = Res("Brush.Text") });
        if (!string.IsNullOrWhiteSpace(detail))
        {
            text.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = Res("Brush.TextMuted"),
            });
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(iconBox);
        row.Children.Add(text);

        // 左 3px 语义色条 + 白卡
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var bar = new Border { Width = 3, Background = accent };
        var card = new Border
        {
            Background = Res("Brush.Surface"),
            BorderBrush = Res("Brush.BorderStrong"),
            BorderThickness = new Thickness(0, 1, 1, 1),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Padding = new Thickness(16, 12, 16, 12),
            Child = row,
        };
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(card, 1);
        body.Children.Add(bar);
        body.Children.Add(card);

        var slide = new TranslateTransform(0, -10);
        var toast = new Border
        {
            Margin = new Thickness(14),
            Opacity = 0,
            RenderTransform = slide,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = body,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                Direction = 270,
                Opacity = 0.18,
                ShadowDepth = 3,
                Color = Color.FromRgb(0x1E, 0x32, 0x5A),
            },
        };

        // 满宽透明宿主：Toast 靠右；全透明区域可点穿
        var host = new Grid { Width = Math.Max(0, owner.ActualWidth - 20) };
        host.Children.Add(toast);

        var popup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Relative,
            PlacementTarget = owner,
            HorizontalOffset = 10,
            VerticalOffset = 54,
            StaysOpen = true,
            Child = host,
        };
        _current = popup;
        popup.IsOpen = true;

        // 入场：淡入 + 下滑
        toast.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

        // 停留后淡出并关闭
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (_, _) =>
            {
                popup.IsOpen = false;
                if (ReferenceEquals(_current, popup))
                {
                    _current = null;
                }
            };
            toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        timer.Start();
    }
}
