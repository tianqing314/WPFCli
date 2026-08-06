namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 判定器——把旧 <c>RangeCondition</c>/<c>ValueCondition</c>/<c>TextCondition</c> 的比较逻辑
/// 抽成纯函数，便于单测、无 UI 依赖。
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// 按条件判定一个数值测量值。
    /// </summary>
    /// <param name="condition">判定条件。</param>
    /// <param name="value">数值测量值。</param>
    /// <returns>判定结果。</returns>
    ConditionResult Evaluate(ConditionDescriptor condition, double value);

    /// <summary>
    /// 按条件判定一个文本测量值。
    /// </summary>
    /// <param name="condition">判定条件。</param>
    /// <param name="value">文本测量值。</param>
    /// <returns>判定结果。</returns>
    ConditionResult Evaluate(ConditionDescriptor condition, string value);

    /// <summary>
    /// 对一组条件全部求值，任一不过则整体不过。
    /// </summary>
    /// <param name="conditions">条件集合。</param>
    /// <param name="value">数值测量值。</param>
    /// <returns>判定结果。</returns>
    ConditionResult EvaluateAll(IEnumerable<ConditionDescriptor> conditions, double value);
}

/// <summary>
/// 判定结果：是否通过 + 描述信息。
/// </summary>
public sealed record ConditionResult(bool Passed, string Message)
{
    /// <summary>
    /// 通过的默认结果。
    /// </summary>
    public static readonly ConditionResult Ok = new(true, "OK");
}
