namespace TESTRIG.Automation;

/// <summary>
/// 产线计数器：通过/失败/重试/平均节拍。对应旧工具栏 通过个数/失败个数/重试个数/平均(min)。
/// </summary>
public sealed class AutomationCounters
{
    /// <summary>
    /// 累计节拍秒数。
    /// </summary>
    private double _totalSeconds;

    /// <summary>
    /// 已记录节拍的样本数。
    /// </summary>
    private int _recorded;

    /// <summary>
    /// 通过个数。
    /// </summary>
    public int Passed { get; private set; }

    /// <summary>
    /// 失败个数。
    /// </summary>
    public int Failed { get; private set; }

    /// <summary>
    /// 重试个数。
    /// </summary>
    public int Retried { get; private set; }

    /// <summary>
    /// 平均节拍（秒），无样本为 0。
    /// </summary>
    public double AvgSeconds => _recorded == 0 ? 0 : _totalSeconds / _recorded;

    /// <summary>
    /// 任一计数变化后触发，供 UI 刷新。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 记录一次测试结果与节拍。
    /// </summary>
    /// <param name="passed">是否通过。</param>
    /// <param name="seconds">本次节拍秒数。</param>
    public void Record(bool passed, double seconds)
    {
        if (passed)
        {
            Passed++;
        }
        else
        {
            Failed++;
        }

        _totalSeconds += seconds;
        _recorded++;
        Changed?.Invoke();
    }

    /// <summary>
    /// 重试计数 +1。
    /// </summary>
    public void AddRetry()
    {
        Retried++;
        Changed?.Invoke();
    }

    /// <summary>
    /// 清零所有计数。
    /// </summary>
    public void Reset()
    {
        Passed = Failed = Retried = 0;
        _totalSeconds = 0;
        _recorded = 0;
        Changed?.Invoke();
    }
}
