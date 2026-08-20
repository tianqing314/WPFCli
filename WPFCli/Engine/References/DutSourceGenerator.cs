using System.Text;
using System.Text.RegularExpressions;

namespace WPFCli.Engine;

/// <summary>
/// DUT 接口与驱动源码生成器 —— 从旧 <c>Uut\*.cs</c>（Bots.TestBench.Device.{dut}_2）解析
/// <c>SimpleCommandEnum</c> 字典，生成新体系两份文件：
///   1) <c>TESTRIG.Devices.Abstractions\Dut\I{dut}Dut.cs</c>（命令枚举 + 接口）
///   2) <c>TESTRIG.Devices\Dut\{dut}\{dut}Dut.cs</c>（[DutDriver] 真机驱动，走 Xmas11 DPG2SCPI）
/// ConST811A 走 APC2Device 反射映射，使用专用模板。
/// </summary>
internal static class DutSourceGenerator
{
    /// <summary>SimpleCommands 字典项：{SimpleCommandEnum.枚举名,"SCPI串"}。</summary>
    private static readonly Regex SimpleCommandPattern = new(
        @"\{SimpleCommandEnum\.([\w\u4e00-\u9fff]+),\s*""([^""]+)""\}", RegexOptions.Compiled);

    internal static (List<string> ifaceFiles, List<string> driverFiles, List<string> todos) GenerateDutFiles(
        string sourceFile, string dut, string outputDir, string bizType)
    {
        var todos = new List<string>();
        var text = File.ReadAllText(sourceFile);

        // 提取 SimpleCommands 字典（枚举名 → SCPI 串）
        var commands = new List<(string Name, string Scpi)>();
        foreach (Match m in SimpleCommandPattern.Matches(text))
            commands.Add((m.Groups[1].Value, m.Groups[2].Value));

        var ifaceDir = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut");
        var driverDir = Path.Combine(outputDir, "src", "03.Devices", "TESTRIG.Devices", "Dut", dut);
        Directory.CreateDirectory(ifaceDir);
        Directory.CreateDirectory(driverDir);

        var ifaceRel = Path.Combine("src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut", $"I{dut}Dut.cs");
        var driverRel = Path.Combine("src", "03.Devices", "TESTRIG.Devices", "Dut", dut, $"{dut}Dut.cs");
        ReferencesAdapter.WriteIfNotExists(Path.Combine(outputDir, ifaceRel), () =>
            dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase)
                ? BuildConST811AInterfaceSource(bizType)
                : BuildInterfaceSource(dut, commands, bizType));
        ReferencesAdapter.WriteIfNotExists(Path.Combine(outputDir, driverRel), () =>
            dut.Equals("ConST811A", StringComparison.OrdinalIgnoreCase)
                ? BuildConST811ADriverSource(dut, bizType)
                : BuildDriverSource(dut, commands, bizType));

        if (commands.Count == 0)
            todos.Add($"{Path.GetFileName(sourceFile)}：未解析到 SimpleCommands 字典，{dut}Command 枚举为空");

