using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WPFCli.Engine;

/// <summary>
/// References manifest 生成器 + Phase3 编排 —— 解析旧 <c>Jigs\*.json</c>（Bots.TestBench 任务配置），
/// 按变体生成新体系两份产物：
///   1) <c>TESTRIG.TestSteps\{dut}\{dut}_{后缀}\{dut}_{后缀}.cs</c>（处理器源码，委托 <see cref="TestStepSourceGenerator"/>）
///   2) <c>TESTRIG.Jigs\Manifests\{dut}\{dut}_{后缀}.json</c>（新 manifest 格式）
/// 任务序列以 JSON <c>Location.Entry</c> 为权威来源；旧人工项（ManualTestItem）转 Manual 步。
/// </summary>
/// <summary>旧 Jigs 配置中单个测试任务（Location.Entry 为权威来源）。</summary>
internal sealed record JigTask(string Entry, string Name, string Description, string Guid,
    List<(string Name, string Value, string? Unit)> Parameters, List<(string Name, double Min, double Max, string Unit)> Conditions,
    string Categories, bool IsManual);

/// <summary>旧 Jigs 配置中的变体（TestCategoriesItems 的分类项）。</summary>
internal sealed record JigVariant(string Value, string Name);

internal static class ReferencesManifestBuilder
{
    internal static (List<string> handlerFiles, List<string> manifestFiles, List<string> todos) GenerateTestStepsAndManifest(
        string stepsFile, string jigFile, string dut, string outputDir, string bizType)
    {
        var suffix = ReferencesAdapter.BizSuffix(bizType);
        var todos = new List<string>();
        var script = File.ReadAllText(stepsFile);

        // 解析旧 JSON（允许注释/尾逗号）
        using var doc = JsonDocument.Parse(
            File.ReadAllBytes(jigFile),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;

        // 任务序列（Entry 为权威来源）。无 Entry 的旧人工项也保留，转成新体系 ManualConfirm。
        var tasks = new List<JigTask>();
        if (root.TryGetProperty("TaskCollection", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tc.EnumerateArray())
            {
                var entry = GetString(item, "Location", "Entry");
                var isManual = item.TryGetProperty("$type", out var tt) &&
                               tt.ValueKind == JsonValueKind.String && tt.GetString()?.Contains("ManualTestItem") == true;
                if (string.IsNullOrWhiteSpace(entry))
                {
                    if (!isManual) continue;
                    entry = "Manual_" + (GetGuid(item).Replace("-", "", StringComparison.Ordinal));
                }
                // 旧体系人工项（ManualTestItem，如整机供电测试）转新体系 Manual 步（引擎弹确认框）
                tasks.Add(new JigTask(
                    entry,
                    GetString(item, "Name") ?? entry,
                    GetString(item, "TestDesc") ?? "",
                    GetGuid(item),
                    ReadParameters(item),
                    ReadConditions(item),
                    GetString(item, "Categories") ?? "",
                    isManual));
            }
        }

        var variants = ReadVariants(root);
        var handlerFiles = new List<string>();
        var manifestFiles = new List<string>();
        foreach (var variant in variants)
        {
            // 旧版动态工装脚本经常没有 P21_D 分类（只有 Type/设备分类），
            // 此时不能把全部任务误过滤掉；存在明确分类时才按版本筛选。
            var hasP21Categories = tasks.Any(t => t.Categories.StartsWith("P21_D", StringComparison.OrdinalIgnoreCase));
            var variantTasks = hasP21Categories
                ? tasks.Where(t =>
                    t.Categories.Equals("P21_D", StringComparison.OrdinalIgnoreCase) ||
                    t.Categories.Equals($"P21_D_{variant.Value}", StringComparison.OrdinalIgnoreCase)).ToList()
                : tasks.ToList();
            if (variantTasks.Count == 0)
            {
                todos.Add($"分类 {variant.Value}：没有匹配到测试项");
                continue;
            }

            var variantSuffix = variant.Value.Equals("Default", StringComparison.OrdinalIgnoreCase)
                ? suffix
                : $"{variant.Value}_{suffix}";
            // 每个版本使用独立 DeviceFamily，避免四份独立产品的同名 Kind 在运行时冲突。
            // Dut.Model 仍统一为实际设备族名称（ConST811A），结果查询不受版本目录影响。
            var handlerDir = Path.Combine(outputDir, "src", "04.TestSteps", "TESTRIG.TestSteps", dut, $"{dut}_{variantSuffix}");
            var manifestDir = Path.Combine(outputDir, "src", "05.Jigs", "TESTRIG.Jigs", "Manifests", dut);
            Directory.CreateDirectory(handlerDir);
            Directory.CreateDirectory(manifestDir);
            var handlerRel = Path.Combine("src", "04.TestSteps", "TESTRIG.TestSteps", dut, $"{dut}_{variantSuffix}", $"{dut}_{variantSuffix}.cs");
            var manifestRel = Path.Combine("src", "05.Jigs", "TESTRIG.Jigs", "Manifests", dut, $"{dut}_{variantSuffix}.json");
            ReferencesAdapter.WriteIfNotExists(Path.Combine(outputDir, handlerRel),
                () => TestStepSourceGenerator.BuildHandlerSource(script, variantTasks, dut, variantSuffix,
                    variant.Value.Equals("Default", StringComparison.OrdinalIgnoreCase) ? dut : $"{dut}_{variantSuffix}", todos));
            ReferencesAdapter.WriteIfNotExists(Path.Combine(outputDir, manifestRel),
                () => BuildManifestSource(root, variantTasks, dut, bizType, variant));
            handlerFiles.Add(handlerRel);
            manifestFiles.Add(manifestRel);
        }

        return (handlerFiles, manifestFiles, todos);
    }

    private static List<JigVariant> ReadVariants(JsonElement root)
    {
        var result = new List<JigVariant>();
        if (root.TryGetProperty("Type", out var type) &&
            type.TryGetProperty("TestCategoriesItems", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var value = GetString(item, "Value");
                var name = GetString(item, "Name");
                if (!string.IsNullOrWhiteSpace(value) && !value.Equals("P21_D", StringComparison.OrdinalIgnoreCase))
                    result.Add(new JigVariant(value, string.IsNullOrWhiteSpace(name) ? value : name));
            }
        }

        return result.Count > 0
            ? result
            : [new JigVariant("Default", "默认")];
    }

    private static string? GetString(JsonElement obj, params string[] path)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        // 调用点同时使用了两种旧 JSON 形态：("Location","Entry") 表示嵌套，
        // ("IP","Ip","Address")/ ("Bauds","Baud") 表示候选字段。兼容两者。
        if (path.Length > 0 && obj.TryGetProperty(path[0], out var first))
        {
            if (first.ValueKind == JsonValueKind.String) return first.GetString();
            if (first.ValueKind == JsonValueKind.Object)
            {
                for (var i = 1; i < path.Length; i++)
                {
                    if (!first.TryGetProperty(path[i], out var nested)) continue;
                    return nested.ValueKind == JsonValueKind.String ? nested.GetString() : nested.ToString();
                }
                return first.ToString();
            }
            return first.ToString();
        }
        for (var i = 1; i < path.Length; i++)
        {
            if (!obj.TryGetProperty(path[i], out var candidate)) continue;
            return candidate.ValueKind == JsonValueKind.String ? candidate.GetString() : candidate.ToString();
        }
        return null;
    }

