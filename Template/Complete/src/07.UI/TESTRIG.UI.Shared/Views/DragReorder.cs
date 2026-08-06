using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 列表「拖动重排」附加行为（手柄式）：
/// - 给 ItemsControl 设 <c>DragReorder.Items="{Binding 底层集合}"</c>（接收拖放并重排）。
/// - 给每个项里的拖动手柄元素设 <c>DragReorder.Handle="True"</c>（从手柄按下才发起拖动，
///   避免与项内的 Button 点击 / Expander 展开冲突，也天然解决设备组/板子两级嵌套）。
/// 重排在同一集合内进行（Drop 时校验被拖项属于本 ItemsControl 的集合）。
///
/// 交互增强：拖动时在窗口 AdornerLayer 上浮出「幽灵」预览跟随鼠标（带阴影，长按浮起感），
/// 并在目标位置画一条插入线，落点按行中线判定（上半→前，下半→后），落点直观且稳定。
/// </summary>
public static class DragReorder
{
    /// <summary>
    /// 拖放数据格式标识（进程内自定义）。
    /// </summary>
    private const string Fmt = "TESTRIG.DragItem";

    /// <summary>
    /// 附加属性 Items：给 ItemsControl 绑底层集合，使其接收拖放并重排。
    /// </summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.RegisterAttached(
        "Items", typeof(IList), typeof(DragReorder), new PropertyMetadata(null, OnItemsChanged));

    /// <summary>
    /// 读附加属性 Items。
    /// </summary>
    /// <param name="o">目标对象。</param>
    /// <returns>底层集合。</returns>
    public static IList? GetItems(DependencyObject o)
    {
        return (IList?)o.GetValue(ItemsProperty);
    }

    /// <summary>
    /// 写附加属性 Items。
    /// </summary>
    /// <param name="o">目标对象。</param>
    /// <param name="v">底层集合。</param>
    public static void SetItems(DependencyObject o, IList? v)
    {
        o.SetValue(ItemsProperty, v);
    }

    /// <summary>
    /// Items 变化：为 ItemsControl 挂/摘拖放事件。
    /// </summary>
    /// <param name="d">目标对象。</param>
    /// <param name="e">属性变化参数。</param>
    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic)
        {
            return;
        }

        ic.AllowDrop = true;
        ic.DragOver -= OnDragOver;
        ic.Drop -= OnDrop;
        ic.DragLeave -= OnDragLeave;
        if (e.NewValue != null)
        {
            ic.DragOver += OnDragOver;
            ic.Drop += OnDrop;
            ic.DragLeave += OnDragLeave;
        }
    }

    /// <summary>
    /// 拖动经过：设置放置效果，更新幽灵位置与插入线。
    /// </summary>
    /// <param name="sender">事件源（ItemsControl）。</param>
    /// <param name="e">拖放事件参数。</param>
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var ic = (ItemsControl)sender;
        var items = GetItems(ic);
        var ok = e.Data.GetDataPresent(Fmt) && items != null && items.Contains(e.Data.GetData(Fmt));
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        if (!ok)
        {
            return;
        }

        _ghost?.Update(e.GetPosition(_ghostHost));
        if (_insertion != null && _ghostHost != null)
        {
            var d = Compute(ic, items!, e.Data.GetData(Fmt)!, e.GetPosition(ic), _ghostHost);
            _insertion.Set(d.Y, d.X1, d.X2);
        }
    }

    /// <summary>
    /// 拖动离开：不清插入线（避免行间隙闪烁）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">拖放事件参数。</param>
    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        // 保留插入线，避免行间隙闪烁
    }

    /// <summary>
    /// 放置：按落点计算目标索引，在本集合内重排（同层保护 + 移除后索引修正）。
    /// </summary>
    /// <param name="sender">事件源（ItemsControl）。</param>
    /// <param name="e">拖放事件参数。</param>
    private static void OnDrop(object sender, DragEventArgs e)
    {
        var ic = (ItemsControl)sender;
        var items = GetItems(ic);
        if (items == null || !e.Data.GetDataPresent(Fmt))
        {
            return;
        }

        var dragged = e.Data.GetData(Fmt);
        if (dragged == null || !items.Contains(dragged))
        {
            return;   // 只在本集合内重排（同层保护）
        }

        e.Handled = true;

        var host = _ghostHost ?? ic;
        var to = Compute(ic, items, dragged, e.GetPosition(ic), host).Index;
        var from = items.IndexOf(dragged);
        if (from < 0 || to < 0)
        {
            return;
        }

        if (from < to)
        {
            to--;                 // 移除靠前项后，后方索引整体左移一位
        }

        if (to > items.Count - 1)
        {
            to = items.Count - 1;
        }

        if (from == to)
        {
            return;
        }

        items.RemoveAt(from);
        items.Insert(to, dragged);
    }

    /// <summary>
    /// 计算落点：目标行 + 行内上下半判定 → 插入索引 &amp; 插入线坐标（host 坐标系）。
    /// </summary>
    /// <param name="ic">当前 ItemsControl。</param>
    /// <param name="items">底层集合。</param>
    /// <param name="dragged">被拖项。</param>
    /// <param name="cursorInIc">光标在 ItemsControl 坐标。</param>
    /// <param name="host">幽灵/插入线宿主。</param>
    /// <returns>(插入索引, 线 Y, 线左 X, 线右 X)。</returns>
    private static (int Index, double Y, double X1, double X2) Compute(
        ItemsControl ic, IList items, object dragged, Point cursorInIc, UIElement host)
    {
        var count = items.Count;
        var target = ItemUnder(ic, cursorInIc);
        if (target != null && items.Contains(target))
        {
            var c = ic.ItemContainerGenerator.ContainerFromItem(target) as FrameworkElement;
            if (c != null && Bounds(c, host, out var r))
            {
                var after = cursorInIc.Y > VerticalMid(c, ic);
                var idx = items.IndexOf(target) + (after ? 1 : 0);
                return (idx, after ? r.Bottom : r.Top, r.Left, r.Right);
            }
        }
        // 落在空白/间隙：贴最后一行之后（或第一行之前）
        if (count > 0)
        {
            var last = ic.ItemContainerGenerator.ContainerFromIndex(count - 1) as FrameworkElement;
            var first = ic.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
            if (last != null && Bounds(last, host, out var rl) && cursorInIc.Y >= VerticalMid(last, ic))
            {
                return (count, rl.Bottom, rl.Left, rl.Right);
            }

            if (first != null && Bounds(first, host, out var rf))
            {
                return (0, rf.Top, rf.Left, rf.Right);
            }
        }
        return (count, 0, 0, 0);
    }

    /// <summary>
    /// 元素相对某祖先的垂直中线 Y。
    /// </summary>
    /// <param name="c">元素。</param>
    /// <param name="rel">参照祖先。</param>
    /// <returns>中线 Y。</returns>
    private static double VerticalMid(FrameworkElement c, Visual rel)
    {
        var t = c.TransformToAncestor(rel).TransformBounds(new Rect(c.RenderSize));
        return t.Top + t.Height / 2;
    }

    /// <summary>
    /// 取元素相对宿主的边界矩形（跨树失败返回 false）。
    /// </summary>
    /// <param name="c">元素。</param>
    /// <param name="host">宿主。</param>
    /// <param name="r">边界矩形（输出）。</param>
    /// <returns>是否成功。</returns>
    private static bool Bounds(FrameworkElement c, UIElement host, out Rect r)
    {
        try
        {
            r = c.TransformToAncestor((Visual)host).TransformBounds(new Rect(c.RenderSize));
            return true;
        }
        catch
        {
            r = default;
            return false;
        }
    }

    /// <summary>
    /// 附加属性 Handle：标记某元素为拖动手柄（从它按下并移动才发起拖动）。
    /// </summary>
    public static readonly DependencyProperty HandleProperty = DependencyProperty.RegisterAttached(
        "Handle", typeof(bool), typeof(DragReorder), new PropertyMetadata(false, OnHandleChanged));

    /// <summary>
    /// 读附加属性 Handle。
    /// </summary>
    /// <param name="o">目标对象。</param>
    /// <returns>是否手柄。</returns>
    public static bool GetHandle(DependencyObject o)
    {
        return (bool)o.GetValue(HandleProperty);
    }

    /// <summary>
    /// 写附加属性 Handle。
    /// </summary>
    /// <param name="o">目标对象。</param>
    /// <param name="v">是否手柄。</param>
    public static void SetHandle(DependencyObject o, bool v)
    {
        o.SetValue(HandleProperty, v);
    }

    /// <summary>
    /// 手柄按下时的起始坐标。
    /// </summary>
    private static Point _start;

    /// <summary>
    /// 当前被拖数据项。
    /// </summary>
    private static object? _item;

    /// <summary>
    /// 是否正在拖动。
    /// </summary>
    private static bool _dragging;

    /// <summary>
    /// 幽灵/插入线宿主（最外层 ItemsControl）。
    /// </summary>
    private static UIElement? _ghostHost;

    /// <summary>
    /// 宿主的 AdornerLayer。
    /// </summary>
    private static AdornerLayer? _layer;

    /// <summary>
    /// 拖动幽灵浮层。
    /// </summary>
    private static DragGhostAdorner? _ghost;

    /// <summary>
    /// 插入线。
    /// </summary>
    private static InsertionAdorner? _insertion;

    /// <summary>
    /// Handle 变化：为手柄元素挂/摘按下/移动事件并设手型光标。
    /// </summary>
    /// <param name="d">目标对象。</param>
    /// <param name="e">属性变化参数。</param>
    private static void OnHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        fe.PreviewMouseLeftButtonDown -= OnHandleDown;
        fe.PreviewMouseMove -= OnHandleMove;
        fe.Cursor = null;
        if ((bool)e.NewValue)
        {
            fe.PreviewMouseLeftButtonDown += OnHandleDown;
            fe.PreviewMouseMove += OnHandleMove;
            fe.Cursor = Cursors.SizeAll;
        }
    }

    /// <summary>
    /// 手柄按下：记录起点与被拖项，独占按下防触发外层 Expander/Button。
    /// </summary>
    /// <param name="sender">事件源（手柄元素）。</param>
    /// <param name="e">鼠标事件参数。</param>
    private static void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        _start = e.GetPosition(null);
        _item = fe.DataContext;
        // 手柄独占按下，防止触发外层 Expander/Button
        e.Handled = true;
    }

    /// <summary>
    /// 手柄移动：超过拖动阈值即建幽灵/插入线并启动 DoDragDrop，结束后清理浮层。
    /// </summary>
    /// <param name="sender">事件源（手柄元素）。</param>
    /// <param name="e">鼠标事件参数。</param>
    private static void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_dragging || _item == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var p = e.GetPosition(null);
        if (Math.Abs(p.X - _start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var handle = (FrameworkElement)sender;
        var row = ItemRow(handle, _item);                 // 被拖行的容器（拿它的外观做幽灵）
        _ghostHost = DropHost(handle);                    // 最外层可放置的 ItemsControl（幽灵/插入线宿主）
        _dragging = true;
        try
        {
            if (row != null && _ghostHost != null)
            {
                _layer = AdornerLayer.GetAdornerLayer(_ghostHost);
                if (_layer != null)
                {
                    var grab = e.GetPosition(row);
                    _ghost = new DragGhostAdorner(_ghostHost, row, grab);
                    _insertion = new InsertionAdorner(_ghostHost);
                    _layer.Add(_ghost);
                    _layer.Add(_insertion);
                }
            }
            DragDrop.DoDragDrop(handle, new DataObject(Fmt, _item), DragDropEffects.Move);
        }
        finally
        {
            if (_layer != null)
            {
                if (_ghost != null)
                {
                    _layer.Remove(_ghost);
                }

                if (_insertion != null)
                {
                    _layer.Remove(_insertion);
                }
            }
            _ghost = null; _insertion = null; _layer = null; _ghostHost = null;
            _dragging = false; _item = null;
        }
    }

    /// <summary>
    /// 命中：cursorInIc 坐标下、属于本 ItemsControl 集合的数据项。
    /// </summary>
    /// <param name="ic">当前 ItemsControl。</param>
    /// <param name="cursorInIc">光标坐标。</param>
    /// <returns>命中的数据项，或 null。</returns>
    private static object? ItemUnder(ItemsControl ic, Point cursorInIc)
    {
        var hit = ic.InputHitTest(cursorInIc) as DependencyObject;
        while (hit != null && hit != ic)
        {
            if (hit is FrameworkElement fe && fe.DataContext is { } dc && ic.Items.Contains(dc))
            {
                return dc;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    /// <summary>
    /// 从手柄向上找承载被拖数据项的行容器（DataContext == item 的最外层元素）。
    /// </summary>
    /// <param name="src">起始元素（手柄）。</param>
    /// <param name="item">被拖数据项。</param>
    /// <returns>行容器，或 null。</returns>
    private static FrameworkElement? ItemRow(DependencyObject? src, object item)
    {
        FrameworkElement? row = null;
        while (src != null)
        {
            if (src is FrameworkElement fe && ReferenceEquals(fe.DataContext, item))
            {
                row = fe;
            }

            src = VisualTreeHelper.GetParent(src);
        }
        return row;
    }

    /// <summary>
    /// 找最外层设了 DragReorder.Items 的 ItemsControl（幽灵/插入线的宿主，覆盖整个列表区）。
    /// </summary>
    /// <param name="src">起始元素。</param>
    /// <returns>宿主元素，或 null。</returns>
    private static UIElement? DropHost(DependencyObject? src)
    {
        UIElement? host = null;
        while (src != null)
        {
            if (src is ItemsControl ic && GetItems(ic) != null)
            {
                host = ic;
            }

            src = VisualTreeHelper.GetParent(src);
        }
        return host;
    }
}

/// <summary>
/// 拖动幽灵：把被拖行渲染成半透明带阴影的浮层，跟随鼠标。
/// </summary>
internal sealed class DragGhostAdorner : Adorner
{
    /// <summary>
    /// 承载被拖行外观的矩形子元素。
    /// </summary>
    private readonly Rectangle _child;

    /// <summary>
    /// 抓取点（相对被拖行的偏移，使浮层贴合光标）。
    /// </summary>
    private readonly Point _grab;

    /// <summary>
    /// 浮层平移变换。
    /// </summary>
    private readonly TranslateTransform _xform = new();

    /// <summary>
    /// 当前光标位置（宿主坐标）。
    /// </summary>
    private Point _pos;

    /// <summary>
    /// 用被拖行外观构造幽灵浮层。
    /// </summary>
    /// <param name="host">宿主元素。</param>
    /// <param name="source">被拖行。</param>
    /// <param name="grab">抓取偏移。</param>
    public DragGhostAdorner(UIElement host, FrameworkElement source, Point grab) : base(host)
    {
        _grab = grab;
        IsHitTestVisible = false;
        _child = new Rectangle
        {
            Width = source.RenderSize.Width,
            Height = source.RenderSize.Height,
            Fill = new VisualBrush(source) { Opacity = 0.9 },
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.45, Color = Colors.Black },
            Opacity = 0.85,
            RenderTransform = _xform,
        };
        AddVisualChild(_child);
    }

    /// <summary>
    /// 更新浮层位置（跟随光标，减抓取偏移）。
    /// </summary>
    /// <param name="pos">光标位置（宿主坐标）。</param>
    public void Update(Point pos)
    {
        _pos = pos;
        _xform.X = _pos.X - _grab.X;
        _xform.Y = _pos.Y - _grab.Y;
    }

    /// <summary>
    /// 可视子元素数（恒 1）。
    /// </summary>
    protected override int VisualChildrenCount => 1;

    /// <summary>
    /// 取可视子元素。
    /// </summary>
    /// <param name="index">索引。</param>
    /// <returns>子元素。</returns>
    protected override Visual GetVisualChild(int index)
    {
        return _child;
    }

    /// <summary>
    /// 测量子元素。
    /// </summary>
    /// <param name="c">约束尺寸。</param>
    /// <returns>期望尺寸。</returns>
    protected override Size MeasureOverride(Size c)
    {
        _child.Measure(c);
        return _child.DesiredSize;
    }

    /// <summary>
    /// 排列子元素。
    /// </summary>
    /// <param name="f">终尺寸。</param>
    /// <returns>使用尺寸。</returns>
    protected override Size ArrangeOverride(Size f)
    {
        _child.Arrange(new Rect(_child.DesiredSize));
        return f;
    }
}

/// <summary>
/// 插入线：在落点画一条高亮横线 + 两端小圆点，指示将要落到的位置。
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    /// <summary>
    /// 横线画笔（高亮蓝）。
    /// </summary>
    private static readonly Pen Line;

    /// <summary>
    /// 端点圆点画刷（高亮蓝）。
    /// </summary>
    private static readonly Brush Dot;

    /// <summary>
    /// 线 Y、线左 X、线右 X。
    /// </summary>
    private double _y, _x1, _x2;

    /// <summary>
    /// 是否显示（无有效坐标时不画）。
    /// </summary>
    private bool _show;

    /// <summary>
    /// 初始化并冻结画笔/画刷。
    /// </summary>
    static InsertionAdorner()
    {
        // 高亮蓝
        var c = Color.FromRgb(0x1E, 0x88, 0xE5);
        Dot = new SolidColorBrush(c);
        Dot.Freeze();
        Line = new Pen(new SolidColorBrush(c), 2.5);
        Line.Freeze();
    }

    /// <summary>
    /// 构造插入线（不参与命中测试）。
    /// </summary>
    /// <param name="host">宿主元素。</param>
    public InsertionAdorner(UIElement host) : base(host)
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// 设置插入线坐标并触发重绘。
    /// </summary>
    /// <param name="y">线 Y。</param>
    /// <param name="x1">线左 X。</param>
    /// <param name="x2">线右 X。</param>
    public void Set(double y, double x1, double x2)
    {
        _y = y;
        _x1 = x1;
        _x2 = x2;
        _show = x2 > x1;
        InvalidateVisual();
    }

    /// <summary>
    /// 绘制横线 + 两端圆点。
    /// </summary>
    /// <param name="dc">绘制上下文。</param>
    protected override void OnRender(DrawingContext dc)
    {
        if (!_show)
        {
            return;
        }

        dc.DrawLine(Line, new Point(_x1, _y), new Point(_x2, _y));
        dc.DrawEllipse(Dot, null, new Point(_x1, _y), 3.5, 3.5);
        dc.DrawEllipse(Dot, null, new Point(_x2, _y), 3.5, 3.5);
    }
}
