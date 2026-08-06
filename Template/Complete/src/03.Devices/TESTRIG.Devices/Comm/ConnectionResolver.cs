using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// 解析结果：把 CommEndpoint 解析到当前可用的具体目标（串口当前 COM / 网络 ip:port）。
/// </summary>
/// <param name="Link">通讯方式。</param>
/// <param name="Target">解析出的目标（COM 号或 ip:port），失败为 null。</param>
/// <param name="Ok">是否解析成功。</param>
/// <param name="Message">描述信息。</param>
public sealed record ResolvedEndpoint(LinkType Link, string? Target, bool Ok, string Message);

/// <summary>
/// 把 CommEndpoint（物理链路）解析到当前句柄目标。串口按物理链路匹配当前 COM；网络直通；USB 下一轮。
/// </summary>
public interface IConnectionResolver
{
    /// <summary>
    /// 解析端点到当前可用目标。
    /// </summary>
    /// <param name="endpoint">通讯端点。</param>
    /// <returns>解析结果。</returns>
    ResolvedEndpoint Resolve(CommEndpoint endpoint);
}

/// <summary>
/// 默认连接解析器：网络直通、串口按物理链路匹配当前 COM、USB 暂不支持。
/// </summary>
public sealed class ConnectionResolver : IConnectionResolver
{
    /// <summary>
    /// 设备扫描器（供串口物理链路匹配）。
    /// </summary>
    private readonly IDeviceScanner _scanner;

    /// <summary>
    /// 构造连接解析器。
    /// </summary>
    /// <param name="scanner">设备扫描器。</param>
    public ConnectionResolver(IDeviceScanner scanner)
    {
        _scanner = scanner;
    }

    /// <summary>
    /// 解析端点到当前可用目标。
    /// </summary>
    /// <param name="e">通讯端点。</param>
    /// <returns>解析结果。</returns>
    public ResolvedEndpoint Resolve(CommEndpoint e)
    {
        return e.Link switch
        {
            LinkType.Ethernet => string.IsNullOrWhiteSpace(e.Ip)
                ? new ResolvedEndpoint(e.Link, null, false, "未配置 IP")
                : new ResolvedEndpoint(e.Link, $"{e.Ip}:{e.Port}", true, $"网络 {e.Ip}:{e.Port}"),
            LinkType.Serial => ResolveSerial(e),
            LinkType.Usb => new ResolvedEndpoint(e.Link, null, false, "USB 物理链路解析下一轮支持"),
            _ => new ResolvedEndpoint(e.Link, null, false, "未知通讯类型"),
        };
    }

    /// <summary>
    /// 串口解析：按物理链路（或直接 COM）匹配当前扫描到的串口。
    /// </summary>
    /// <param name="e">串口端点。</param>
    /// <returns>解析结果。</returns>
    private ResolvedEndpoint ResolveSerial(CommEndpoint e)
    {
        if (string.IsNullOrWhiteSpace(e.PhysicalLink))
        {
            return new ResolvedEndpoint(e.Link, null, false, "未配置串口物理链路");
        }

        var hit = _scanner.ScanSerial()
            .FirstOrDefault(s => s.PortChain == e.PhysicalLink || s.Com == e.PhysicalLink);
        return hit is null
            ? new ResolvedEndpoint(e.Link, null, false, $"物理链路[{e.PhysicalLink}] 未发现对应串口")
            : new ResolvedEndpoint(e.Link, hit.Com, true, $"物理链路[{e.PhysicalLink}] → {hit.Com}");
    }
}
