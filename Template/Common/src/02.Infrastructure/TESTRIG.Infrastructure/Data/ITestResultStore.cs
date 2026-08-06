using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Infrastructure.Data;

/// <summary>
/// 测试结果存储契约：落库、按 SN/测试项查询历史。
/// </summary>
public interface ITestResultStore
{
    /// <summary>
    /// 远程库是否可用（配置了 RemoteSync 连接串时为 true）。查询页据此决定「查看远程」开关是否放行。
    /// </summary>
    bool RemoteAvailable { get; }

    /// <summary>
    /// 保存一次测试会话结果（子表逐项写；全测时主表按 SN upsert）。
    /// </summary>
    /// <param name="result">测试会话结果。</param>
    /// <param name="ct">取消令牌。</param>
    Task SaveAsync(TestSessionResult result, CancellationToken ct = default);

    /// <summary>
    /// 按 SN + 测试项代码取最近一次的过程日志/过程数据（完成的测试项从库里加载用）。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="testItemCode">测试项代码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>最近一次明细，或 null。</returns>
    Task<StoredStepDetail?> GetLatestStepAsync(string deviceSn, string testItemCode, CancellationToken ct = default);

    /// <summary>
    /// 分页查询主表（时间范围/批次/结果），按结束时间倒序。
    /// </summary>
    /// <param name="filter">查询条件。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">页大小。</param>
    /// <param name="useRemote">true 查远程 MySQL 库，false 查本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果。</returns>
    Task<TestDataPage> QueryMainAsync(TestRecordFilter filter, int page, int pageSize, bool useRemote = false, CancellationToken ct = default);

    /// <summary>
    /// 取某 SN 的所有测试项（同项多次取最近一次），按测试项顺序/时间。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="useRemote">true 查远程 MySQL 库，false 查本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项明细列表。</returns>
    Task<IReadOnlyList<StoredStepDetail>> GetStepsBySnAsync(string deviceSn, bool useRemote = false, CancellationToken ct = default);

    /// <summary>
    /// 按 SN 删除一条记录：主表该行 + 子表该 SN 的全部测试项明细（不可恢复，调用方须先确认）。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="useRemote">true 删远程 MySQL 库，false 删本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除条数（主表/子表）。</returns>
    Task<DeletedRecordCount> DeleteBySnAsync(string deviceSn, bool useRemote = false, CancellationToken ct = default);
}

/// <summary>
/// 删除结果条数。
/// </summary>
/// <param name="Main">主表删除行数。</param>
/// <param name="Details">子表（测试详情）删除行数。</param>
public sealed record DeletedRecordCount(int Main, int Details);

/// <summary>
/// 从库里加载的单测试项历史明细（含代码，供测试项列表 + 详情弹窗复用）。
/// </summary>
/// <param name="TestItemName">测试项名称。</param>
/// <param name="ResultStatus">结果状态。</param>
/// <param name="ProcessInfos">过程日志。</param>
/// <param name="ProcessDataJson">过程数据 JSON。</param>
/// <param name="ErrorMessage">错误信息。</param>
/// <param name="StartTime">开始时间。</param>
/// <param name="EndTime">结束时间。</param>
/// <param name="TestItemCode">测试项代码。</param>
public sealed record StoredStepDetail(
    string TestItemName,
    string ResultStatus,
    string? ProcessInfos,
    string? ProcessDataJson,
    string? ErrorMessage,
    DateTime StartTime,
    DateTime EndTime,
    string TestItemCode = "");

/// <summary>
/// 主表查询条件。
/// </summary>
/// <param name="From">起始时间。</param>
/// <param name="To">结束时间。</param>
/// <param name="BatchNo">批次号。</param>
/// <param name="IsPass">是否通过。</param>
/// <param name="DeviceModel">设备型号（当前测试页对应的被检型号；空则不过滤）。</param>
public sealed record TestRecordFilter(DateTime? From = null, DateTime? To = null, string? BatchNo = null, bool? IsPass = null, string? DeviceModel = null);

/// <summary>
/// 主表一行记录。
/// </summary>
/// <param name="DeviceSn">被检序列号。</param>
/// <param name="BatchNo">批次号。</param>
/// <param name="DeviceModel">设备型号。</param>
/// <param name="StationNo">工位号。</param>
/// <param name="IsPass">是否通过。</param>
/// <param name="IsRePressed">是否经过重压重测。</param>
/// <param name="Operator">操作员。</param>
/// <param name="StartTime">开始时间。</param>
/// <param name="EndTime">结束时间。</param>
public sealed record MainTestRecord(
    string DeviceSn, string? BatchNo, string? DeviceModel, string? StationNo,
    bool IsPass, bool IsRePressed, string? Operator, DateTime StartTime, DateTime EndTime);

