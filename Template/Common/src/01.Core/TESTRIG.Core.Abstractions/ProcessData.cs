namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 测试过程实时采集数据（最终序列化为 JSON 落 test_process_data，前端用于曲线绘制）。
/// 列式结构：共享时间轴 <see cref="TimeSec"/> + 每通道一组 <see cref="ProcessChannel.Values"/>，
/// 同一索引对应同一时刻，利于多通道叠加成曲线。
/// </summary>
public sealed record ProcessDataSeries
{
    /// <summary>
    /// 采集量单位（如 V/mA）。
    /// </summary>
    public string Unit { get; init; } = "V";

    /// <summary>
    /// 采样间隔（毫秒）。
    /// </summary>
    public int IntervalMs { get; init; }

    /// <summary>
    /// 采集起始时刻。
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// 各采样点的相对时间（秒，相对 <see cref="StartedAt"/>）。长度 = 采样点数。
    /// </summary>
    public IReadOnlyList<double> TimeSec { get; init; } = [];

    /// <summary>
    /// 各通道数据；每个通道的 Values 长度应与 <see cref="TimeSec"/> 一致。
    /// </summary>
    public IReadOnlyList<ProcessChannel> Channels { get; init; } = [];
}

/// <summary>
/// 一路采集通道。
/// </summary>
/// <param name="Name">通道名。</param>
/// <param name="Values">该通道各采样点的值。</param>
public sealed record ProcessChannel(string Name, IReadOnlyList<double> Values);
