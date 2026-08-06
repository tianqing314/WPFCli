namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 通讯方式：网络 / 串口 / USB。
/// </summary>
public enum LinkType
{
    /// <summary>
    /// 网络（TCP/IP）。
    /// </summary>
    Ethernet,

    /// <summary>
    /// 串口。
    /// </summary>
    Serial,

    /// <summary>
    /// USB。
    /// </summary>
    Usb,
}

/// <summary>
/// 串口参数。
/// </summary>
/// <param name="Baud">波特率。</param>
/// <param name="DataBits">数据位。</param>
/// <param name="StopBits">停止位。</param>
/// <param name="Parity">校验方式。</param>
public sealed record SerialParams(int Baud = 9600, int DataBits = 8, string StopBits = "One", string Parity = "None");

/// <summary>
/// 一个设备的通讯端点。串口/USB 存**物理链路号**（稳定标识）——COM 号/USB 句柄运行时由
/// 扫描器/解析器按物理链路匹配（COM 号会变、物理链路不变）。取代旧扁平 CommDescriptor。
/// </summary>
public sealed record CommEndpoint
{
    /// <summary>
    /// 通讯方式。
    /// </summary>
    public LinkType Link { get; init; } = LinkType.Ethernet;

    /// <summary>
    /// 网络 IP（Ethernet 用）。
    /// </summary>
    public string? Ip { get; init; }

    /// <summary>
    /// 网络端口（Ethernet 用）。
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// 串口/USB 物理链路号（USB 端口链如 "1-1.1.4"，或串口位标识）。
    /// </summary>
    public string? PhysicalLink { get; init; }

    /// <summary>
    /// 串口参数（Serial 用）。
    /// </summary>
    public SerialParams? Serial { get; init; }

    /// <summary>
    /// USB 厂商 ID（Usb 用）。
    /// </summary>
    public int? Vid { get; init; }

    /// <summary>
    /// USB 产品 ID（Usb 用）。
    /// </summary>
    public int? Pid { get; init; }

    /// <summary>
    /// 构造一个网络端点。
    /// </summary>
    /// <param name="ip">IP。</param>
    /// <param name="port">端口。</param>
    /// <returns>网络端点。</returns>
    public static CommEndpoint OfEthernet(string ip, int port)
    {
        return new() { Link = LinkType.Ethernet, Ip = ip, Port = port };
    }

    /// <summary>
    /// 构造一个串口端点。
    /// </summary>
    /// <param name="physicalLink">物理链路号。</param>
    /// <param name="p">串口参数（默认 9600-8-N-1）。</param>
    /// <returns>串口端点。</returns>
    public static CommEndpoint OfSerial(string physicalLink, SerialParams? p = null)
    {
        return new() { Link = LinkType.Serial, PhysicalLink = physicalLink, Serial = p ?? new SerialParams() };
    }

    /// <summary>
    /// 构造一个 USB 端点。
    /// </summary>
    /// <param name="physicalLink">物理链路号。</param>
    /// <param name="vid">厂商 ID。</param>
    /// <param name="pid">产品 ID。</param>
    /// <returns>USB 端点。</returns>
    public static CommEndpoint OfUsb(string physicalLink, int vid, int pid)
    {
        return new() { Link = LinkType.Usb, PhysicalLink = physicalLink, Vid = vid, Pid = pid };
    }

    /// <summary>
    /// 生成端点的可读描述。
    /// </summary>
    /// <returns>描述串。</returns>
    public string Describe()
    {
        return Link switch
        {
            LinkType.Ethernet => $"网络 {Ip}:{Port}",
            LinkType.Serial => $"串口 链路[{PhysicalLink}] {Serial?.Baud}",
            LinkType.Usb => $"USB 链路[{PhysicalLink}] VID_{Vid:X4}&PID_{Pid:X4}",
            _ => "未知",
        };
    }
}
