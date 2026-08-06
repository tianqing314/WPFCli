using System;
using System.Globalization;
using System.Windows.Data;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// [进度(0~1), 轨道宽] → 填充条宽度（自绘硬朗风进度条用）。
/// </summary>
public sealed class ProgressToWidthConverter : IMultiValueConverter
{
    /// <summary>
    /// 进度乘以轨道宽。
    /// </summary>
    /// <param name="values">进度、轨道 ActualWidth。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">未用。</param>
    /// <param name="culture">区域。</param>
    /// <returns>填充宽。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double p && values[1] is double w && w > 0)
        {
            return Math.Max(0, Math.Min(1, p)) * w;
        }

        return 0d;
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
