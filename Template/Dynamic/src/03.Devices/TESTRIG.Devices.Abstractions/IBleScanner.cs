namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 上位机蓝牙扫描（**非被检指令**）：扫描指定名称/MAC 的 BLE 广播测信号强度。
/// 取代旧 <c>Win10BLEDeviceHelper.SeBleDeviceAsync</c>。真机实现走 Win10 WinRT 广播监听（TESTRIG.Devices.BleWin），
/// 无实现时回落 <c>NoOpBleScanner</c>（返回 null）。
/// </summary>
public interface IBleScanner
{
    /// <summary>
    /// 扫描 BLE 广播，命中返回 RSSI(dBm)，超时未命中返回 null。
    /// </summary>
    /// <param name="name">目标蓝牙名称。</param>
    /// <param name="mac">目标 MAC（12 位十六进制）。</param>
    /// <param name="timeoutSeconds">扫描超时（秒）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>命中返回 RSSI(dBm)，超时返回 null。</returns>
    Task<int?> ScanRssiAsync(string name, string mac, int timeoutSeconds = 20, CancellationToken ct = default);
}
