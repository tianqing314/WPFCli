using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 轻量多通道折线图：把 <see cref="ProcessDataSeries"/>（共享时间轴 + 每通道一组值）画成曲线。
/// 实时模式下 Series 随采样点不断替换 → 自动重绘，曲线随之增长。
/// 支持鼠标交互：滚轮缩放（以光标为中心）、左键拖拽平移、悬停查看各点数据值、双击复位。
/// </summary>
public partial class CurveChartView : UserControl
{
    /// <summary>
    /// 各通道折线配色（按索引循环）。
    /// </summary>
    private static readonly Brush[] ChannelBrushes =
        [Brushes.SteelBlue, Brushes.OrangeRed, Brushes.SeaGreen, Brushes.MediumPurple];

    // ===== 视图窗口（X 缩放/平移状态）=====

    /// <summary>数据整体 X 范围（下界）。</summary>
    private double _dataXMin;

    /// <summary>数据整体 X 范围（上界）。</summary>
    private double _dataXMax;

    /// <summary>当前可视 X 窗口（下界）。</summary>
    private double _viewXMin;

    /// <summary>当前可视 X 窗口（上界）。</summary>
    private double _viewXMax;

    /// <summary>用户是否手动缩放/平移过（true 时不随新数据自动复位窗口）。</summary>
    private bool _userView;

    // ===== 最近一次绘制的坐标变换（供悬停反算屏幕坐标）=====

    private double _left, _top, _plotW, _plotH, _yMin, _yMax;

    /// <summary>本次是否已成功绘出图（有有效变换）。</summary>
    private bool _hasPlot;

    // ===== 拖拽平移状态 =====

    /// <summary>是否正在左键拖拽。</summary>
    private bool _dragging;

    /// <summary>拖拽上一帧光标 X（画布坐标）。</summary>
    private double _dragLastX;