/// <summary>
/// 分页结果。
/// </summary>
/// <param name="Items">当前页记录。</param>
/// <param name="Total">总数。</param>
public sealed record TestDataPage(IReadOnlyList<MainTestRecord> Items, int Total);

/// <summary>
/// 把测试会话结果落本地 SQLite。
/// </summary>
public sealed class EfTestResultStore : ITestResultStore
{
    /// <summary>
    /// 本地 SQLite DbContext 工厂。
    /// </summary>
    private readonly IDbContextFactory<ResultDbContext> _factory;

    /// <summary>
    /// 远程 MySQL DbContext 工厂（未配置 RemoteSync 连接串时为 null）。
    /// </summary>
    private readonly IDbContextFactory<RemoteResultDbContext>? _remoteFactory;

    /// <summary>
    /// 远程上报适配器（未配置时为 <see cref="NullExternalSync"/>）。
    /// </summary>
    private readonly IExternalSync _sync;

    /// <summary>
    /// 当前用户会话：测试账号（如 admin）登录时数据只落本地、不上传云端。
    /// </summary>
    private readonly Auth.IUserSession _session;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<EfTestResultStore> _logger;

    /// <summary>
    /// 用本地/远程 DbContext 工厂 + 远程上报适配器 + 用户会话构造。
    /// </summary>
    /// <param name="factory">本地 SQLite DbContext 工厂。</param>
    /// <param name="remoteFactory">远程 MySQL DbContext 工厂（未配置时为 null）。</param>
    /// <param name="sync">远程上报适配器。</param>
    /// <param name="session">当前用户会话（测试账号门）。</param>
    /// <param name="logger">日志。</param>
    public EfTestResultStore(IDbContextFactory<ResultDbContext> factory, IDbContextFactory<RemoteResultDbContext>? remoteFactory, IExternalSync sync, Auth.IUserSession session, ILogger<EfTestResultStore> logger)
    {
        _factory = factory;
        _remoteFactory = remoteFactory;
        _sync = sync;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// 远程库是否可用（配置了 RemoteSync 连接串 → 远程工厂已注册）。
    /// </summary>
    public bool RemoteAvailable => _remoteFactory is not null;

    /// <summary>
    /// 按数据源创建 DbContext（远程未配置时抛异常，调用方应先查 <see cref="RemoteAvailable"/>）。
    /// </summary>
    /// <param name="useRemote">是否用远程库。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>本地或远程结果库上下文。</returns>
    private async Task<ResultDbContextBase> CreateQueryDbAsync(bool useRemote, CancellationToken ct)
    {
        if (useRemote)
        {
            if (_remoteFactory is null)
            {
                throw new InvalidOperationException("远程库未配置，无法查询远程数据。");
            }

            return await _remoteFactory.CreateDbContextAsync(ct);
        }

        return await _factory.CreateDbContextAsync(ct);
    }

    /// <summary>
    /// 保存会话结果：先落本地 SQLite；本地提交成功后再上报远程（远程失败仅记日志，不影响本地）。
    /// </summary>
    /// <param name="result">测试会话结果。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SaveAsync(TestSessionResult result, CancellationToken ct = default)
    {
        await using (var db = await _factory.CreateDbContextAsync(ct))
        {
            await ResultWriter.WriteSessionAsync(db, result, ct);
            await db.SaveChangesAsync(ct);
        }

        // 测试账号（如 admin）登录：数据只落本地，不上传云端（调试/试机不污染云端产线数据）。
        if (_session.IsTestAccount)
        {
            _logger.LogInformation("测试账号登录，本次结果只落本地不上传云端（TaskId={TaskId}）。", result.TaskId);
            return;
        }

        // 本地已落库成功——再尝试远程上报；远程不可用不能拖垮产线本地流程。
        if (_sync.Enabled)
        {
            try
            {
                await _sync.PushAsync(result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "远程上报失败（TaskId={TaskId}），本地已保存；远程该批数据缺失，需补传。", result.TaskId);
            }
        }
    }

    /// <summary>
    /// 分页查询主表：按条件过滤，结束时间倒序，页大小限 1~100。
    /// </summary>
    /// <param name="filter">查询条件。</param>
    /// <param name="page">页码（&lt;1 视为 1）。</param>
    /// <param name="pageSize">页大小（&lt;1 视为 20，&gt;100 截为 100）。</param>
    /// <param name="useRemote">true 查远程 MySQL 库，false 查本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果。</returns>
    public async Task<TestDataPage> QueryMainAsync(TestRecordFilter filter, int page, int pageSize, bool useRemote = false, CancellationToken ct = default)
    {
        await using var db = await CreateQueryDbAsync(useRemote, ct);
        var q = db.TestData.AsQueryable();
        if (filter.From is { } f)
        {
            q = q.Where(x => x.EndTime >= f);
        }

        if (filter.To is { } t)
        {
            q = q.Where(x => x.EndTime <= t);
        }

        if (!string.IsNullOrWhiteSpace(filter.BatchNo))
        {
            q = q.Where(x => x.BatchNo == filter.BatchNo);
        }

        if (filter.IsPass is { } p)
        {
            q = q.Where(x => x.IsPass == p);
        }

        // 只看当前测试页对应的被检型号——不同板卡的数据互不串台
        if (!string.IsNullOrWhiteSpace(filter.DeviceModel))
        {
            q = q.Where(x => x.DeviceModel == filter.DeviceModel);
        }

        var total = await q.CountAsync(ct);
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var items = await q.OrderByDescending(x => x.EndTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MainTestRecord(x.DeviceSn, x.BatchNo, x.DeviceModel, x.StationNo,
                x.IsPass, x.IsRePressed, x.Operator, x.StartTime, x.EndTime))
            .ToListAsync(ct);
        return new TestDataPage(items, total);
    }

    /// <summary>
    /// 取某 SN 所有测试项（同代码多次取最近一次），按开始时间排序。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="useRemote">true 查远程 MySQL 库，false 查本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项明细列表。</returns>
    public async Task<IReadOnlyList<StoredStepDetail>> GetStepsBySnAsync(string deviceSn, bool useRemote = false, CancellationToken ct = default)
    {
        await using var db = await CreateQueryDbAsync(useRemote, ct);
        // 同一测试项可能多次（重测），取每个代码最近一次
        var rows = await db.TestDataDetails
            .Where(d => d.DeviceSn == deviceSn)
            .ToListAsync(ct);
        return rows
            .GroupBy(d => d.TestItemCode)
            .Select(g => g.OrderByDescending(d => d.EndTime).First())
            .OrderBy(d => d.StartTime)
            .Select(d => new StoredStepDetail(d.TestItemName, d.ResultStatus, d.TestProcessInfos,
                d.TestProcessData, d.ErrorMessage, d.StartTime, d.EndTime, d.TestItemCode))
            .ToList();
    }

    /// <summary>
    /// 按 SN 删除主表行与该 SN 的全部子表明细（先删子表再删主表；两张表在同一连接上顺序删除）。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="useRemote">true 删远程 MySQL 库，false 删本地 SQLite（默认）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除条数（主表/子表）。</returns>
    public async Task<DeletedRecordCount> DeleteBySnAsync(string deviceSn, bool useRemote = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceSn))
        {
            return new DeletedRecordCount(0, 0);
        }

