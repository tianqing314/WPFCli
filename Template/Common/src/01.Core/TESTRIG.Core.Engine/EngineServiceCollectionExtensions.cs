using Microsoft.Extensions.DependencyInjection;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Core.Engine;

/// <summary>
/// 测试引擎核心服务的 DI 注册扩展。
/// </summary>
public static class EngineServiceCollectionExtensions
{
    /// <summary>
    /// 注册测试引擎核心服务（判定器 + 运行器）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddTestEngine(this IServiceCollection services)
    {
        services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
        services.AddSingleton<TestRunner>();
        return services;
    }
}