    /// <summary>
    /// 构造曲线视图。
    /// </summary>
    public CurveChartView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// <see cref="Series"/> 依赖属性（变化即重绘）。
    /// </summary>
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(ProcessDataSeries), typeof(CurveChartView),
        new PropertyMetadata(null, (d, _) => ((CurveChartView)d).OnSeriesChanged()));

    /// <summary>
    /// 采集数据序列（多通道曲线源）。
    /// </summary>
    public ProcessDataSeries? Series
    {
        get => (ProcessDataSeries?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>
    /// 数据变化：未手动缩放时让可视窗口跟随最新整体范围（实时曲线增长时自动展开），然后重绘。
    /// </summary>
    private void OnSeriesChanged()
    {
        var s = Series;
        if (s is not null && s.TimeSec.Count > 0)
        {
            _dataXMin = s.TimeSec[0];
            _dataXMax = s.TimeSec[^1];
            if (_dataXMax <= _dataXMin)
            {
                _dataXMax = _dataXMin + 1;
            }

            if (!_userView)
            {
                _viewXMin = _dataXMin;
                _viewXMax = _dataXMax;
            }
            else
            {
                ClampView();
            }
        }

        Redraw();
    }

    /// <summary>
    /// 画布尺寸变化 → 重绘。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">尺寸变化参数。</param>
    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    /// <summary>
    /// 把可视窗口约束在数据范围内；窗口不小于数据全宽时贴合全宽。
    /// </summary>
    private void ClampView()
    {
        var full = _dataXMax - _dataXMin;
        var span = _viewXMax - _viewXMin;
        if (span >= full || span <= 0)
        {
            _viewXMin = _dataXMin;
            _viewXMax = _dataXMax;
            return;
        }

        if (_viewXMin < _dataXMin)
        {
            _viewXMin = _dataXMin;
            _viewXMax = _dataXMin + span;
        }

        if (_viewXMax > _dataXMax)
        {
            _viewXMax = _dataXMax;
            _viewXMin = _dataXMax - span;
        }
    }

    /// <summary>
    /// 重绘：清空画布，按可视 X 窗口 + 窗口内 Y 范围画坐标轴/网格/各通道折线与图例。无数据或尺寸过小则显示空提示。
    /// </summary>
    private void Redraw()
    {
        PlotCanvas.Children.Clear();
        OverlayCanvas.Children.Clear();
        _hasPlot = false;
        var s = Series;
        double w = PlotCanvas.ActualWidth, h = PlotCanvas.ActualHeight;

        var hasData = s is not null && s.TimeSec.Count > 0 && s.Channels.Count > 0;
        EmptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        HintText.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        if (!hasData || w < 40 || h < 40)
        {
            return;
        }

        const double left = 46, right = 12, top = 24, bottom = 22;
        var plotW = w - left - right;
        var plotH = h - top - bottom;

        // 可视窗口（未初始化时贴合数据）
        double xMin = _viewXMin, xMax = _viewXMax;
        if (xMax <= xMin)
        {
            xMin = s!.TimeSec[0];
            xMax = s.TimeSec[^1];
            if (xMax <= xMin)
            {
                xMax = xMin + 1;
            }
        }

        // Y 范围只统计可视窗口内的采样点（配合 X 缩放，纵向也自适应）
        double yMin = double.MaxValue, yMax = double.MinValue;
        var n0 = s!.TimeSec.Count;
        for (var i = 0; i < n0; i++)
        {
            var t = s.TimeSec[i];
            if (t < xMin || t > xMax)
            {
                continue;
            }

            foreach (var ch in s.Channels)
            {
                if (i >= ch.Values.Count)
                {
                    continue;
                }

                var v = ch.Values[i];
                if (v < yMin)
                {
                    yMin = v;
                }

                if (v > yMax)
                {
                    yMax = v;
                }
            }
        }

        if (yMin == double.MaxValue)
        {
            // 窗口内无点（极端情况）：退回全体范围
            foreach (var ch in s.Channels)
            {
                foreach (var v in ch.Values)
                {
                    if (v < yMin) { yMin = v; }
                    if (v > yMax) { yMax = v; }
                }
            }
        }

        if (yMax <= yMin) { yMin -= 0.5; yMax += 0.5; }
        var padY = (yMax - yMin) * 0.08;
        yMin -= padY; yMax += padY;

        // 保存变换供悬停使用
        _left = left; _top = top; _plotW = plotW; _plotH = plotH;
        _viewXMin = xMin; _viewXMax = xMax; _yMin = yMin; _yMax = yMax;
        _hasPlot = true;

        double X(double t) => left + (t - xMin) / (xMax - xMin) * plotW;
        double Y(double v) => top + (1 - (v - yMin) / (yMax - yMin)) * plotH;

        // 坐标轴
        AddLine(left, top, left, top + plotH, "#BBB", 1);
        AddLine(left, top + plotH, left + plotW, top + plotH, "#BBB", 1);

        // Y 网格 + 刻度（4 等分）
        for (var i = 0; i <= 4; i++)
        {
            var val = yMin + (yMax - yMin) * i / 4;
            var py = Y(val);
            if (!double.IsNaN(py))
            {
                AddLine(left, py, left + plotW, py, "#F0F0F0", 1);
                AddText(val.ToString("0.###", CultureInfo.InvariantCulture), 4, py - 8, "#888", 10);
            }
        }
        // X 起止刻度
        AddText($"{xMin:0.#}", left, top + plotH + 3, "#888", 10);
        AddText($"{xMax:0.#}", left + plotW - 26, top + plotH + 3, "#888", 10);

        // 各通道折线 + 图例
        double legendX = left + 6;
        for (var c = 0; c < s.Channels.Count; c++)
        {
            var ch = s.Channels[c];
            var brush = ChannelBrushes[c % ChannelBrushes.Length];
            var poly = new Polyline
            {
                Stroke = brush,
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                // 缩放后窗口外的采样点会映射到绘图区左/右侧之外，裁到绘图矩形内，避免压住 Y 轴刻度/溢出
                Clip = new RectangleGeometry(new Rect(left, top, plotW, plotH)),
            };
            var n = Math.Min(ch.Values.Count, s.TimeSec.Count);
            for (var i = 0; i < n; i++)
            {
                poly.Points.Add(new Point(X(s.TimeSec[i]), Y(ch.Values[i])));
            }

            PlotCanvas.Children.Add(poly);

            // 图例
            var swatch = new Rectangle { Width = 12, Height = 3, Fill = brush };
            Canvas.SetLeft(swatch, legendX); Canvas.SetTop(swatch, 10);
            PlotCanvas.Children.Add(swatch);
            AddText($"{ch.Name} ({s.Unit})", legendX + 16, 4, "#555", 11);
            legendX += 16 + (ch.Name.Length * 9) + 40;
        }
    }

    // ===== 鼠标交互 =====

    /// <summary>
    /// 滚轮缩放 X 轴（以光标位置为中心），向上放大、向下缩小，约束在数据范围内。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">滚轮参数。</param>
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hasPlot)
        {
            return;
        }

        var mx = e.GetPosition(PlotCanvas).X;
        // 光标处对应的时间（缩放锚点）
        var anchor = _viewXMin + (Math.Clamp(mx, _left, _left + _plotW) - _left) / _plotW * (_viewXMax - _viewXMin);
        var factor = e.Delta > 0 ? 0.82 : 1 / 0.82;

        var newSpan = (_viewXMax - _viewXMin) * factor;
        var full = _dataXMax - _dataXMin;
        var minSpan = full * 0.02;
        newSpan = Math.Clamp(newSpan, minSpan, full);

        // 保持 anchor 在光标处的相对位置不变
        var frac = (anchor - _viewXMin) / (_viewXMax - _viewXMin);
        _viewXMin = anchor - frac * newSpan;
        _viewXMax = _viewXMin + newSpan;
        _userView = newSpan < full;
        ClampView();
        Redraw();
        e.Handled = true;
    }

    /// <summary>
    /// 左键按下：开始拖拽平移（并捕获鼠标）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标参数。</param>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击复位到数据全宽
            _userView = false;
            _viewXMin = _dataXMin;
            _viewXMax = _dataXMax;
            Redraw();
            return;
        }

        if (!_hasPlot)
        {
            return;
        }

        _dragging = true;
        _dragLastX = e.GetPosition(PlotCanvas).X;
        Root.CaptureMouse();
    }

    /// <summary>
    /// 左键抬起：结束拖拽。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标参数。</param>
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Root.ReleaseMouseCapture();
    }

    /// <summary>
    /// 鼠标移动：拖拽中平移 X 窗口；否则显示悬停十字准线 + 各通道数据点浮层。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标参数。</param>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_hasPlot)
        {
            return;
        }

        var mx = e.GetPosition(PlotCanvas).X;

        if (_dragging)
        {
            var span = _viewXMax - _viewXMin;
            var dt = (mx - _dragLastX) / _plotW * span;
            _dragLastX = mx;
            _viewXMin -= dt;
            _viewXMax -= dt;
            _userView = true;
            ClampView();
            Redraw();
            return;
        }

        ShowHover(e.GetPosition(PlotCanvas));
    }

    /// <summary>
    /// 鼠标离开：清除悬停浮层。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标参数。</param>
    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _dragging = false;
        OverlayCanvas.Children.Clear();
    }

    /// <summary>
    /// 在光标处找最近采样点，画十字准线 + 各通道标记点，并弹出数据值浮层。
    /// </summary>
    /// <param name="p">光标（画布坐标）。</param>
    private void ShowHover(Point p)
    {
        OverlayCanvas.Children.Clear();
        var s = Series;
        if (s is null || s.TimeSec.Count == 0 || p.X < _left || p.X > _left + _plotW || p.Y < _top || p.Y > _top + _plotH)
        {
            return;
        }

        // 光标 X → 时间 → 最近采样点索引（仅在可视窗口内）
        var t = _viewXMin + (p.X - _left) / _plotW * (_viewXMax - _viewXMin);
        var best = -1;
        var bestDist = double.MaxValue;
        for (var i = 0; i < s.TimeSec.Count; i++)
        {
            var tt = s.TimeSec[i];
            if (tt < _viewXMin || tt > _viewXMax)
            {
                continue;
            }

            var d = Math.Abs(tt - t);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        if (best < 0)
        {
            return;
        }

        double X(double v) => _left + (v - _viewXMin) / (_viewXMax - _viewXMin) * _plotW;
        double Y(double v) => _top + (1 - (v - _yMin) / (_yMax - _yMin)) * _plotH;

        var px = X(s.TimeSec[best]);

        // 竖直准线
        OverlayCanvas.Children.Add(new Line
        {
            X1 = px, Y1 = _top, X2 = px, Y2 = _top + _plotH,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#99AACC")!,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 3 },
        });

        // 浮层文本：X 值 + 各通道值
        var lines = new List<(string Text, Brush Brush)>
        {
            ($"x = {s.TimeSec[best]:0.###}", (Brush)new BrushConverter().ConvertFromString("#666")!),
        };
        for (var c = 0; c < s.Channels.Count; c++)
        {
            var ch = s.Channels[c];
            if (best >= ch.Values.Count)
            {
                continue;
            }

            var val = ch.Values[best];
            var brush = ChannelBrushes[c % ChannelBrushes.Length];

            // 标记点
            const double r = 3.5;
            var dot = new Ellipse
            {
                Width = r * 2, Height = r * 2, Fill = brush,
                Stroke = Brushes.White, StrokeThickness = 1,
            };
            Canvas.SetLeft(dot, px - r);
            Canvas.SetTop(dot, Y(val) - r);
            OverlayCanvas.Children.Add(dot);

            lines.Add(($"{ch.Name}: {val.ToString("0.###", CultureInfo.InvariantCulture)} {s.Unit}", brush));
        }

        AddTooltip(lines, px, p.Y);
    }

    /// <summary>
    /// 在光标附近绘制数据值浮层（贴近右边则翻到左侧，避免超出画布）。
    /// </summary>
    /// <param name="lines">文本行（内容 + 颜色）。</param>
    /// <param name="anchorX">锚点 X（准线处）。</param>
    /// <param name="cursorY">光标 Y。</param>
    private void AddTooltip(List<(string Text, Brush Brush)> lines, double anchorX, double cursorY)
    {
        var panel = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };
        foreach (var (text, brush) in lines)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
            });
        }

        var border = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString("#F8FCFFFF")!,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#CCD5E0")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = panel,
        };

        // 先测量再定位
        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tw = border.DesiredSize.Width;
        var th = border.DesiredSize.Height;

        var tx = anchorX + 12;
        if (tx + tw > _left + _plotW)
        {
            tx = anchorX - 12 - tw;
        }

        var ty = Math.Clamp(cursorY - th / 2, _top, _top + _plotH - th);
        Canvas.SetLeft(border, tx);
        Canvas.SetTop(border, ty);
        OverlayCanvas.Children.Add(border);
    }

    /// <summary>
    /// 在画布上加一条线段。
    /// </summary>
    /// <param name="x1">起点 X。</param>
    /// <param name="y1">起点 Y。</param>
    /// <param name="x2">终点 X。</param>
    /// <param name="y2">终点 Y。</param>
    /// <param name="color">颜色（十六进制串）。</param>
    /// <param name="thick">线宽。</param>
    private void AddLine(double x1, double y1, double x2, double y2, string color, double thick)
    {
        PlotCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = (Brush)new BrushConverter().ConvertFromString(color)!,
            StrokeThickness = thick,
        });
    }

    /// <summary>
    /// 在画布上加一段文本。
    /// </summary>
    /// <param name="text">文本。</param>
    /// <param name="x">左位置。</param>
    /// <param name="y">上位置。</param>
    /// <param name="color">颜色（十六进制串）。</param>
    /// <param name="size">字号。</param>
    private void AddText(string text, double x, double y, string color, double size)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = (Brush)new BrushConverter().ConvertFromString(color)!,
        };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
        PlotCanvas.Children.Add(tb);
    }
}
