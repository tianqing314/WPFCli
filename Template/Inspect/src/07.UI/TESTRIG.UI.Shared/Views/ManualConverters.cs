using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 两个绑定值引用相等 → true（手册目录高亮当前选中篇）。
/// </summary>
public sealed class SameRefToTrueConverter : IMultiValueConverter
{
    /// <summary>
    /// 判断两值是否同一引用。
    /// </summary>
    /// <param name="values">两个绑定值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">未用。</param>
    /// <param name="culture">区域。</param>
    /// <returns>是否相等。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.Length == 2 && ReferenceEquals(values[0], values[1]);
    }

    /// <summary>
    /// 不支持回转。
    /// </summary>
    /// <param name="value">值。</param>
    /// <param name="targetTypes">目标类型。</param>
    /// <param name="parameter">未用。</param>
    /// <param name="culture">区域。</param>
    /// <returns>不支持。</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 计数 &gt; 0 → Visible，否则 Collapsed（锚点区块有则显示）。
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 计数转可见性。
    /// </summary>
    /// <param name="value">计数。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">未用。</param>
    /// <param name="culture">区域。</param>
    /// <returns>可见性。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 不支持回转。
    /// </summary>
    /// <param name="value">值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">未用。</param>
    /// <param name="culture">区域。</param>
    /// <returns>不支持。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
