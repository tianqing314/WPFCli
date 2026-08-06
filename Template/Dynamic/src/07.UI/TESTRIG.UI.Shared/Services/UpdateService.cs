using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 升级清单（服务器 stable/latest.json，字段见 docs/在线升级方案.md §4.2）。
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>新版本号（x.y.z）。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>包文件名（与清单同目录）。</summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    /// <summary>包字节数。</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>包 SHA-256（十六进制小写）。</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    /// <summary>发布时间。</summary>
    [JsonPropertyName("publishTime")]
    public DateTimeOffset PublishTime { get; set; }

    /// <summary>是否强制更新。</summary>
    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    /// <summary>低于此版本按强更处理（空 = 不限制）。</summary>
    [JsonPropertyName("minVersion")]
    public string? MinVersion { get; set; }

    /// <summary>更新说明。</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
}

/// <summary>
/// 升级本地配置（Config/update.json，随机部署、升级不覆盖，技术员可手改服务器地址）。
/// </summary>
public sealed class UpdateConfig
{
    /// <summary>分发根地址（客户端拼 /stable/latest.json）。</summary>
    [JsonPropertyName("serverBase")]
    public string ServerBase { get; set; } = "http://file.gdz.const.cc:8090/pcba-ats";

    /// <summary>是否自动检查更新。</summary>
    [JsonPropertyName("autoCheck")]
    public bool AutoCheck { get; set; } = true;

    /// <summary>自动检查间隔（小时）。</summary>
    [JsonPropertyName("checkIntervalHours")]
    public int CheckIntervalHours { get; set; } = 4;
}

/// <summary>
/// 本机已装版本记录（Config/installed.json，升级不覆盖）。记录最近一次经升级器装入的版本与其升级包 SHA-256，
/// 用于「同版本号内容变更」检测：服务器同版本号重传（内容变、哈希变）时也能识别为需升级。
/// </summary>
public sealed class InstalledState
{
    /// <summary>已装版本号（应与当前运行装配版本一致，不一致则哈希不可信）。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>已装升级包 SHA-256（十六进制小写）。</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

/// <summary>
/// 在线升级服务：读取清单、比较版本、下载校验、拉起升级器/回滚（对应 docs/在线升级方案.md 方案 A）。
/// 单例注册于 App；UI 触点（红点/对话框/菜单）由 MainViewModel 消费。
/// </summary>
public sealed class UpdateService
{
    /// <summary>
    /// 升级器文件名（随主程序分发，升级时拷到 %TEMP% 运行）。
    /// </summary>
    private const string UpdaterExe = "TESTRIG.Updater.exe";

    /// <summary>
    /// 主程序文件名（升级器换目录后要重启的目标）。
    /// </summary>
    private const string AppExe = "TESTRIG.App.exe";

    /// <summary>
    /// 共享 HttpClient（清单 10s 超时在调用处用 CTS 控制，下载不限时）。
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// JSON 选项（camelCase 清单，容忍大小写）。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// 程序目录（末尾无分隔符）。
    /// </summary>
    private static string AppDir => AppContext.BaseDirectory.TrimEnd('\\', '/');

    /// <summary>
    /// 本机当前版本（装配版本前三位）。
    /// </summary>
    public Version CurrentVersion { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build) : new Version(1, 0, 0);

    /// <summary>
    /// 本地升级配置（首次访问时若无文件则写出默认值供技术员修改）。
    /// </summary>
    public UpdateConfig Config { get; }

    /// <summary>
    /// 最近一次检查到的清单（null = 未检查或检查失败）。
    /// </summary>
    public UpdateManifest? Latest { get; private set; }

    /// <summary>
    /// 当前操作员（登录后由 App 写入）。随命令行传给升级器，落进 logs/update-*.log 供追溯。
    /// </summary>
    public string Operator { get; set; } = "";

    /// <summary>
    /// 是否有可用新版本：服务器版本更高，或「同版本号但内容（升级包哈希）已变」——
    /// 后者要求本机有可信的已装记录（记录版本 == 当前运行版本）且记录哈希与服务器清单哈希不同。
    /// </summary>
    public bool HasUpdate =>
        Latest is not null
        && (ParseVersion(Latest.Version) > CurrentVersion || IsSameVersionContentChanged);

