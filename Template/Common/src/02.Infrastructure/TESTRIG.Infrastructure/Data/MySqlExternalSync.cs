using Microsoft.EntityFrameworkCore;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Infrastructure.Data;

/// <summary>
/// 远程 MySQL 上报适配器：把测试会话结果写入远程结果库（表结构与本地一致）。
/// 上报失败由本类抛出，调用方（<see cref="EfTestResultStore"/>）负责吞异常记日志，保证不阻断本地流程。
/// </summary>
public sealed class MySqlExternalSync : IExternalSync
{
    /// <summary>
    /// 远程 DbContext 工厂。
    /// </summary>
    private readonly IDbContextFactory<RemoteResultDbContext> _factory;

    /// <summary>
    /// 用远程 DbContext 工厂构造。
    /// </summary>
    /// <param name="factory">远程 DbContext 工厂。</param>
    public MySqlExternalSync(IDbContextFactory<RemoteResultDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public bool Enabled => true;

    /// <inheritdoc/>
    public async Task PushAsync(TestSessionResult result, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await ResultWriter.WriteSessionAsync(db, result, ct);
        await db.SaveChangesAsync(ct);
    }
}
