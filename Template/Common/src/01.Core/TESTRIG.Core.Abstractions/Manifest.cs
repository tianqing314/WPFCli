namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 一套针床（一块板）的强类型清单——纯数据。新增板子 = 加一份此 JSON，零代码。
/// 取代旧 <c>*_Auto.json</c> + <c>*.distributed.json</c> 的多态 <c>$type</c> 反序列化。
/// </summary>
public sealed record JigManifest
{
    /// <summary>
    /// 唯一任务标识，如 "ConST218A_Mainboard"。
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 菜单一级：设备，如 "ConST218A"。
    /// </summary>
    public required string DeviceFamily { get; init; }

    /// <summary>
    /// 菜单二级：板子名称，如 "主板（径向）"。
    /// </summary>
    public required string BoardName { get; init; }

    /// <summary>
    /// 任务描述。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 被检板驱动 + 连接（型号决定用哪个 DUT 驱动）。
    /// </summary>
    public required DeviceDescriptor Dut { get; init; }

    /// <summary>
    /// 号位/板位（一套针床上的多块板）。对应旧 TestTaskSources。
    /// </summary>
    public IReadOnlyList<PositionDescriptor> Positions { get; init; } = [];

    /// <summary>
    /// 测试项序列。
    /// </summary>
    public IReadOnlyList<StepDescriptor> Steps { get; init; } = [];

    /// <summary>
    /// 针床工装子设备（可选，默认空）。用于「被检不直接通讯、但针床工装上有需要连接的板级子设备」的板
    /// （如 PS02/A20 传感器板烧录的针床继电器 D/E 与 2 路电流计）。非空时连接配置窗以「针床工装」组呈现这些设备、
    /// 隐藏原「按号位被检」行；端点持久化到 connections.json 的 <see cref="ConnectionSettings.Fixtures"/>。
    /// 现有板不设此字段 → 行为完全不变。
    /// </summary>
    public IReadOnlyList<SubDeviceConfig> FixtureDevices { get; init; } = [];

    /// <summary>
    /// 标准模块（Tool 设备）列表（可选，默认空）。整机等模板的标准设备（如 ConST171 的 DPSEX1 正压 /
    /// DPSEX2 真空标准模块）挂在此：按 <see cref="ToolDeviceDescriptor.Key"/> 创建实例，
    /// 处理器用 <c>GetDevice&lt;T&gt;(deviceKey)</c> 获取（同一型号可多实例，独立串口/SN）。
    /// 空 = 无标准模块，行为不变。
    /// </summary>
    public IReadOnlyList<ToolDeviceDescriptor> ToolDevices { get; init; } = [];
}

/// <summary>
/// 标准模块（Tool 设备）描述：每号位独立的标准设备（独立通讯/SN），按 <see cref="Key"/> 区分实例。
/// 驱动按 <see cref="Model"/> 经标准模块注册表创建（如 DPSEX 标准模块 → DPSEXStandardModule）。
/// </summary>
/// <param name="Key">实例键（旧 DeviceKey，如 DPSEX1/DPSEX2），处理器按此获取。</param>
/// <param name="Name">设备名（仅界面/日志显示，如"正压模块"）。</param>
/// <param name="Model">型号：决定用哪个标准模块驱动（如 DPSEX）。</param>
public sealed record ToolDeviceDescriptor(string Key, string Name, string Model)
{
    /// <summary>
    /// 该标准模块的连接端点（通讯方式与串口/网口参数）。
    /// </summary>
    public CommEndpoint? Comm { get; init; }

    /// <summary>
    /// 期望设备序列号（DevSn；可空 = 不校验序列号，连接按 IsExist 判定）。
    /// </summary>
    public string? SerialNumber { get; init; }
}

/// <summary>
/// 共享设备（标准模块）独立配置文件：整机等模板每套工装一份（<c>Manifests/&lt;设备族&gt;/&lt;Key&gt;.shared.json</c>），
    /// 由测试项维护窗口增删改。运行时存在则**完全取代** manifest 的 <see cref="JigManifest.ToolDevices"/>
/// （References 转换只提供初始默认，用户维护为准）；不存在则回落 manifest 默认。
/// </summary>
public sealed class ToolDeviceConfigFile
{
    /// <summary>
    /// 配置格式版本（当前 1）。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 共享设备清单。
    /// </summary>
    public List<ToolDeviceDescriptor> Devices { get; set; } = [];
}

/// <summary>
/// 共享设备（标准模块）配置仓储：按工装（设备族 + Key）读写独立配置文件。
/// </summary>
public interface ISharedDeviceStore
{
    /// <summary>
    /// 加载某工装的共享设备配置。无独立配置文件返回 null（调用方回落 manifest 默认 ToolDevices）。
    /// </summary>
    /// <param name="deviceFamily">设备族（manifest.DeviceFamily）。</param>
    /// <param name="key">工装 Key（manifest.Key）。</param>
    /// <returns>共享设备清单，无配置返回 null。</returns>
    IReadOnlyList<ToolDeviceDescriptor>? Load(string deviceFamily, string key);