    /// <summary>
    /// 同版本号内容变更：服务器版本 == 当前运行版本，且本机已装记录可信（版本相符）、
    /// 服务器清单有哈希、且与已装哈希不同。用于同版本号重传时仍能触发升级。
    /// </summary>
    private bool IsSameVersionContentChanged =>
        Latest is not null
        && ParseVersion(Latest.Version) == CurrentVersion
        && _installed is not null
        && ParseVersion(_installed.Version) == CurrentVersion
        && !string.IsNullOrWhiteSpace(Latest.Sha256)
        && !string.IsNullOrWhiteSpace(_installed.Sha256)
        && !string.Equals(_installed.Sha256, Latest.Sha256, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 当前可用更新是否为强制更新（mandatory 或本机低于 minVersion）。
    /// </summary>
    public bool IsMandatory =>
        HasUpdate && Latest is not null
        && (Latest.Mandatory
            || (!string.IsNullOrWhiteSpace(Latest.MinVersion) && CurrentVersion < ParseVersion(Latest.MinVersion!)));

    /// <summary>
    /// 是否存在可回滚的上一版本（&lt;app&gt;.backup 完整存在）。
    /// </summary>
    public bool RollbackAvailable =>
        File.Exists(Path.Combine(AppDir + ".backup", AppExe));

    /// <summary>本机已装版本记录（Config/installed.json；无记录或读失败为 null，此时不做同版本内容比对）。</summary>
    private InstalledState? _installed;

    /// <summary>
    /// 构造：加载/初始化 Config/update.json，读取 Config/installed.json 已装记录。
    /// </summary>
    public UpdateService()
    {
        var cfgPath = Path.Combine(AppDir, "Config", "update.json");
        try
        {
            if (File.Exists(cfgPath))
            {
                Config = JsonSerializer.Deserialize<UpdateConfig>(File.ReadAllText(cfgPath), JsonOpts) ?? new UpdateConfig();
            }
            else
            {
                Config = new UpdateConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(cfgPath)!);
                File.WriteAllText(cfgPath, JsonSerializer.Serialize(Config, JsonOpts));
            }
        }
        catch
        {
            Config = new UpdateConfig();
        }

        try
        {
            var stPath = Path.Combine(AppDir, "Config", "installed.json");
            if (File.Exists(stPath))
            {
                _installed = JsonSerializer.Deserialize<InstalledState>(File.ReadAllText(stPath), JsonOpts);
            }
        }
        catch
        {
            _installed = null;
        }
    }

    /// <summary>
    /// 记录本机即将装入的版本与升级包哈希到 Config/installed.json（供下次同版本内容变更检测）。
    /// 写入点在拉起升级器之前——此刻已确定要装成 <see cref="Latest"/> 版；失败仅忽略、不阻断升级。
    /// </summary>
    /// <param name="m">即将装入的清单。</param>
    private void SaveInstalledState(UpdateManifest m)
    {
        try
        {
            var state = new InstalledState { Version = m.Version, Sha256 = m.Sha256 };
            var stPath = Path.Combine(AppDir, "Config", "installed.json");
            Directory.CreateDirectory(Path.GetDirectoryName(stPath)!);
            File.WriteAllText(stPath, JsonSerializer.Serialize(state, JsonOpts));
            _installed = state;
        }
        catch
        {
            // 记录失败不影响本次升级；最坏是下次同版本内容变更检测不到
        }
    }

    /// <summary>
    /// 拉取并解析服务器清单（10s 超时）。成功即更新 <see cref="Latest"/>。
    /// </summary>
    /// <returns>是否检查成功（网络/解析失败返回 false，不抛出）。</returns>
    public async Task<bool> CheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var url = $"{Config.ServerBase.TrimEnd('/')}/stable/latest.json";
            var json = (await Http.GetStringAsync(url, cts.Token)).TrimStart('﻿');
            var m = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOpts);
            if (m is null || string.IsNullOrWhiteSpace(m.Version) || string.IsNullOrWhiteSpace(m.File))
            {
                return false;
            }

