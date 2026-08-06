using Microsoft.Extensions.DependencyInjection;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.TestSteps;

/// <summary>
/// 测试项处理器的 DI 注册扩展。
/// </summary>
public static class TestStepsServiceCollectionExtensions
{
    /// <summary>
    /// 自动注册 TESTRIG.TestSteps 程序集内所有测试项处理器（<see cref="IStepHandler"/>）
    /// 及板级生命周期处理器（<see cref="IBoardLifecycleHandler"/>）。
    /// **新增一类测试项 / 新板的设备特有测试 = 只写一个 IStepHandler 类即可，无需在此登记。**
    /// 注：这是受控的「单程序集 + 强类型」扫描（只扫本程序集、只挑实现接口的具体类），
    /// 与早期弃用的 MEF「全目录 DLL 扫描 + 黑名单」完全不同——可预期、可断点、编译期类型安全。
    /// 处理器的构造函数依赖照常由 DI 注入。
    /// </summary>
    public static IServiceCollection AddPcbaTestSteps(this IServiceCollection services)
    {
        var asm = typeof(TestStepsServiceCollectionExtensions).Assembly;

        // 注册 IStepHandler
        foreach (var t in asm.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                    && typeof(IStepHandler).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IStepHandler), t);
        }

        // 注册 IBoardLifecycleHandler
        foreach (var t in asm.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                    && typeof(IBoardLifecycleHandler).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IBoardLifecycleHandler), t);
        }

        return services;
    }
}
