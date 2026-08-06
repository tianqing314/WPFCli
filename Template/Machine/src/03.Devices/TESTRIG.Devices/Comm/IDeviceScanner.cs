namespace TESTRIG.Devices.Comm;

/// <summary>
/// 扫描到的一个串口设备。PortChain=物理链路(USB端口链)，作稳定标识；Com=当前分配的COM号(会变)。
/// </summary>
/// <param name="Com">当前分配的 COM 号。</param>
/// <param name="PortChain">物理链路（USB 端口链）。</param>
/// <param name="Friendly">友好名。</param>
/// <param name="Label">标签。</param>
public sealed record DiscoveredSerial(string Com, string PortChain, string Friendly, string Label);

/// <summary>
/// 扫描到的一个 USB 设备（已按 VID 过滤）。PortChain=物理链路；Vid/Pid=厂商/产品标识。
/// </summary>
/// <param name="PortChain">物理链路。</param>
/// <param name="Vid">厂商 ID。</param>
/// <param name="Pid">产品 ID。</param>
/// <param name="Friendly">友好名。</param>
/// <param name="Label">标签。</param>
public sealed record DiscoveredUsb(string PortChain, int Vid, int Pid, string Friendly, string Label);

/// <summary>
/// 设备扫描器：枚举本机当前可用的串口 / USB（USB 按 VID 过滤，如 ConST 自家 0x2E19）。
/// </summary>
public interface IDeviceScanner
{
    /// <summary>
    /// 枚举当前串口设备。
    /// </summary>
    /// <returns>串口设备列表。</returns>
    IReadOnlyList<DiscoveredSerial> ScanSerial();

    /// <summary>
    /// 枚举指定 VID 的 USB 设备（如 ConST 0x2E19）。返回物理链路 + VID/PID。
    /// </summary>
    /// <param name="vid">厂商 ID。</param>
    /// <returns>USB 设备列表。</returns>
    IReadOnlyList<DiscoveredUsb> ScanUsb(int vid);
}

/// <summary>
/// USB 枚举（本轮留空，下一轮接 LibUsbDotNet）。
/// </summary>
public interface IUsbEnumerator
{
    /// <summary>
    /// 枚举当前 USB 物理链路。
    /// </summary>
    /// <returns>物理链路列表。</returns>
    IReadOnlyList<string> EnumeratePortChains();
}

/// <summary>
/// 空 USB 枚举器（占位实现，返回空集合）。
/// </summary>
public sealed class EmptyUsbEnumerator : IUsbEnumerator
{
    /// <summary>
    /// 占位实现：返回空集合。
    /// </summary>
    /// <returns>空集合。</returns>
    public IReadOnlyList<string> EnumeratePortChains()
    {
        return Array.Empty<string>();
    }
}
