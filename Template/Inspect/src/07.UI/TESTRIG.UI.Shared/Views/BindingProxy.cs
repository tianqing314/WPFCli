using System.Windows;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 让 DataGridColumn 等不在可视树中的元素能通过绑定访问 DataContext。
/// 用法：在 DataGrid.Resources 放一个 &lt;views:BindingProxy Data="{Binding}"/&gt;，
/// 列绑定写 {Binding Data.Xxx, Source={StaticResource proxy}}。
/// </summary>
public class BindingProxy : Freezable
{
    /// <summary>
    /// 承载的数据（通常为外层 DataContext）。
    /// </summary>
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));

    /// <summary>
    /// 创建实例（Freezable 要求）。
    /// </summary>
    /// <returns>新实例。</returns>
    protected override Freezable CreateInstanceCore()
    {
        return new BindingProxy();
    }

    /// <summary>
    /// 承载的数据（通常为外层 DataContext）。
    /// </summary>
    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
