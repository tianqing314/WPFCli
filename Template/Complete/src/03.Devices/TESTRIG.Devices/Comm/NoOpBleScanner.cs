using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// 无蓝牙扫描能力时的兜底实现（返回 null）。真机由 TESTRIG.Devices.BleWin 的 Win10 实现覆盖。
/// </summary>
public sealed class NoOpBleScanner : IBleScanner
{
    /// <summary>
    /// 兜底实现：直接返回 null（无扫描能力）。
    /// </summary>
    /// <param name="name">蓝牙名称。</param>
    /// <param name="mac">蓝牙 MAC。</param>
    /// <param name="timeoutSeconds">超时（秒）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>恒为 null。</returns>
    public Task<int?> ScanRssiAsync(string name, string mac, int timeoutSeconds = 20, CancellationToken ct = default)
    {
        return Task.FromResult<int?>(null);
    }
}
