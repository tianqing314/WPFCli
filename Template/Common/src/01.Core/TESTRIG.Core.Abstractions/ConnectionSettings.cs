namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 连接配置 v2：PLC（网络，单台）+ 标准盒（复合，多子设备各自配置）+ 被检（按板每号位一个）。
/// 取代旧扁平结构。持久化到 Config/connections.json。
/// </summary>
public sealed class ConnectionSettings
{
    /// <summary>
    /// 配置结构版本号。
    /// </summary>
    public int SchemaVersion { get; set; } = 4;

    /// <summary>
    /// PLC（国锐），网络。
    /// </summary>
    public CommEndpoint Plc { get; set; } = CommEndpoint.OfEthernet("223.223.223.100", 502);

    /// <summary>
    /// 标准盒内部子设备（固定集合，基本不变）。
    /// </summary>
    public List<SubDeviceConfig> StandardBox { get; set; } = SubDeviceConfig.DefaultBox();

    /// <summary>
    /// 被检：板 Key → 每号位一个连接（数量=号位数=拼版数）。
    /// </summary>
    public Dictionary<string, List<CommEndpoint>> Duts { get; set; } = new();

    /// <summary>
    /// 针床工装子设备端点：板 Key → 该板 <see cref="JigManifest.FixtureDevices"/> 各子设备的连接端点（顺序与之一致）。
    /// 仅声明了针床工装的板（如 PS02/A20）才有条目；其余板为空。**加法字段**：旧 v4 配置文件缺此项时反序列化为空字典，
    /// 不触发重建、不影响任何现有连接，故无需提升 SchemaVersion。
    /// </summary>
    public Dictionary<string, List<CommEndpoint>> Fixtures { get; set; } = new();

    /// <summary>
    /// 标准模块（共享设备，整机模板：DPSEX1 正压 / DPSEX2 真空）端点覆盖：
    /// manifest <c>ToolDevices</c> 的 Key → 连接端点（串口）。**加法字段**：旧配置缺此项反序列化为空字典，不触发重建。
    /// </summary>
    public Dictionary<string, CommEndpoint> StandardModules { get; set; } = new();
}

/// <summary>
/// 标准盒内一个可通讯子设备。
/// </summary>
public sealed class SubDeviceConfig
{
    /// <summary>
    /// 子设备键（唯一标识）。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 子设备显示名。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 子设备通讯端点。
    /// </summary>
    public CommEndpoint Comm { get; set; } = new();

    /// <summary>
    /// 标准盒默认子设备集合（来自业务文档"内部可通讯设备" + 原框架 DSTB）。
    /// 键与真机 CommID 一致；每个设备**独立 IP、端口不尽相同**（非单 IP+基址偏移）。
    /// ConST326 走串口，其余为网络通讯。现场按实物在 connections.json 里改。
    /// </summary>
    /// <returns>默认子设备配置列表。</returns>
    public static List<SubDeviceConfig> DefaultBox()
    {
        return
        [
            new() { Key = "ConST326", Name = "ConST326 标准信号源", Comm = CommEndpoint.OfSerial("COM3", new SerialParams(9600, 8, "Two", "None")) },
            new() { Key = "BNRC32A", Name = "继电器A（32路）", Comm = CommEndpoint.OfEthernet("192.168.45.101", 1030) },
            new() { Key = "BNRC32B", Name = "继电器B（32路）", Comm = CommEndpoint.OfEthernet("192.168.45.102", 1030) },
            new() { Key = "BNRC16C", Name = "继电器C（16路）", Comm = CommEndpoint.OfEthernet("192.168.45.103", 1030) },
            new() { Key = "ZH4412A", Name = "12路电流计", Comm = CommEndpoint.OfEthernet("192.168.45.104", 20108) },
            new() { Key = "ZH4402A", Name = "2路电流计", Comm = CommEndpoint.OfEthernet("192.168.45.105", 20108) },
            new() { Key = "DAM6803D", Name = "电压采集模块", Comm = CommEndpoint.OfEthernet("192.168.45.106", 1030) },
        ];
    }
}