        await using var db = await CreateQueryDbAsync(useRemote, ct);
        var details = await db.TestDataDetails.Where(d => d.DeviceSn == deviceSn).ExecuteDeleteAsync(ct);
        var main = await db.TestData.Where(x => x.DeviceSn == deviceSn).ExecuteDeleteAsync(ct);
        _logger.LogWarning("删除测试记录：SN={Sn}，数据源={Source}，主表 {Main} 行，详情 {Details} 行，操作员={Operator}。",
            deviceSn, useRemote ? "远程MySQL" : "本地SQLite", main, details, _session.Operator);
        return new DeletedRecordCount(main, details);
    }

    /// <summary>
    /// 取某 SN + 测试项代码最近一次明细，无则 null。
    /// </summary>
    /// <param name="deviceSn">被检序列号。</param>
    /// <param name="testItemCode">测试项代码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>最近一次明细，或 null。</returns>
    public async Task<StoredStepDetail?> GetLatestStepAsync(string deviceSn, string testItemCode, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.TestDataDetails
            .Where(d => d.DeviceSn == deviceSn && d.TestItemCode == testItemCode)
            .OrderByDescending(d => d.EndTime)
            .FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new StoredStepDetail(row.TestItemName, row.ResultStatus, row.TestProcessInfos,
                row.TestProcessData, row.ErrorMessage, row.StartTime, row.EndTime);
    }
}
