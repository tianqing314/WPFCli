namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 泄露设备型号（旧脚本 <c>LeakDeviceModel</c>）。同型号在各泄露位置的容积一致。
/// </summary>
public enum LeakDeviceModel
{
    /// <summary>表压、差压和微差压设备测试泄露的容积一致。</summary>
    MpDpLlp = 0,

    /// <summary>高精度、气象版的设备测试泄露的容积一致。</summary>
    Hmp = 1,
}

/// <summary>
/// 泄露位置（旧脚本 <c>LeakPosition</c>）。
/// </summary>
public enum LeakPosition
{
    /// <summary>正压输出端。</summary>
    PositiveExport = 0,

    /// <summary>负压输出端。</summary>
    NegativeExport = 1,

    /// <summary>正压气源。</summary>
    PositiveSource = 2,

    /// <summary>负压气源。</summary>
    NegativeSource = 3,
}

/// <summary>
/// 泄露速率计算工具（移植旧脚本 <c>Util.LeakTestValueFormula</c>）。
/// 公式：Q = Ve * △P * 60 / (P0 * T)，结果单位 ml/min。
/// Ve 为容积（按设备型号 + 泄露位置查表），△P 为压力变化量，P0 为大气压，T 为采集时长（秒）。
/// 集中维护、可单测。
/// </summary>
public static class LeakFormula
{
    /// <summary>
    /// 计算泄露速率（ml/min）。
    /// </summary>
    /// <param name="deviceModel">设备型号（决定容积）。</param>
    /// <param name="position">泄露位置（决定容积）。</param>
    /// <param name="pressureChange">压力变化量 △P（kPa）。</param>
    /// <param name="time">采集时长 T（秒）。</param>
    /// <param name="atm">大气压 P0（kPa）。</param>
    /// <returns>泄露速率（ml/min）；atm=0 或 time=0 返回 <see cref="double.NaN"/>。</returns>
    public static double Compute(LeakDeviceModel deviceModel, LeakPosition position,
        double pressureChange, double time, double atm)
    {
        if (atm == 0 || time == 0)
        {
            return double.NaN;
        }

        var volume = GetVolume(deviceModel, position);
        return (volume * pressureChange * 60) / (atm * time);
    }

    /// <summary>
    /// 按设备型号 + 泄露位置查容积 Ve（ml）。容积表移植自旧脚本 <c>Volume</c> 类。
    /// </summary>
    /// <param name="deviceModel">设备型号。</param>
    /// <param name="position">泄露位置。</param>
    /// <returns>容积（ml）；未知组合返回 0。</returns>
    private static double GetVolume(LeakDeviceModel deviceModel, LeakPosition position)
    {
        return (deviceModel, position) switch
        {
            // 表压/差压/微差压：正/负压输出端容积不同，气源端容积一致
            (LeakDeviceModel.MpDpLlp, LeakPosition.PositiveExport) => 13.5,
            (LeakDeviceModel.MpDpLlp, LeakPosition.NegativeExport) => 15.5,
            (LeakDeviceModel.MpDpLlp, LeakPosition.PositiveSource) => 11.82,
            (LeakDeviceModel.MpDpLlp, LeakPosition.NegativeSource) => 11.82,

            // 高精度/气象版：正/负压输出端容积一致，气源端容积一致
            (LeakDeviceModel.Hmp, LeakPosition.PositiveExport) => 13.5,
            (LeakDeviceModel.Hmp, LeakPosition.NegativeExport) => 13.5,
            (LeakDeviceModel.Hmp, LeakPosition.PositiveSource) => 11.82,
            (LeakDeviceModel.Hmp, LeakPosition.NegativeSource) => 11.82,

            _ => 0,
        };
    }
}