    private static string GetGuid(JsonElement item)
    {
        var g = GetString(item, "GUID");
        return string.IsNullOrWhiteSpace(g) ? Guid.NewGuid().ToString() : g;
    }

    private static List<(string Name, string Value, string? Unit)> ReadParameters(JsonElement item)
    {
        var list = new List<(string, string, string?)>();
        if (!item.TryGetProperty("Parameters", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var p in arr.EnumerateArray())
        {
            var name = GetString(p, "Name") ?? "";
            var value = GetString(p, "Value") ?? "";
            var unit = GetString(p, "Unit");
            if (name.Length > 0) list.Add((name, value, unit));
        }
        return list;
    }

    private static List<(string Name, double Min, double Max, string Unit)> ReadConditions(JsonElement item)
    {
        var list = new List<(string, double, double, string)>();
        if (!item.TryGetProperty("Conditions", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var c in arr.EnumerateArray())
        {
            var name = GetString(c, "Name") ?? "";
            if (name.Length == 0) continue;
            var min = c.TryGetProperty("Lower", out var lo) && lo.ValueKind == JsonValueKind.Number && lo.TryGetDouble(out var lv) ? lv : 0;
            var max = c.TryGetProperty("Upper", out var hi) && hi.ValueKind == JsonValueKind.Number && hi.TryGetDouble(out var hv) ? hv : 0;
            var unit = GetString(c, "Unit") ?? "";
            // 旧 ValueCondition（Value + Operator，如 "泄漏指标 ≤100"）→ Range 语义：<= 取上限，>= 取下限，否则取等值。
            // Value 可能是 Number 或字符串（"100"/"0.5"）；TryGetDouble 对非 Number 会抛，须先按 ValueKind 处理。
            if (min == 0 && max == 0 && c.TryGetProperty("Value", out var v))
            {
                double val = 0;
                if (v.ValueKind == JsonValueKind.Number)
                {
                    val = v.GetDouble();
                }
                else if (v.ValueKind == JsonValueKind.String &&
                         double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sv))
                {
                    val = sv;
                }
                var op = GetString(c, "Operator") ?? "";
                if (val != 0 || op.Length > 0)
                {
                    if (op.Contains("<=") || op == "<") max = val;
                    else if (op.Contains(">=") || op == ">") min = val;
                    else { min = val; max = val; }
                }
            }
            list.Add((name, min, max, unit));
        }
        return list;
    }

    private static string BuildManifestSource(JsonElement root, IReadOnlyList<JigTask> tasks, string dut, string bizType, JigVariant variant)
    {
        var suffix = ReferencesAdapter.BizSuffix(bizType);
        var label = ReferencesAdapter.BizLabel(bizType);
        var boardName = $"{variant.Name}{label}测试";
        var deviceName = $"{dut} 被检";
        var variantSuffix = variant.Value.Equals("Default", StringComparison.OrdinalIgnoreCase)
            ? suffix
            : $"{variant.Value}_{suffix}";
        var productModel = ReferencesAdapter.ProductModelForVariant(variant.Value);

        // 旧 JSON 的设备顺序并不可靠：按 DeviceType/DeviceKey 找 DUT 与共享设备，且始终取首个通讯配置。
        JsonElement? dutDevice = null;
        var toolDevices = new List<object>();
        if (root.TryGetProperty("Devices", out var devices) && devices.ValueKind == JsonValueKind.Array)
        {
            foreach (var dev in devices.EnumerateArray())
            {
                var deviceType = GetString(dev, "DeviceType") ?? "";
                var key = GetString(dev, "DeviceKey") ?? "";
                if (deviceType.Equals("DUT", StringComparison.OrdinalIgnoreCase) &&
                    key.Equals("P21", StringComparison.OrdinalIgnoreCase))
                {
                    dutDevice = dev;
                    deviceName = GetString(dev, "DeviceName") ?? deviceName;
                    continue;
                }
                if (!deviceType.Equals("Tool", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(key)) continue;
                var firstConfig = FirstCommConfig(dev);
                var toolComm = BuildCommEndpoint(firstConfig);
                // 型号取旧 $type 的类名（如 Bots.TestBench.Device.DPSEX → DPSEX），标准模块注册表按此匹配 [DutDriver]
                var typeName = GetString(dev, "$type") ?? "";
                var model = typeName.Split(',')[0].Trim().Split('.').LastOrDefault();
                if (string.IsNullOrWhiteSpace(model)) model = key;
                var toolEntry = new Dictionary<string, object?>
                {
                    ["Key"] = key,
                    ["Name"] = GetString(dev, "DeviceName") ?? key,
                    ["Model"] = model,
                    ["Comm"] = toolComm,
                };
                var toolSerialNumber = GetString(firstConfig, "DevSn");
                if (!string.IsNullOrWhiteSpace(toolSerialNumber))
                {
                    toolEntry["SerialNumber"] = toolSerialNumber;
                }
                toolDevices.Add(toolEntry);
            }
        }

        if (dutDevice is null && root.TryGetProperty("Devices", out var fallbackDevices) && fallbackDevices.ValueKind == JsonValueKind.Array)
        {
            // 兼容旧 References：部分动态工装仍使用 P22/P27 等号位名称；只要明确标记为 DUT，
            // 就以首个 DUT 作为被检设备，生成后的 Model 仍统一为实际 dut。
            foreach (var dev in fallbackDevices.EnumerateArray())
            {
                if ((GetString(dev, "DeviceType") ?? "").Equals("DUT", StringComparison.OrdinalIgnoreCase))
                {
                    dutDevice = dev;
                    deviceName = GetString(dev, "DeviceName") ?? deviceName;
                    break;
                }
            }
        }
        if (dutDevice is null)
            throw new InvalidOperationException("References 设备配置缺少 DeviceType=DUT 的被检设备");
        var dutComm = BuildCommEndpoint(FirstCommConfig(dutDevice.Value));

        var json = new Dictionary<string, object?>
        {
            ["Key"] = $"{dut}_{variantSuffix}",
            ["DeviceFamily"] = $"{dut}_{variantSuffix}",
            ["BoardName"] = boardName,
            ["Description"] = $"{dut} {variant.Name}{label}测试（自动转换自 References\\{bizType}\\{dut}\\Jigs）",
            ["Dut"] = new Dictionary<string, object?>
            {
                ["Name"] = deviceName,
                ["Model"] = dut,
                ["ProductModel"] = productModel,
                ["Comm"] = dutComm,
            },
            // 标准模块（Tool 设备）多实例：处理器按 DeviceKey 用 GetDevice<T>(key) 获取（如 DPSEX1/DPSEX2）
            ["ToolDevices"] = toolDevices.ToArray(),
            ["Positions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["Index"] = 1,
                    ["Name"] = "1号位",
                    ["Comm"] = dutComm,
                 },
            },
            ["Steps"] = tasks.Select(t => (object)new Dictionary<string, object?>
            {
                ["Key"] = t.Entry,
                ["Kind"] = t.Entry,
                ["Name"] = t.Name,
                ["Description"] = t.Description,
                ["Settings"] = new Dictionary<string, object?> { ["ProductModel"] = productModel },
                // 旧 ManualTestItem → 新 Manual 步（引擎弹人工确认框，60s 超时）
                ["StepType"] = t.IsManual || t.Entry.Equals("TestCPS", StringComparison.OrdinalIgnoreCase) ? "Manual" : "Auto",
                ["TimeoutMs"] = t.IsManual || t.Entry.Equals("TestCPS", StringComparison.OrdinalIgnoreCase) ? 60000 : 0,
                ["Parameters"] = t.Parameters.Select(p => (object)new Dictionary<string, object?>
                {
                    ["Name"] = p.Name,
                    ["Value"] = p.Value,
                    ["Unit"] = p.Unit,
                }).ToArray(),
                ["Conditions"] = t.Conditions.Select(c => (object)new Dictionary<string, object?>
                {
                    ["Kind"] = "Range",
                    ["Name"] = c.Name,
                    ["Min"] = c.Min,
                    ["Max"] = c.Max,
                    ["Unit"] = c.Unit,
                }).ToArray(),
                ["Guid"] = t.Guid,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(json, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static JsonElement FirstCommConfig(JsonElement device)
    {
        if (device.TryGetProperty("CommConfigs", out var configs) &&
            configs.ValueKind == JsonValueKind.Array && configs.GetArrayLength() > 0)
            return configs[0];
        return default;
    }

    private static Dictionary<string, object?> BuildCommEndpoint(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?> { ["Link"] = "Ethernet" };
        var type = GetString(config, "$type") ?? "";
        if (type.Contains("USB", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>
            {
                ["Link"] = "Usb",
                ["PhysicalLink"] = "",
                ["Vid"] = ParseHexInt(GetString(config, "Vid")),
                ["Pid"] = ParseHexInt(GetString(config, "Pid")),
            };
        }
        if (type.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
        {
            var result = new Dictionary<string, object?> { ["Link"] = "Ethernet" };
            var ip = GetString(config, "IP", "Ip", "Address");
            if (!string.IsNullOrWhiteSpace(ip)) result["Ip"] = ip;
            if (int.TryParse(GetString(config, "Port"), out var port)) result["Port"] = port;
            return result;
        }
        return new Dictionary<string, object?>
        {
            ["Link"] = "Serial",
            ["PhysicalLink"] = GetString(config, "Name", "PortName") ?? "",
            ["Serial"] = new Dictionary<string, object?>
            {
                ["Baud"] = int.TryParse(GetString(config, "Bauds", "Baud"), out var baud) ? baud : 9600,
                ["DataBits"] = int.TryParse(GetString(config, "DataBits"), out var bits) ? bits : 8,
                ["StopBits"] = GetString(config, "StopBits") ?? "One",
                ["Parity"] = GetString(config, "Parity") ?? "None",
            },
        };
    }

    private static int? ParseHexInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : null;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : null;
    }
}
