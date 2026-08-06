using System.Text.Json;
using System.Text.Json.Serialization;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Infrastructure.Configuration;

/// <summary>
/// 连接配置存储契约：读当前配置、持久化。
/// </summary>
public interface IConnectionConfigStore
{
    /// <summary>
    /// 当前生效的连接配置（稳定实例：编辑时直接改它的字段再 Save，工厂即时读到）。
    /// </summary>
    ConnectionSettings Current { get; }

    /// <summary>
    /// 把当前配置持久化到 connections.json。
    /// </summary>
    void Save();
}

/// <summary>
/// 连接配置读写：Config/connections.json，首次运行写入默认值。
/// </summary>
public sealed class JsonConnectionConfigStore : IConnectionConfigStore
{
    /// <summary>
    /// 当前配置结构版本（不符则重建默认）。
    /// </summary>
    private const int CurrentSchema = 4;

    /// <summary>
    /// JSON 序列化选项（大小写不敏感、缩进、枚举按字符串）。
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// connections.json 路径。
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// 构造：定位 Config/connections.json 并加载（不存在则写默认）。
    /// </summary>
    public JsonConnectionConfigStore()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Config");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "connections.json");
        Current = Load();
    }

    /// <summary>
    /// 当前生效的连接配置。
    /// </summary>
    public ConnectionSettings Current { get; private set; }

    /// <summary>
    /// 加载配置：不存在或版本不符或解析失败则重建默认。
    /// </summary>
    /// <returns>连接配置。</returns>
    private ConnectionSettings Load()
    {
        if (!File.Exists(_path))
        {
            return WriteDefault();
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(_path), Options);
            // 旧结构（无 SchemaVersion 或版本不符）→ 重建默认
            if (loaded is null || loaded.SchemaVersion != CurrentSchema)
            {
                return WriteDefault();
            }

            return loaded;
        }
        catch
        {
            return WriteDefault();
        }
    }

    /// <summary>
    /// 写默认配置到文件并返回（写失败忽略）。
    /// </summary>
    /// <returns>默认连接配置。</returns>
    private ConnectionSettings WriteDefault()
    {
        var def = new ConnectionSettings();
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(def, Options));
        }
        catch
        {
            // 写盘失败（只读目录等）忽略，仍返回内存默认
        }
        return def;
    }

    /// <summary>
    /// 把当前配置持久化到 connections.json。
    /// </summary>
    public void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
    }
}