            Latest = m;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 下载升级包到 &lt;app&gt;\updates\ 并做 SHA-256 校验；同名且哈希已匹配的旧包直接复用。
    /// </summary>
    /// <param name="progress">进度（0~1）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>通过校验的包完整路径。</returns>
    public async Task<string> DownloadAsync(IProgress<double>? progress, CancellationToken ct)
    {
        var m = Latest ?? throw new InvalidOperationException("尚未检查到可用更新。");
        var dir = Path.Combine(AppDir, "updates");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, m.File);

        // 上次下完没装的包：哈希对得上就复用
        if (File.Exists(path) && string.Equals(await Sha256Async(path, ct), m.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(1);
            return path;
        }

        var url = $"{Config.ServerBase.TrimEnd('/')}/stable/{Uri.EscapeDataString(m.File)}";
        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? m.Size;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(path);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0)
                {
                    progress?.Report((double)done / total);
                }
            }
        }

        var hash = await Sha256Async(path, ct);
        if (!string.Equals(hash, m.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidOperationException("升级包校验失败（SHA-256 不匹配），已删除，请重试或检查服务器包。");
        }

        return path;
    }

    /// <summary>
    /// 拉起升级器安装并退出主程序（升级器从 %TEMP% 运行以便替换程序目录内的一切）。
    /// </summary>
    /// <param name="zipPath">已校验的升级包。</param>
    public void InstallAndRestart(string zipPath)
    {
        // 记录本次装入的版本+包哈希，供下次「同版本号内容变更」检测
        if (Latest is not null)
        {
            SaveInstalledState(Latest);
        }

        var updater = StageUpdater();
        Process.Start(new ProcessStartInfo
        {
            FileName = updater,
            Arguments = $"--pid {Environment.ProcessId} --app \"{AppDir}\" --zip \"{zipPath}\" --exe {AppExe} --operator \"{OperatorArg}\"",
            // 工作目录必须在程序目录之外，否则升级器自己占着目录、换名必败
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = true,
        });
        ExitForUpdate();
    }

    /// <summary>
    /// 拉起升级器回滚到上一版本并退出主程序。
    /// </summary>
    public void RollbackAndRestart()
    {
        var updater = StageUpdater();
        Process.Start(new ProcessStartInfo
        {
            FileName = updater,
            Arguments = $"--pid {Environment.ProcessId} --app \"{AppDir}\" --rollback --exe {AppExe} --operator \"{OperatorArg}\"",
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = true,
        });
        ExitForUpdate();
    }

    /// <summary>
    /// 传给升级器的操作员参数：剔除会破坏命令行引号的字符，空则记为「未知」。
    /// </summary>
    private string OperatorArg
    {
        get
        {
            var name = (Operator ?? "").Replace("\"", "").Trim();
            return string.IsNullOrEmpty(name) ? "未知" : name;
        }
    }

    /// <summary>
    /// 把升级器拷贝到 %TEMP% 并返回其路径（程序目录随后会被整体换名）。
    /// </summary>
    /// <returns>%TEMP% 中升级器路径。</returns>
    private static string StageUpdater()
    {
        var src = Path.Combine(AppDir, UpdaterExe);
        if (!File.Exists(src))
        {
            throw new FileNotFoundException("缺少升级器 TESTRIG.Updater.exe（发布包应包含，请重新发布）。", src);
        }

        var dst = Path.Combine(Path.GetTempPath(), $"TESTRIG.Updater_{Environment.TickCount64}.exe");
        File.Copy(src, dst, true);
        return dst;
    }

    /// <summary>
    /// 以“升级中”方式退出：跳过主窗关闭确认。
    /// </summary>
    private static void ExitForUpdate()
    {
        if (Application.Current?.MainWindow is Views.MainWindow mw)
        {
            mw.CloseWithoutConfirm();
        }

        Application.Current?.Shutdown();
    }

    /// <summary>
    /// 计算文件 SHA-256（十六进制小写）。
    /// </summary>
    /// <param name="path">文件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>哈希。</returns>
    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 宽容解析版本号（解析失败按 0.0.0，永远不会误判为新版本）。
    /// </summary>
    /// <param name="text">版本文本。</param>
    /// <returns>版本。</returns>
    private static Version ParseVersion(string text)
    {
        return Version.TryParse(text.Trim().TrimStart('v', 'V'), out var v) ? v : new Version(0, 0, 0);
    }
}