        return (new List<string> { ifaceRel }, new List<string> { driverRel }, todos);
    }

    private static string BuildInterfaceSource(string dut, IReadOnlyList<(string Name, string Scpi)> commands, string bizType)
    {
        var label = ReferencesAdapter.BizLabel(bizType);
        var sb = new StringBuilder();
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace TESTRIG.Devices.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} {label}被检命令层。**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2.SimpleCommandEnum</c>");
        sb.AppendLine("/// （SCPI 指令转发）。执行失败抛 <see cref=\"DeviceCommException\"/>（由引擎按异常收尾并落盘）。");
        sb.AppendLine("/// </summary>");
        if (commands.Count > 0)
        {
            sb.AppendLine($"public enum {dut}Command");
            sb.AppendLine("{");
            foreach (var (name, scpi) in commands)
                sb.AppendLine($"    /// <summary>SCPI {scpi}</summary>");
            foreach (var (name, _) in commands)
                sb.AppendLine($"    {name},");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} {label}（设备族 {dut}）被检{(commands.Count > 0 ? "命令" : "设备")}接口。**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2</c>");
        sb.AppendLine("/// （旧平台驱动，内部转调 Xmas11 <c>DPG2SCPI</c>，返回 <c>iResponse</c>）。");
        sb.AppendLine("/// 读值方法返回值、通讯/执行失败抛 <see cref=\"DeviceCommException\"/>（由引擎按异常收尾并落盘）。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public interface I{dut}Dut : IDutDevice");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 补充连接（重连），返回是否已连接。PORT: 旧 ConST221_2.ReplenishLink（针床设备逻辑简化为直接建连）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    /// <returns>是否连接成功。</returns>");
        sb.AppendLine("    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);");
        sb.AppendLine();
        if (commands.Count > 0)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 执行一条" + label + "测试 SCPI 指令（无回值）。PORT: 旧 " + dut + "_2.ExecuteAnyCommand_NoResponse。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"command\">指令（电源开/关、RTC/铁电/FLASH 自检等）。</param>");
            sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
            sb.AppendLine($"    Task ExecuteAnyCommandNoResponseAsync({dut}Command command, CancellationToken ct = default);");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildConST811AInterfaceSource(string bizType)
    {
        var label = ReferencesAdapter.BizLabel(bizType);
        return """
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>ConST811A {label}被检设备的真实 APC2 通讯契约。</summary>
public interface IConST811ADut : IDutDevice
{{
    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);
    Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<bool> SetPrimaryDeviceTypeAsync(string deviceType, CancellationToken ct = default);
    Task CommandAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default);
    Task<string> QueryTextAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default);
    Task<double> QueryDoubleAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default);
    Task<bool> QueryBooleanAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default);
}}
""".Replace("{{", "{", StringComparison.Ordinal)
            .Replace("}}", "}", StringComparison.Ordinal)
            .Replace("{label}", label, StringComparison.Ordinal);
    }

    private static string BuildDriverSource(string dut, IReadOnlyList<(string Name, string Scpi)> commands, string bizType)
    {
        var label = ReferencesAdapter.BizLabel(bizType);
        var sb = new StringBuilder();
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.IO.Ports;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using TESTRIG.Core.Abstractions;");
        sb.AppendLine("using TESTRIG.Devices.Abstractions;");
        sb.AppendLine("using Xmas11.Comm.Data.Common;");
        sb.AppendLine("using Xmas11.Comm.Devices;");
        sb.AppendLine("using Xmas11.Comm.Devices.DPG2;");
        sb.AppendLine();
        sb.AppendLine($"namespace TESTRIG.Devices.Dut.{dut};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dut} 主板（设备族 {dut}）被检**真机驱动**：走 Xmas11 <see cref=\"DPG2SCPI\"/> 通讯库，");
        sb.AppendLine($"/// 命令层**自动转换**自旧 <c>Bots.TestBench.Device.{dut}_2</c>（内部转调 <c>DPG2SCPI.*</c>，返回 <c>iResponse</c>）。");
        sb.AppendLine("/// 连接按 manifest 号位 <see cref=\"CommEndpoint\"/> 的串口参数（波特率/停止位等）直接建连；");
        sb.AppendLine("/// 针床被检在工装准备上电后才连接（工装准备前不连接，见工装准备处理器 ReplenishLinkAsync）。");
        sb.AppendLine("/// 每条命令 <c>iResponse.IsCorrect=false</c> 即抛 <see cref=\"DeviceCommException\"/>，交引擎按异常收尾。");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[DutDriver(\"{dut}\")]");
        sb.AppendLine($"public sealed class {dut}Dut : I{dut}Dut");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>日志。</summary>");
        sb.AppendLine("    private readonly ILogger _logger;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>连接端点（号位 Comm）。</summary>");
        sb.AppendLine("    private readonly CommEndpoint? _comm;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>{dut} 通讯实例（连接后有值）。</summary>");
        sb.AppendLine("    private DPG2SCPI? _dev;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>设备键。</summary>");
        sb.AppendLine("    public string Key { get; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>设备型号名。</summary>");
        sb.AppendLine("    public string Model { get; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>是否已连接。</summary>");
        sb.AppendLine("    public bool IsConnected { get; private set; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 取 {dut} 实例，未连接抛 <see cref=\"DeviceCommException\"/>（CommunicationError）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    private DPG2SCPI Dev => _dev ?? throw new DeviceCommException(\"{dut} 未连接\", TestResultStatus.CommunicationError);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 用设备描述符构造真机被检（端点取号位 Comm）。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"descriptor\">设备描述符（含号位 Comm）。</param>");
        sb.AppendLine("    /// <param name=\"logger\">日志。</param>");
        sb.AppendLine($"    public {dut}Dut(DeviceDescriptor descriptor, ILogger logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("        Key = descriptor.Model;");
        sb.AppendLine("        Model = descriptor.Model;");
        sb.AppendLine("        _comm = descriptor.Comm;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 连接被检：按端点（网络/串口）建 DPG2SCPI，Open 成功即连接成功。");
        sb.AppendLine("    /// 针床被检由工装准备上电后经 <see cref=\"ReplenishLinkAsync\"/> 连接（工装准备前不连接），");
        sb.AppendLine("    /// 串口参数取 manifest 号位配置（波特率/停止位等），不做旧体系 Board 分支的覆盖/探活指令。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    public Task ConnectAsync(CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            try { _dev?.Close(); } catch { }");
        sb.AppendLine("            _dev = Build(_comm);");
        sb.AppendLine("            var opened = _dev.Open();");
        sb.AppendLine("            IsConnected = opened;");
        sb.AppendLine($"            _logger.LogInformation(IsConnected ? $\"{dut} 真机连接成功\" : $\"{dut} 连接未就绪（将重试）\");");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 按端点构造 DPG2SCPI（网络/串口）。串口参数取 manifest 号位配置（波特率/停止位/校验位），不覆盖。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ep\">连接端点。</param>");
        sb.AppendLine("    /// <returns>通讯实例。</returns>");
        sb.AppendLine("    private static DPG2SCPI Build(CommEndpoint? ep)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (ep is null || ep.Link == LinkType.Ethernet)");
        sb.AppendLine("        {");
        sb.AppendLine("            var ip = ep?.Ip ?? Environment.GetEnvironmentVariable(\"TESTRIG_DUT_IP\") ?? \"192.168.40.107\";");
        sb.AppendLine("            var port = ep?.Port ?? int.Parse(Environment.GetEnvironmentVariable(\"TESTRIG_DUT_PORT\") ?? \"1030\", CultureInfo.InvariantCulture);");
        sb.AppendLine("            return new DPG2SCPI(IPAddress.Parse(ip), port);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (ep.Link == LinkType.Serial)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sp = ep.Serial ?? new SerialParams();");
        sb.AppendLine("            var portName = string.IsNullOrWhiteSpace(ep.PhysicalLink) ? \"COM1\" : ep.PhysicalLink!;");
        sb.AppendLine("            var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;");
        sb.AppendLine("            var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;");
        sb.AppendLine("            return new DPG2SCPI(portName, sp.Baud, sp.DataBits, stopBits, parity);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        throw new DeviceCommException(\"{dut} 不支持 USB 连接（旧平台默认串口扫描）\", TestResultStatus.CommunicationError);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 补充连接（重连）。PORT: 旧 ConST221_2.ReplenishLink。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    /// <returns>是否连接成功。</returns>");
        sb.AppendLine("    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        await ConnectAsync(ct);");
        sb.AppendLine("        return IsConnected;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 执行一条{label}测试 SCPI 指令（无回值）。PORT: 旧 ExecuteAnyCommand_NoResponse。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"command\">指令。</param>");
        sb.AppendLine("    /// <param name=\"ct\">取消令牌。</param>");
        sb.AppendLine("    // 无 SimpleCommands 字典（如 P27CommonBase 走 Xmas11 API）时不生成命令执行方法，由人工按设备 API 补充");
        if (commands.Count > 0)
        {
        sb.AppendLine($"    public Task ExecuteAnyCommandNoResponseAsync({dut}Command command, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var scpi = command switch");
        sb.AppendLine("            {");
        foreach (var (name, scpi) in commands)
            sb.AppendLine($"                {dut}Command.{name} => \"{scpi}\",");
        sb.AppendLine($"                _ => throw new DeviceCommException($\"未知指令 {{command}}\", TestResultStatus.HardwareError),");
        sb.AppendLine("            };");
        sb.AppendLine("            var res = Dev.ExecuteAnyCommand_NoResponse(scpi);");
        sb.AppendLine("            if (!res.IsCorrect)");
        sb.AppendLine($"                throw new DeviceCommException($\"执行指令 {{command}} 失败\", TestResultStatus.HardwareError);");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        }
        sb.AppendLine();
        sb.AppendLine("    // ===== IDutDevice 必需实现 ===== ");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读整机序列号。PORT: DPG2.GetSerialNumber。</summary>");
        sb.AppendLine("    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)");
        sb.AppendLine("        => Str(() => Dev.GetSerialNumber(), \"读取SN\", ct);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>读固件版本。PORT: DPG2.GetVersion。</summary>");
        sb.AppendLine("    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)");
        sb.AppendLine("        => Str(() => Dev.GetVersion(), \"读取版本\", ct);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>写板卡类型/初始信息（旧体系无对应命令，留空）。</summary>");
        sb.AppendLine("    public Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)");
        sb.AppendLine("        => Task.CompletedTask;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>按测量点名测量（旧体系无通用测量入口，返回 0）。</summary>");
        sb.AppendLine("    public Task<double> MeasureAsync(string point, CancellationToken ct = default)");
        sb.AppendLine("        => Task.FromResult(0d);");
        sb.AppendLine();
        sb.AppendLine("    // ===== iResponse 包装：失败抛 DeviceCommException =====");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>执行一条返回字符串的命令，失败抛通讯异常。</summary>");
        sb.AppendLine("    private Task<string> Str(Func<iResponse<string>> call, string what, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return Task.Run(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var r = call();");
        sb.AppendLine("            if (!r.IsCorrect)");
        sb.AppendLine("            throw new DeviceCommException($\"{what}失败\", TestResultStatus.CommunicationError);");
        sb.AppendLine("            return r.Result;");
        sb.AppendLine("        }, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>释放连接。</summary>");
        sb.AppendLine("    public ValueTask DisposeAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        try { _dev?.Close(); } catch { }");
        sb.AppendLine("        _dev = null;");
        sb.AppendLine("        IsConnected = false;");
        sb.AppendLine("        return ValueTask.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildConST811ADriverSource(string dut, string bizType)
    {
        return """
using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using Xmas11.Comm.Devices;

namespace TESTRIG.Devices.Dut.ConST811A;

/// <summary>ConST811A 被检真机驱动，使用 Xmas11 APC2Device。</summary>
[DutDriver("ConST811A")]
public sealed class ConST811ADut : IConST811ADut
{
    private readonly ILogger _logger;
    private readonly CommEndpoint? _comm;
    private APC2Device? _dev;

    public ConST811ADut(DeviceDescriptor descriptor, ILogger logger)
    {
        Key = descriptor.Name;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
        _logger = logger;
    }

    public string Key { get; }
    public string Model { get; }
    public bool IsConnected { get; private set; }
    private APC2Device Dev => _dev ?? throw Error("ConST811A 未连接", TestResultStatus.CommunicationError);

    public Task ConnectAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        try { _dev?.Close(); } catch { }
        _dev = Build(_comm);
        IsConnected = _dev.Open();
        if (!IsConnected)
            throw Error("ConST811A 连接失败", TestResultStatus.CommunicationError);
        _logger.LogInformation("P21/ConST811A 连接成功");
    }, ct);

    private static APC2Device Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet)
            return new APC2Device(IPAddress.Parse(ep?.Ip ?? "192.168.40.107"), ep?.Port ?? 8000);
        if (ep.Link == LinkType.Usb)
            return new APC2Device((ushort)(ep.Vid ?? 0x2E19), (ushort)(ep.Pid ?? 0x02F9), ep.PhysicalLink ?? "");
        if (ep.Link == LinkType.Serial)
        {
            var sp = ep.Serial ?? new SerialParams();
            var stop = Enum.TryParse<StopBits>(sp.StopBits, true, out var sb) ? sb : StopBits.One;
            var parity = Enum.TryParse<Parity>(sp.Parity, true, out var pa) ? pa : Parity.None;
            return new APC2Device(ep.PhysicalLink ?? "COM1", sp.Baud, sp.DataBits, stop, parity);
        }
        throw Error($"ConST811A 不支持连接类型 {ep.Link}", TestResultStatus.CommunicationError);
    }

    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)
    {
        await ConnectAsync(ct);
        return IsConnected;
    }

    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)
        => QueryTextAsync("GetSerialNumber", null, ct);

    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)
        => QueryTextAsync("GetVersion", null, ct);

    public async Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        await CommandAsync("SetSerialNumber", [serialNumber], ct);
        return string.Equals(await ReadSerialNumberAsync(ct), serialNumber, StringComparison.Ordinal);
    }

    public async Task<bool> SetPrimaryDeviceTypeAsync(string deviceType, CancellationToken ct = default)
    {
        await CommandAsync("SetPrimaryDevType", [deviceType], ct);
        return string.Equals(await QueryTextAsync("GetDevType", null, ct), deviceType, StringComparison.OrdinalIgnoreCase);
    }

    public async Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(boardType))
            throw Error("设备类型不能为空", TestResultStatus.HardwareError);
        if (!await SetPrimaryDeviceTypeAsync(boardType, ct))
            throw Error("设备类型写入后回读不一致", TestResultStatus.HardwareError);
    }

    public Task<double> MeasureAsync(string point, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(point))
            throw Error("测量点不能为空", TestResultStatus.HardwareError);
        return QueryDoubleAsync(point, null, ct);
    }

    public Task CommandAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default)
        => Task.Run(() => { _ = Invoke(operation, arguments); }, ct);

    public Task<string> QueryTextAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default)
        => Task.Run(() => Convert.ToString(Invoke(operation, arguments), CultureInfo.InvariantCulture) ?? "", ct);

    public Task<double> QueryDoubleAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default)
        => Task.Run(() => ToDouble(Invoke(operation, arguments), operation), ct);

    public Task<bool> QueryBooleanAsync(string operation, IReadOnlyList<string>? arguments = null, CancellationToken ct = default)
        => Task.Run(() => ToBoolean(Invoke(operation, arguments), operation), ct);

    private object? Invoke(string operation, IReadOnlyList<string>? arguments)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw Error("APC2 操作名不能为空", TestResultStatus.HardwareError);

        operation = NormalizeOperation(operation);

        var separator = operation.IndexOf('|');
        var methodName = separator < 0 ? operation : operation[..separator];
        var selector = separator < 0 ? null : operation[(separator + 1)..];
        var sourceArgs = arguments?.ToArray() ?? [];
        var candidates = typeof(APC2Device).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals(methodName, StringComparison.Ordinal) && m.GetParameters().Length == sourceArgs.Length)
            .ToArray();

        Exception? last = null;
        foreach (var method in candidates)
        {
            try
            {
                var parameters = method.GetParameters();
                var converted = new object?[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                    converted[i] = ConvertArgument(sourceArgs[i], parameters[i].ParameterType);
                var response = method.Invoke(Dev, converted);
                var result = UnwrapResponse(response, methodName);
                return Select(result, selector);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                last = ex.InnerException;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        if (sourceArgs.Length == 0 && TryGetDefaultArguments(methodName, out var defaults))
            return Invoke(methodName, defaults);

        var detail = last is null ? "未找到参数匹配的方法" : last.Message;
        throw Error($"APC2 操作 {methodName} 失败：{detail}", TestResultStatus.HardwareError);
    }

    private static bool TryGetDefaultArguments(string methodName, out IReadOnlyList<string> arguments)
    {
        arguments = methodName switch
        {
            "SetCheckerOpen" or "SetCheckerSelect" or "GetCheckerState" => new[] { "KeyBoard" },
            "GetPressureModelStableState" => new[] { "1" },
            "SetPressureModelStableParam" => new[] { "1", "1", "5" },
            "SetModuleStableEnable" => new[] { "InnerModule_H", "false" },
            "SetPressureModelUnit" => new[] { "ControllerModule", "kPa" },
            "SetPressureControlMode" => new[] { "MEASURE" },
            "SetPressureModelPressureType" => new[] { "ControllerModule", "Absolute" },
            "SetBlueToothState" or "SetWifiState" => new[] { "Open" },
            "SetSetPointLimitState" => new[] { "Close" },
            _ => Array.Empty<string>(),
        };
        return arguments.Count > 0;
    }

    private static string NormalizeOperation(string operation)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GetDUTSN"] = "GetSerialNumber",
            ["GetVersion"] = "GetVersion_Application",
            ["GetAtmosSensor"] = "GetAtmos",
            ["GetBatteryValue"] = "GetBATTery",
            ["GetValveStateValue"] = "GetSwitchValveState",
            ["SetValveStata"] = "SetSwitchValveState",
            ["SetTargetPressure"] = "SetTargetPressureValue",
            ["GetPressureStableState"] = "GetPressureModelStableState",
            ["GetAtmosSensorSN"] = "GetAtmosSensorSN",
            ["OpenBlueTooth"] = "SetBlueToothState",
            ["CloseBlueTooth"] = "SetBlueToothState",
            ["OpenWLANFunction"] = "SetWifiState",
            ["CloseWLANFunction"] = "SetWifiState",
            ["OpenPressureModelLimit"] = "SetSetPointLimitState",
            ["CloseSetPointLimitPressureRange"] = "SetSetPointLimitState"
        };
        var separator = operation.IndexOf('|');
        var method = separator < 0 ? operation : operation[..separator];
        return aliases.TryGetValue(method, out var mapped)
            ? mapped + (separator < 0 ? "" : operation[separator..])
            : operation;
    }

    private static object? ConvertArgument(string value, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target == typeof(string)) return value;
        if (target == typeof(bool)) return bool.Parse(value);
        if (target == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (target == typeof(short)) return short.Parse(value, CultureInfo.InvariantCulture);
        if (target == typeof(ushort)) return ushort.Parse(value, CultureInfo.InvariantCulture);
        if (target == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
        if (target == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
        if (target.IsEnum)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                return Enum.ToObject(target, number);
            return Enum.Parse(target, value, true);
        }
        return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    private static object? UnwrapResponse(object? response, string operation)
    {
        if (response is null) return null;
        var type = response.GetType();
        var correct = type.GetProperty("IsCorrect", BindingFlags.Public | BindingFlags.Instance);
        if (correct?.PropertyType == typeof(bool) && !(bool)(correct.GetValue(response) ?? false))
            throw Error($"APC2 操作 {operation} 返回失败：{response}", TestResultStatus.HardwareError);
        var result = type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
        return result is null ? response : result.GetValue(response);
    }

    private static object? Select(object? value, string? selector)
    {
        if (value is null || string.IsNullOrWhiteSpace(selector)) return value;
        foreach (var member in selector.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var property = value.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw Error($"响应 {value.GetType().Name} 没有属性 {member}", TestResultStatus.HardwareError);
            value = property.GetValue(value);
            if (value is null) break;
        }
        return value;
    }

    private static double ToDouble(object? value, string operation)
    {
        if (value is null) throw Error($"APC2 操作 {operation} 没有返回值", TestResultStatus.HardwareError);
        if (value is IConvertible convertible)
        {
            try { return convertible.ToDouble(CultureInfo.InvariantCulture); } catch { }
        }
        var property = value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null) return ToDouble(property.GetValue(value), operation);
        throw Error($"APC2 操作 {operation} 返回值 {value.GetType().Name} 不能转换为数值", TestResultStatus.HardwareError);
    }

    private static bool ToBoolean(object? value, string operation)
    {
        if (value is bool b) return b;
        if (value is null) throw Error($"APC2 操作 {operation} 没有返回值", TestResultStatus.HardwareError);
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (bool.TryParse(text, out var parsed)) return parsed;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return number != 0;
        return text.Equals("Open", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Pass", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Passed", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Connected", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Online", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Stable", StringComparison.OrdinalIgnoreCase);
    }

    private static DeviceCommException Error(string message, TestResultStatus status) => new(message, status);

    public ValueTask DisposeAsync()
    {
        try { _dev?.Close(); } catch { }
        _dev = null;
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
""";
    }
}
