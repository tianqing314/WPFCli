using System.Management;
using System.Text.RegularExpressions;
using Xmas11.IO.USB;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// 串口扫描器：WMI `Win32_PnPEntity`(Caption 含 "(COMn)") 枚举 COM，
/// 用 GetDeviceProperties 取 `DEVPKEY_Device_LocationPaths` 解析 USB 端口链作物理链路。
/// 移植自参考项目 `多设备位置定位配置/WmiDeviceScanner`。仅 Windows；异常时返回空，不抛。
/// </summary>
public sealed class WmiSerialScanner : IDeviceScanner
{
    /// <summary>
    /// 匹配 "(COMn)" 提取 COM 号。
    /// </summary>
    private static readonly Regex ComRx = new(@"\(COM(\d+)\)", RegexOptions.Compiled);

    /// <summary>
    /// 匹配位置路径中的 "USB(n)" 段。
    /// </summary>
    private static readonly Regex UsbRx = new(@"USB\((\d+)\)", RegexOptions.Compiled);

    /// <summary>
    /// 匹配设备 ID 中的 "VID_xxxx&amp;PID_xxxx"。
    /// </summary>
    private static readonly Regex VidPidRx = new(@"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    /// <summary>
    /// 枚举当前串口设备（COM + USB 端口链）。
    /// </summary>
    /// <returns>串口设备列表；异常返回空。</returns>
    public IReadOnlyList<DiscoveredSerial> ScanSerial()
    {
        var list = new List<DiscoveredSerial>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");
            foreach (ManagementObject mo in searcher.Get())
            {
                var caption = mo["Caption"]?.ToString() ?? "";
                var m = ComRx.Match(caption);
                if (!m.Success)
                {
                    continue;
                }

                var com = "COM" + m.Groups[1].Value;
                var chain = TryGetPortChain(mo);
                if (string.IsNullOrEmpty(chain))
                {
                    // 兜底：无端口链时用 COM 自身作链路号
                    chain = com;
                }

                list.Add(new DiscoveredSerial(com, chain, caption, BuildLabel(chain, com, caption)));
            }
        }
        catch
        {
            return Array.Empty<DiscoveredSerial>();
        }
        return list;
    }

    /// <summary>
    /// 枚举指定 VID 的 USB 设备（如 ConST 0x2E19）。仅 Windows；异常时返回空，不抛。
    /// </summary>
    /// <param name="vid">厂商 ID。</param>
    /// <returns>USB 设备列表；异常返回空。</returns>
    public IReadOnlyList<DiscoveredUsb> ScanUsb(int vid)
    {
        var list = new List<DiscoveredUsb>();
        try
        {
            // 用 Xmas11 自带 USB 枚举：这里取到的 DeviceLocation 正是 USBDevice(vid,pid,location) / ATCDevice
            // 连接时匹配用的键。WMI 位置路径端口链（如 "4.1"）与之不同，若作 location 会报 "Device not found"。
            if (!USBDevice.Find(out Dictionary<USBVidPid, List<DeviceProperties>> all) || all is null)
            {
                return list;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in all)
            {
                if (kv.Key.VID != (uint)vid)
                {
                    // 只保留目标 VID（过滤 ConST 自家设备）
                    continue;
                }

                foreach (var dp in kv.Value)
                {
                    var loc = dp.DeviceLocation ?? "";
                    if (string.IsNullOrWhiteSpace(loc) || !seen.Add(loc))
                    {
                        continue;
                    }

                    var friendly = FirstNonBlank(dp.FriendlyName, dp.DeviceDescription, "USB 设备");
                    var pid = (int)kv.Key.PID;
                    list.Add(new DiscoveredUsb(loc, vid, pid, friendly, BuildUsbLabel(loc, vid, pid, friendly)));
                }
            }
        }
        catch
        {
            return Array.Empty<DiscoveredUsb>();
        }
        return list;
    }

    /// <summary>
    /// 取第一个非空白串（都空返回末位兜底）。
    /// </summary>
    /// <param name="values">候选串。</param>
    /// <returns>第一个非空白串。</returns>
    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v!;
            }
        }
        return "";
    }

    /// <summary>
    /// 取设备的 USB 端口链（经 WMI GetDeviceProperties 读位置路径解析）。
    /// </summary>
    /// <param name="mo">WMI 设备对象。</param>
    /// <returns>端口链（如 "1.1.4"），失败为空串。</returns>
    private static string TryGetPortChain(ManagementObject mo)
    {
        try
        {
            var inParams = mo.GetMethodParameters("GetDeviceProperties");
            inParams["devicePropertyKeys"] = new[] { "DEVPKEY_Device_LocationPaths" };
            var outParams = mo.InvokeMethod("GetDeviceProperties", inParams, null);
            if (outParams?["deviceProperties"] is ManagementBaseObject[] props && props.Length > 0)
            {
                var data = props[0]["Data"];
                var loc = data switch
                {
                    string[] arr when arr.Length > 0 => arr[0],
                    string s => s,
                    _ => "",
                };
                return ExtractUsbPortChain(loc);
            }
        }
        catch
        {
            // 无位置路径能力的设备：返回空串由调用方兜底
        }
        return "";
    }

    /// <summary>
    /// 从 Windows 位置路径提取 USB 端口链。
    /// 例："PCIROOT(0)#...#USBROOT(0)#USB(1)#USB(1)#USB(4)" → "1.1.4"。
    /// </summary>
    /// <param name="winLocPath">Windows 位置路径。</param>
    /// <returns>端口链，无法解析为空串。</returns>
    private static string ExtractUsbPortChain(string winLocPath)
    {
        if (string.IsNullOrWhiteSpace(winLocPath))
        {
            return "";
        }

        var rootIdx = winLocPath.IndexOf("USBROOT(", StringComparison.Ordinal);
        if (rootIdx < 0)
        {
            return "";
        }

        var after = winLocPath[rootIdx..];
        var matches = UsbRx.Matches(after);
        if (matches.Count <= 1)
        {
            return "";
        }

        return string.Join('.', matches.Cast<Match>().Skip(1).Select(x => x.Groups[1].Value));
    }

    /// <summary>
    /// 构造串口的可读标签。
    /// </summary>
    /// <param name="chain">端口链。</param>
    /// <param name="com">COM 号。</param>
    /// <param name="friendly">友好名。</param>
    /// <returns>标签串。</returns>
    private static string BuildLabel(string chain, string com, string friendly)
    {
        var prefix = chain == com ? "(无端口链)" : "HUB根→" + string.Join("→", chain.Split('.').Select(p => $"端口{p}"));
        return $"{prefix} → {com}  [{friendly}]";
    }

    /// <summary>
    /// 匹配 USB DeviceLocation（如 "Port_#0001.Hub_#0004"）。
    /// </summary>
    private static readonly Regex UsbLocRx = new(@"Port_#0*(\d+)\.Hub_#0*(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// 构造 USB 的可读标签（从 DeviceLocation 解析 HUB/端口）。
    /// </summary>
    /// <param name="location">USB DeviceLocation。</param>
    /// <param name="vid">厂商 ID。</param>
    /// <param name="pid">产品 ID。</param>
    /// <param name="friendly">友好名。</param>
    /// <returns>标签串。</returns>
    private static string BuildUsbLabel(string location, int vid, int pid, string friendly)
    {
        var m = UsbLocRx.Match(location);
        var readable = m.Success ? $"HUB{m.Groups[2].Value}→端口{m.Groups[1].Value}" : location;
        return $"{readable}  VID_{vid:X4}&PID_{pid:X4}  [{friendly}]";
    }
}
