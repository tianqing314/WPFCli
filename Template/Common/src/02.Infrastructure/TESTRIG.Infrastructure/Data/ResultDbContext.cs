using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TESTRIG.Infrastructure.Data;

/// <summary>
/// 结果库共用基类：定义两张表的 DbSet 与主键/索引映射，供本地 SQLite 与远程 MySQL 复用。
/// </summary>
public abstract class ResultDbContextBase : DbContext
{
    /// <summary>
    /// 用 EF 选项构造。
    /// </summary>
    /// <param name="options">DbContext 选项。</param>
    protected ResultDbContextBase(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// 主表：以 SN 为维度，仅全测结果写/更新。
    /// </summary>
    public DbSet<PcbaTestData> TestData => Set<PcbaTestData>();

    /// <summary>
    /// 子表：以 SN + 测试项为维度。
    /// </summary>
    public DbSet<PcbaTestDataDetail> TestDataDetails => Set<PcbaTestDataDetail>();

    /// <summary>
    /// 配置主键与索引（主表按 SN 唯一 upsert；子表按 SN+项、TaskId 建索引）。
    /// </summary>
    /// <param name="b">模型构建器。</param>
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PcbaTestData>(e =>
        {
            e.HasKey(x => x.Id);
            // 主表按 SN upsert
            e.HasIndex(x => x.DeviceSn).IsUnique();
        });
        b.Entity<PcbaTestDataDetail>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DeviceSn, x.TestItemCode });
            e.HasIndex(x => x.TaskId);
        });
    }
}

/// <summary>
/// 本地 SQLite 结果库。远程上报由 <see cref="TESTRIG.Core.Abstractions.IExternalSync"/> 适配器单独实现。
/// </summary>
public sealed class ResultDbContext : ResultDbContextBase
{
    /// <summary>
    /// 用 EF 选项构造。
    /// </summary>
    /// <param name="options">DbContext 选项。</param>
    public ResultDbContext(DbContextOptions<ResultDbContext> options) : base(options)
    {
    }
}

/// <summary>
/// 远程 MySQL 结果库（正式环境上报目标），复用与本地相同的表结构与映射。
/// </summary>
public sealed class RemoteResultDbContext : ResultDbContextBase
{
    /// <summary>
    /// 用 EF 选项构造。
    /// </summary>
    /// <param name="options">DbContext 选项。</param>
    public RemoteResultDbContext(DbContextOptions<RemoteResultDbContext> options) : base(options)
    {
    }
}

/// <summary>
/// 主表：以 SN 为维度记录，只有跑全部测试项的结果才会记录或更新。
/// </summary>
[Table("pcba_test_data")]
public sealed class PcbaTestData
{
    /// <summary>
    /// 主键。
    /// </summary>
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 被检序列号。
    /// </summary>
    [Column("device_sn")]
    public string DeviceSn { get; set; } = "";

    /// <summary>
    /// 批次号。
    /// </summary>
    [Column("batch_no")]
    public string? BatchNo { get; set; }

    /// <summary>
    /// 设备型号。
    /// </summary>
    [Column("device_model")]
    public string? DeviceModel { get; set; }

    /// <summary>
    /// 自动化工位号（A/B/C/空）。
    /// </summary>
    [Column("station_no")]
    public string? StationNo { get; set; }

    /// <summary>
    /// 是否通过。
    /// </summary>
    [Column("is_pass")]
    public bool IsPass { get; set; }

    /// <summary>
    /// 是否经过重压重测（自动化异常/不合格触发重新压合再测）。
    /// </summary>
    [Column("is_repress")]
    public bool IsRePressed { get; set; }

    /// <summary>
    /// 操作员。
    /// </summary>
    [Column("operator")]
    public string? Operator { get; set; }

    /// <summary>
    /// 该 SN 第一次开始测试的时间。
    /// </summary>
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 该 SN 最后一次测试结束的时间。
    /// </summary>
    [Column("end_time")]
    public DateTime EndTime { get; set; }
}

/// <summary>
/// 子表：以 SN + 测试项为维度记录。
/// </summary>
[Table("pcba_test_data_details")]
public sealed class PcbaTestDataDetail
{
    /// <summary>
    /// 主键。
    /// </summary>
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 测试任务ID：每次执行生成一个新的，同一任务多个测试项共用。
    /// </summary>
    [Column("task_id")]
    public Guid TaskId { get; set; }

    /// <summary>
    /// 被检序列号。
    /// </summary>
    [Column("device_sn")]
    public string DeviceSn { get; set; } = "";

    /// <summary>
    /// 测试项编码（=Step.Key）。
    /// </summary>
    [Column("test_item_code")]
    public string TestItemCode { get; set; } = "";

    /// <summary>
    /// 测试项名称。
    /// </summary>
    [Column("test_item_name")]
    public string TestItemName { get; set; } = "";

    /// <summary>
    /// 测试过程日志信息（按流程记录）。
    /// </summary>
    [Column("test_process_infos")]
    public string? TestProcessInfos { get; set; }

    /// <summary>
    /// 测试过程实时数据采集记录（JSON，用于曲线图展示）。
    /// </summary>
    [Column("test_process_data")]
    public string? TestProcessData { get; set; }

    /// <summary>
    /// 测试结果状态：Success / MetricFail / HardwareError / CommunicationError。
    /// </summary>
    [Column("result_status")]
    public string ResultStatus { get; set; } = "";

    /// <summary>
    /// 错误信息。
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 测试项开始时间。
    /// </summary>
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 测试项结束时间。
    /// </summary>
    [Column("end_time")]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// 操作员。
    /// </summary>
    [Column("operator")]
    public string? Operator { get; set; }
}
