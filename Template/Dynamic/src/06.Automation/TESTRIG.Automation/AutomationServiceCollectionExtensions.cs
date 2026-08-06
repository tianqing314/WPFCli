using Microsoft.Extensions.DependencyInjection;

namespace TESTRIG.Automation;

/// <summary>
/// 自动化编排的 DI 注册扩展。
/// </summary>
public static class AutomationServiceCollectionExtensions
{
    /// <summary>
    /// 注册自动化编排器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddPcbaAutomation(this IServiceCollection services)
    {
        services.AddSingleton<AutomationOrchestrator>();
        return services;
    }
}