    /// <summary>
    /// 保存某工装的共享设备配置（覆盖写 .shared.json；空清单也落盘，表示显式清空）。
    /// </summary>
    /// <param name="deviceFamily">设备族。</param>
    /// <param name="key">工装 Key。</param>
    /// <param name="devices">共享设备清单。</param>
    void Save(string deviceFamily, string key, IReadOnlyList<ToolDeviceDescriptor> devices);
}

/// <summary>
/// 号位/板位（A/B/C 物理工位是 PLC 层概念，后续轮处理，不在此）。
/// 每个号位是一块被检拼版，各自一个连接端点（被检连接随号位走，不在 Dut 上）。
/// </summary>
/// <param name="Index">号位序号（1 起）。</param>
/// <param name="Name">号位名称。</param>
public sealed record PositionDescriptor(int Index, string Name)
{
    /// <summary>
    /// 该号位被检板的连接端点（来自 manifest，可被 connections.json 覆盖）。
    /// </summary>
    public CommEndpoint? Comm { get; init; }
}

/// <summary>
/// 设备描述（取代旧 Devices[].$type 多态）。被检板用；标准盒/PLC 为全局共享，不在 manifest。
/// </summary>
/// <param name="Name">设备名（仅用于界面/日志显示）。</param>
/// <param name="Model">型号：**一板一型号**，既决定用哪个 DUT 驱动（<c>[DutDriver("型号")]</c> 注册串），
/// 也是结果落库的 <c>DeviceModel</c> 与数据查看页的过滤依据，故绝不可多块板共用同一值。</param>
/// <param name="Comm">连接端点（可空）。</param>
/// <param name="SerialNumber">期望设备序列号（标准模块等配置了 DevSn 时用于连接验证；可空）。</param>
public sealed record DeviceDescriptor(string Name, string Model, CommEndpoint? Comm = null, string? SerialNumber = null);

/// <summary>
/// 测试项描述（对应旧 TaskCollection 一项 + Location.Entry）。行为由 <see cref="Kind"/> 对应的处理器提供。
/// </summary>
public sealed record StepDescriptor
{
    /// <summary>
    /// 执行顺序（升序，1 起）。**无需在 JSON 中指定**——由 <see cref="ManifestLoader"/> 按 Steps 数组的加载顺序自动赋值，
    /// 调整顺序只需改数组位置。序列化时忽略（不写回 JSON），加载时按数组位置重算。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Order { get; init; }

    /// <summary>
    /// 项唯一标识（=旧 Location.Entry），如 "TestBatterVoltageDy"。
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 处理器类型，决定用哪个 IStepHandler，如 "Measurement"/"PowerConsumption"。
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// 测试项显示名。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 测试项描述。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 处理器读取的设置项（point/channel/boardType 等）。
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 测试项参数集合。
    /// </summary>
    public IReadOnlyList<ParameterDescriptor> Parameters { get; init; } = [];

    /// <summary>
    /// 判定条件集合。
    /// </summary>
    public IReadOnlyList<ConditionDescriptor> Conditions { get; init; } = [];

    /// <summary>
    /// 执行类型：Auto（自动，默认）/ Manual（人工确认步，整机模板用，引擎暂停号位等操作员 OK/NG）/
    /// Process（过程等待步，如温控，由处理器用 <c>ProcessWaiter</c> 轮询条件并实时上报曲线）。
    /// </summary>
    public string StepType { get; init; } = "Auto";

    /// <summary>
    /// 人工确认 / 过程等待的超时毫秒数（0 或省略 = 不限制）。
    /// </summary>
    public int TimeoutMs { get; init; }

    /// <summary>
    /// 测试项 GUID（默认自动生成）。
    /// </summary>
    public string Guid { get; init; } = System.Guid.NewGuid().ToString();
}

/// <summary>
/// 测试项参数（取代旧 ParameterBase）。
/// </summary>
/// <param name="Name">参数名。</param>
/// <param name="Value">参数值。</param>
/// <param name="Unit">单位（可空）。</param>
public sealed record ParameterDescriptor(string Name, string Value, string? Unit = null);

/// <summary>
/// 判定条件（取代旧 RangeCondition/ValueCondition/TextCondition 的数据部分）。
/// </summary>
public sealed record ConditionDescriptor
{
    /// <summary>
    /// 条件类型：Range / Value / Text。
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// 条件名称。
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 下限（Range 用）。
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// 上限（Range 用）。
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// 期望值（Text/Value 用）。
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// 单位。
    /// </summary>
    public string? Unit { get; init; }
}
