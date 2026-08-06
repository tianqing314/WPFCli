using System.Globalization;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Core.Engine;

/// <summary>
/// 默认判定器：移植旧 Range/Value/Text 条件的比较语义为纯函数。
/// </summary>
public sealed class ConditionEvaluator : IConditionEvaluator
{
    /// <summary>
    /// 按条件判定数值测量值（Range=区间、Value=期望±容差、其它转文本判定）。
    /// </summary>
    /// <param name="condition">判定条件。</param>
    /// <param name="value">数值测量值。</param>
    /// <returns>判定结果。</returns>
    public ConditionResult Evaluate(ConditionDescriptor condition, double value)
    {
        switch (condition.Kind)
        {
            case "Range":
                {
                    var lo = condition.Min;
                    var hi = condition.Max;
                    bool ok = (lo is null || value >= lo.Value) && (hi is null || value <= hi.Value);
                    string range = $"[{(lo?.ToString(CultureInfo.InvariantCulture) ?? "-∞")}, {(hi?.ToString(CultureInfo.InvariantCulture) ?? "+∞")}]";
                    return new ConditionResult(ok, ok ? $"{value} 在范围 {range} 内" : $"{value} 超出范围 {range}");
                }
            case "Value":
                {
                    if (condition.Expected is not null && double.TryParse(condition.Expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var exp))
                    {
                        // Max 复用为容差
                        var tol = condition.Max ?? 0;
                        bool ok = Math.Abs(value - exp) <= tol;
                        return new ConditionResult(ok, ok ? $"{value} ≈ {exp}(±{tol})" : $"{value} 偏离期望 {exp}(±{tol})");
                    }
                    return new ConditionResult(false, "Value 条件缺少 Expected");
                }
            default:
                return Evaluate(condition, value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// 按条件判定文本测量值（Text=字符串比较，Range/Value 尝试转数值后判定）。
    /// </summary>
    /// <param name="condition">判定条件。</param>
    /// <param name="value">文本测量值。</param>
    /// <returns>判定结果。</returns>
    public ConditionResult Evaluate(ConditionDescriptor condition, string value)
    {
        switch (condition.Kind)
        {
            case "Text":
                {
                    bool ok = string.Equals(value?.Trim(), condition.Expected?.Trim(), StringComparison.OrdinalIgnoreCase);
                    return new ConditionResult(ok, ok ? $"\"{value}\" == 期望" : $"\"{value}\" != 期望 \"{condition.Expected}\"");
                }
            case "Range":
            case "Value":
                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return Evaluate(condition, d);
                }

                return new ConditionResult(false, $"\"{value}\" 非数值，无法按 {condition.Kind} 判定");
            default:
                return new ConditionResult(false, $"未知条件类型 {condition.Kind}");
        }
    }

    /// <summary>
    /// 对一组条件全部求值，任一不过则整体不过。
    /// </summary>
    /// <param name="conditions">条件集合。</param>
    /// <param name="value">数值测量值。</param>
    /// <returns>判定结果（全过返回 Ok）。</returns>
    public ConditionResult EvaluateAll(IEnumerable<ConditionDescriptor> conditions, double value)
    {
        foreach (var c in conditions)
        {
            var r = Evaluate(c, value);
            if (!r.Passed)
            {
                return r;
            }
        }
        return ConditionResult.Ok;
    }
}
