using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Infrastructure.Auth;
using TESTRIG.Infrastructure.Configuration;
using TESTRIG.Infrastructure.Data;
using TESTRIG.Infrastructure.Notifications;

namespace TESTRIG.Infrastructure;

/// <summary>
/// 基础设施层 DI 注册扩展（配置、认证、连接配置、通知、结果库）。
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// 注册基础设施层服务：绑定配置、选认证实现（配 OA 走远程否则本地）、连接配置存储、通知总线、SQLite 结果库。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="config">应用配置。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddPcbaInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PcbaOptions>(config.GetSection(PcbaOptions.Section));
        var resultStoreSection = config.GetSection(PcbaOptions.Section).GetSection("ResultStore");
        var resultStoreOptions = new ResultStoreOptions
        {
            Schema = resultStoreSection["Schema"] ?? "pcba",
            TestTypeClass = resultStoreSection["TestTypeClass"],
            TestTypeDetail = int.TryParse(resultStoreSection["TestTypeDetail"], out var typeDetail) ? typeDetail : null,
        };
        services.AddSingleton(resultStoreOptions);
        services.AddSingleton<ManifestLoader>();

        // 认证：配置了 OA 基址走组合认证（测试账号 admin 免 OA 走本地，其余走 OA），否则纯本地离线认证。
        var oaBaseUrl = config.GetSection(PcbaOptions.Section).GetSection("Oa")["BaseUrl"];
        services.AddSingleton<LocalAuthService>();
        if (!string.IsNullOrWhiteSpace(oaBaseUrl))
        {
            services.AddSingleton<OaAuthService>();
            services.AddSingleton<IAuthService, CompositeAuthService>();
        }
        else
        {
            services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<LocalAuthService>());
        }

        services.AddSingleton<IUserSession, UserSession>();

        // 登录历史（近 10 人账号密码，密码 DPAPI 加密落盘），供登录页默认回填与快速选择
        services.AddSingleton<ILoginHistoryStore, JsonLoginHistoryStore>();

        // 连接配置（标准盒/PLC/被检），读写 Config/connections.json
        services.AddSingleton<IConnectionConfigStore, JsonConnectionConfigStore>();
        services.AddSingleton(sp => sp.GetRequiredService<IConnectionConfigStore>().Current);

        // 全局通知总线（状态栏）
        services.AddSingleton<INotificationService, NotificationService>();

        var dbRelative = config.GetSection(PcbaOptions.Section)["DatabasePath"] ?? "Data/results.db";
        var dbFull = Path.Combine(AppContext.BaseDirectory, dbRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(dbFull)!);
        services.AddDbContextFactory<ResultDbContext>(o => o.UseSqlite($"Data Source={dbFull}"));

        // 远程上报：配置了 MySQL 连接串 → 注册远程库工厂 + MySQL 适配器；否则空实现（仅本地）。
        var remoteConn = config.GetSection(PcbaOptions.Section).GetSection("RemoteSync")["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(remoteConn))
        {
            // 现场数据库未启用 TLS；显式关闭可避免 MySqlConnector 在无凭证环境下启动失败。
            remoteConn = AppendSslModeNone(remoteConn);
            // 固定 MySQL 8.0 版本，不用 ServerVersion.AutoDetect：后者在 DI 解析工厂时会发起真实连接，
            // 现场 MySQL 不可达时直接阻塞超时，连累 admin 等仅用本地 SQLite 的登录/开板流程。
            services.AddDbContextFactory<RemoteResultDbContext>(o =>
                o.UseMySql(remoteConn, new MySqlServerVersion(new Version(8, 0, 21))));
            services.AddSingleton<IExternalSync, MySqlExternalSync>();
        }
        else
        {
            services.AddSingleton<IExternalSync>(NullExternalSync.Instance);
        }

        // 结果库：本地工厂必需，远程工厂可选（未配置 RemoteSync 时为 null → RemoteAvailable=false）。
        services.AddSingleton<ITestResultStore>(sp => new EfTestResultStore(
            sp.GetRequiredService<IDbContextFactory<ResultDbContext>>(),
            sp.GetService<IDbContextFactory<RemoteResultDbContext>>(),
            sp.GetRequiredService<ResultStoreOptions>(),
            sp.GetRequiredService<IExternalSync>(),
            sp.GetRequiredService<IUserSession>(),
            sp.GetRequiredService<ILogger<EfTestResultStore>>()));
        return services;
    }

    private static string AppendSslModeNone(string connectionString) =>
        connectionString.Contains("SslMode=", StringComparison.OrdinalIgnoreCase)
            ? connectionString
            : connectionString.TrimEnd(';') + ";SslMode=None;";

    /// <summary>
    /// 开机确保本地结果库已建。开发期无正式迁移：若检测到表结构漂移（缺列）则重建。
    /// </summary>
    /// <param name="provider">服务提供者。</param>
    public static void EnsurePcbaDatabase(this IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<ResultDbContext>>();
        var schema = provider.GetRequiredService<ResultStoreOptions>().ResolvedSchema;
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        try
        {
            // 触一下当前 schema 的关键列；缺表/缺列（旧库）→ 重建（仿真数据，可接受）。
            if (schema == ResultSchema.Product)
            {
                _ = db.ProductTestData.Select(x => new { x.Id, x.IsAllCompleted, x.TestTypeClass }).FirstOrDefault();
                _ = db.ProductTestDataDetails.Select(x => new { x.TaskId, x.TestItemDesc, x.TestItemConditions }).FirstOrDefault();
            }
            else
            {
                _ = db.TestData.Select(x => new { x.StartTime, x.EndTime, x.IsRePressed }).FirstOrDefault();
                _ = db.TestDataDetails.Select(x => new { x.TestItemDesc, x.TestItemConditions }).FirstOrDefault();
            }
        }
        catch
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }
    }
}
