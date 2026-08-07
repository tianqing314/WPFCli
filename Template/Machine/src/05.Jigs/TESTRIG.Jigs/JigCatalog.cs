using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Jigs;

/// <summary>
/// 针床目录：扫描本工程随产物拷贝的 <c>Manifests/&lt;设备&gt;/&lt;板&gt;.json</c>，加载为强类型清单并按设备分组，
/// 供菜单"设备→板子"两级导航。仅扫本目录的 JSON，**非旧 MEF 全盘 DLL 扫描**。
/// 新增板子 = 往 Manifests 里加一份 JSON，无需任何代码。维护页可增删改清单，改后 <see cref="Reload"/> 即时生效。
/// 同时实现 <see cref="ISharedDeviceStore"/>：共享设备（标准模块）独立配置 <c>Manifests/&lt;设备&gt;/&lt;Key&gt;.shared.json</c> 的读写。
/// </summary>
public sealed class JigCatalog : ISharedDeviceStore
{
    /// <summary>
    /// 已加载的针床清单集合。
    /// </summary>
    private readonly List<JigManifest> _jigs = [];

    /// <summary>
    /// 清单 Key → 源 JSON 文件路径（保存/删除定位用，大小写不敏感）。
    /// </summary>
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<JigCatalog> _logger;

    /// <summary>
    /// Manifests 根目录（随产物拷贝到输出目录，运行时加载源）。
    /// </summary>
    private readonly string _dir;

    /// <summary>
    /// 源码 Manifests 目录（开发机上定位到 <c>src/05.Jigs/TESTRIG.Jigs/Manifests</c>；
    /// 找不到（已部署）为 null）。保存/删除同步镜像到此，令改动跨 build 存活。
    /// </summary>
    private readonly string? _sourceDir;

    /// <summary>
    /// 构造：扫描 Manifests 目录加载全部清单（单个失败不影响其余）。
    /// </summary>
    /// <param name="logger">日志。</param>
    public JigCatalog(ILogger<JigCatalog> logger)
    {
        _logger = logger;
        _dir = Path.Combine(AppContext.BaseDirectory, "Manifests");
        _sourceDir = LocateSourceManifests();
        Scan();
    }

    /// <summary>
    /// 从输出目录向上找源码 Manifests 目录（开发机）；找不到返回 null。
    /// </summary>
    /// <returns>源码 Manifests 目录或 null。</returns>
    private static string? LocateSourceManifests()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var cand = Path.Combine(d.FullName, "Dynamic", "src", "05.Jigs", "TESTRIG.Jigs", "Manifests");
            if (Directory.Exists(cand))
            {
                return cand;
            }

