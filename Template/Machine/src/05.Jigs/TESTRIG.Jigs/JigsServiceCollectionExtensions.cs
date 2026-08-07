using Microsoft.Extensions.DependencyInjection;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Jigs;

/// <summary>
/// 针床目录的 DI 注册扩展。
/// </summary>
public static class JigsServiceCollectionExtensions
{
    /// <summary>
    /// 注册针床目录（开机扫描 Manifests 加载所有板）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddPcbaJigs(this IServiceCollection services)
    {
        services.AddSingleton<JigCatalog>();
        // 共享设备（标准模块）配置仓储 = 针床目录（读写在 Manifests 下 .shared.json）
        services.AddSingleton<ISharedDeviceStore>(sp => sp.GetRequiredService<JigCatalog>());
        return services;
    }
}
