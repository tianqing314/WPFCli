using System;
using System.Globalization;
using System.Windows.Data;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 号位卡片宽度换算：<c>视口宽 / min(号位数, 4)</c>。
/// 号位 ≤4 时按数量均分铺满视口；&gt;4 时每卡固定为「4 号位宽度」，总宽超出视口 → 底部横向滚动。
/// 入参 values[0]=ScrollViewer.ViewportWidth（double），values[1]=号位数（int）。
/// </summary>
public sealed class PositionCardWidthConverter : IMultiValueConverter
{
    /// <summary>
    /// 每卡左右外边距合计（与 XAML 卡片 Margin="5" 对应），从均分宽里扣除避免恰好溢出。
    /// </summary>
    private const double CardMargin = 10;

    /// <summary>
    /// 计算单个号位卡片宽度。
    /// </summary>
    /// <param name="values">[视口宽, 号位数]。</param>
    /// <param name="targetType">目标类型（未用）。</param>
    /// <param name="parameter">参数（未用）。</param>
    /// <param name="culture">区域（未用）。</param>
    /// <returns>卡片宽度；视口未测量时返回 <see cref="double.NaN"/>（自动）。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not double viewport || double.IsNaN(viewport) || viewport <= 0 ||
            values[1] is not int count || count <= 0)
        {
            return double.NaN;
        }

        var divisor = Math.Min(count, 4);
        var width = (viewport / divisor) - CardMargin;
        return width < 1 ? double.NaN : width;
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">值。</param>
    /// <param name="targetTypes">目标类型。</param>
    /// <param name="parameter">参数。</param>
    /// <param name="culture">区域。</param>
    /// <returns>抛出 <see cref="NotSupportedException"/>。</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