            d = d.Parent;
        }

        return null;
    }

    /// <summary>
    /// Manifests 根目录（维护页新增清单时的落盘根）。
    /// </summary>
    public string ManifestsDir => _dir;

    /// <summary>
    /// 扫描目录，重建清单集合与路径索引。
    /// </summary>
    private void Scan()
    {
        _jigs.Clear();
        _paths.Clear();
        if (!Directory.Exists(_dir))
        {
            _logger.LogWarning("未找到 Manifests 目录：{Dir}", _dir);
            return;
        }

        var loader = new ManifestLoader();
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
        {
            try
            {
                var jig = loader.Load(file);
                _jigs.Add(jig);
                _paths[jig.Key] = file;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载针床清单失败：{File}", file);
            }
        }
        _logger.LogInformation("已加载针床 {Count} 套，设备 {Devices} 款",
            _jigs.Count, _jigs.Select(j => j.DeviceFamily).Distinct().Count());

        // Dut.Model 必须一板一值：它既是驱动派发键，又是结果落库的 DeviceModel。
        // 撞号不会崩，但两块板的测试数据会混成一堆、查询页按型号也分不开，故启动期显式报错提醒。
        foreach (var dup in _jigs.GroupBy(j => j.Dut.Model, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            _logger.LogError("被检型号 {Model} 被 {Boards} 共用——型号须一板一值，否则测试数据混装且无法按型号查询",
                dup.Key, string.Join("、", dup.Select(j => j.Key)));
        }
    }

    /// <summary>
    /// 重新扫描 Manifests 目录（维护页增删改后调用，令菜单/目录即时反映磁盘）。
    /// </summary>
    public void Reload()
    {
        Scan();
    }

    /// <summary>
    /// 全部已加载的针床清单。
    /// </summary>
    public IReadOnlyList<JigManifest> Jigs => _jigs;

    /// <summary>
    /// 按设备分组（菜单一级=设备，二级=板）。
    /// </summary>
    /// <returns>按设备家族分组的清单。</returns>
    public IEnumerable<IGrouping<string, JigManifest>> ByDevice()
    {
        return _jigs.OrderBy(j => j.DeviceFamily).ThenBy(j => j.BoardName).GroupBy(j => j.DeviceFamily);
    }

    /// <summary>
    /// 按任务 Key 查清单（大小写不敏感）。
    /// </summary>
    /// <param name="key">任务 Key。</param>
    /// <returns>清单，未找到返回 null。</returns>
    public JigManifest? Find(string key)
    {
        return _jigs.FirstOrDefault(j => string.Equals(j.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 按 Key 取源 JSON 文件路径（不存在返回 null）。
    /// </summary>
    /// <param name="key">任务 Key。</param>
    /// <returns>文件路径或 null。</returns>
    public string? PathOf(string key)
    {
        return _paths.TryGetValue(key, out var p) ? p : null;
    }

    /// <summary>
    /// 保存清单到磁盘并重载。新建按 <c>&lt;设备&gt;/&lt;Key&gt;.json</c> 落盘；改名（Key/设备变了）则写新文件并删旧文件。
    /// </summary>
    /// <param name="manifest">要保存的清单。</param>
    /// <param name="originalKey">编辑前的原 Key（新建传 null）；用于改名时删旧文件。</param>
    public void Save(JigManifest manifest, string? originalKey)
    {
        var json = ManifestWriter.ToJson(manifest);
        var target = Path.Combine(_dir, Sanitize(manifest.DeviceFamily), Sanitize(manifest.Key) + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, json);
        MirrorWriteSource(target, json);

        // 改名/换设备：原文件路径与新目标不同 → 删旧文件（避免残留重复清单）
        if (!string.IsNullOrEmpty(originalKey)
            && _paths.TryGetValue(originalKey, out var oldPath)
            && !string.Equals(oldPath, target, StringComparison.OrdinalIgnoreCase)
            && File.Exists(oldPath))
        {
            try { File.Delete(oldPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "删除改名前旧清单失败：{Path}", oldPath); }
            MirrorDeleteSource(oldPath);
        }

        _logger.LogInformation("已保存针床清单 {Key} → {Path}", manifest.Key, target);
        Reload();
    }

    /// <summary>
    /// 共享设备配置 JSON 序列化选项（与 manifest 一致：缩进、空值省略、中文不转义、枚举按字符串）。
    /// </summary>
    private static readonly JsonSerializerOptions SharedJsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 加载某工装的共享设备（标准模块）配置。无独立配置文件返回 null（调用方回落 manifest 默认 ToolDevices）。
    /// </summary>
    /// <param name="deviceFamily">设备族。</param>
    /// <param name="key">工装 Key。</param>
    /// <returns>共享设备清单，无配置返回 null。</returns>
    public IReadOnlyList<ToolDeviceDescriptor>? Load(string deviceFamily, string key)
    {
        var path = SharedPathOf(deviceFamily, key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var cfg = JsonSerializer.Deserialize<ToolDeviceConfigFile>(File.ReadAllText(path), SharedJsonOpts);
            return cfg?.Devices ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载共享设备配置失败：{Path}", path);
            return null;
        }
    }

    /// <summary>
    /// 保存某工装的共享设备（标准模块）配置到 <c>.shared.json</c> 并镜像写回源码目录（空清单也落盘，表示显式清空）。
    /// </summary>
    /// <param name="deviceFamily">设备族。</param>
    /// <param name="key">工装 Key。</param>
    /// <param name="devices">共享设备清单。</param>
    public void Save(string deviceFamily, string key, IReadOnlyList<ToolDeviceDescriptor> devices)
    {
        var path = SharedPathOf(deviceFamily, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new ToolDeviceConfigFile { Devices = devices.ToList() }, SharedJsonOpts);
        File.WriteAllText(path, json);
        MirrorWriteSource(path, json);
        _logger.LogInformation("已保存共享设备配置 {Family}/{Key} → {Path}", deviceFamily, key, path);
    }

    /// <summary>
    /// 共享设备配置文件的落盘路径：<c>Manifests/&lt;设备族&gt;/&lt;Key&gt;.shared.json</c>（与清单同目录）。
    /// </summary>
    /// <param name="deviceFamily">设备族。</param>
    /// <param name="key">工装 Key。</param>
    /// <returns>文件路径。</returns>
    private string SharedPathOf(string deviceFamily, string key)
        => Path.Combine(_dir, Sanitize(deviceFamily), Sanitize(key) + ".shared.json");

    /// <summary>
    /// 把输出目录某清单文件的内容镜像写到源码目录同名相对路径（源码目录不存在则跳过）。
    /// </summary>
    /// <param name="outputPath">输出目录里的目标路径。</param>
    /// <param name="json">要写入的 JSON。</param>
    private void MirrorWriteSource(string outputPath, string json)
    {
        if (_sourceDir is null)
        {
            return;
        }

        try
        {
            var rel = Path.GetRelativePath(_dir, outputPath);
            var srcPath = Path.Combine(_sourceDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(srcPath)!);
            File.WriteAllText(srcPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "镜像写源码清单失败：{Path}", outputPath);
        }
    }

    /// <summary>
    /// 删除源码目录中与输出文件对应的同名清单（源码目录不存在则跳过）。
    /// </summary>
    /// <param name="outputPath">输出目录里的文件路径。</param>
    private void MirrorDeleteSource(string outputPath)
    {
        if (_sourceDir is null)
        {
            return;
        }

        try
        {
            var rel = Path.GetRelativePath(_dir, outputPath);
            var srcPath = Path.Combine(_sourceDir, rel);
            if (File.Exists(srcPath))
            {
                File.Delete(srcPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "镜像删源码清单失败：{Path}", outputPath);
        }
    }

    /// <summary>
    /// 删除清单文件并重载。
    /// </summary>
    /// <param name="key">要删除清单的 Key。</param>
    /// <returns>是否删除成功（未找到返回 false）。</returns>
    public bool Delete(string key)
    {
        if (!_paths.TryGetValue(key, out var path) || !File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        MirrorDeleteSource(path);
        _logger.LogInformation("已删除针床清单 {Key}（{Path}）", key, path);
        Reload();
        return true;
    }

    /// <summary>
    /// 清洗文件/目录名中的非法字符（替换为下划线）。
    /// </summary>
    /// <param name="name">原名。</param>
    /// <returns>合法名。</returns>
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "_" : s;
    }
}
