using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using TESTRIG.Devices.Comm;
using TESTRIG.Devices.Dut;
using TESTRIG.Devices.StandardBox;

namespace TESTRIG.Devices;

/// <summary>
/// 真机/仿真硬件选项（来自 appsettings 的 Pcba:Hardware，由 App 绑定后传入）。
/// </summary>
public sealed record HardwareOptions
{
    /// <summary>
    /// 是否走真机驱动（Xmas11）。也可用环境变量 TESTRIG_REAL_HARDWARE=1 覆盖。
    /// </summary>
    public bool UseReal { get; init; }

    /// <summary>
    /// 产线温湿度计监控服务基址（环境温度）。
    /// </summary>
    public string EnvTempBaseUrl { get; init; } = "http://192.168.0.130:5050";

    /// <summary>
    /// 远程版本校验服务器基址（被检版本比对）。
    /// </summary>
    public string VersionVerifyBaseUrl { get; init; } = "http://192.168.0.134:10001";
}

/// <summary>
/// 设备层 DI 注册扩展。
/// </summary>
public static class DevicesServiceCollectionExtensions
{
    /// <summary>
    /// 注册设备层：共享标准盒（单例）、蓝牙扫描、环境温度、被检驱动注册表、设备提供者工厂。
    /// 整机模板不注册 PLC（国锐）——与动态模板的差异由业务模板同名覆盖实现。
    /// 真机（配置或环境变量）走 Xmas11 驱动，否则全仿真。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="hw">硬件选项（null 时用默认 + 环境变量）。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddPcbaDevices(this IServiceCollection services, HardwareOptions? hw = null)
    {
        // 默认连接配置（无 Infrastructure 时的兜底，如头测）；App 里被 connections.json 版覆盖
        services.TryAddSingleton(new ConnectionSettings());

        // 真机开关：配置 Pcba:Hardware:UseReal 或环境变量 TESTRIG_REAL_HARDWARE=1
        var useReal = (hw?.UseReal ?? false) || Environment.GetEnvironmentVariable("TESTRIG_REAL_HARDWARE") == "1";

        // 标准盒：真机走 Xmas11 RealStandardBox，否则仿真。两者都实现 IDynamicStandardBox。
        if (useReal)
        {
            services.AddSingleton<IDynamicStandardBox, StandardBox.StandardBox>();
        }
        else
        {
            services.AddSingleton<IDynamicStandardBox, SimulatedStandardBox>();
        }
        services.AddSingleton<IStandardBox>(sp => sp.GetRequiredService<IDynamicStandardBox>());

        // 蓝牙扫描兜底（真机由 App 注册 Win10 实现覆盖）
        services.TryAddSingleton<IBleScanner, Comm.NoOpBleScanner>();

        // 环境温度/大气压（同一 HTTP 温湿度计监控服务，type=T / type=BP）；仿真不调用，无网络依赖
        var envUrl = hw?.EnvTempBaseUrl ?? "http://192.168.0.130:5050";
        services.TryAddSingleton<IEnvironmentTemperature>(sp =>
            new Comm.HttpEnvironmentTemperature(envUrl, sp.GetRequiredService<ILoggerFactory>().CreateLogger<Comm.HttpEnvironmentTemperature>()));
        services.TryAddSingleton<IEnvironmentPressure>(sp =>
            new Comm.HttpEnvironmentPressure(envUrl, sp.GetRequiredService<ILoggerFactory>().CreateLogger<Comm.HttpEnvironmentPressure>()));

        // 远程版本校验（被检读回版本 与 服务器最新版本比对）；各被检设备测试项共用，仿真不调用
        var versionUrl = hw?.VersionVerifyBaseUrl ?? "http://192.168.0.134:10001";
        services.TryAddSingleton<Abstractions.Version.IVersionValidator>(sp =>
            new Comm.HttpVersionValidator(versionUrl, sp.GetRequiredService<ILoggerFactory>().CreateLogger<Comm.HttpVersionValidator>()));

        // 设备扫描 + 物理链路解析（串口 WMI；USB 下一轮）
        services.AddSingleton<IDeviceScanner, WmiSerialScanner>();
        services.AddSingleton<IUsbEnumerator, EmptyUsbEnumerator>();
        services.AddSingleton<IConnectionResolver, ConnectionResolver>();
        services.AddSingleton<ConnectionManager>();

        services.AddSingleton(sp =>
        {
            var lf = sp.GetRequiredService<ILoggerFactory>();
            var registry = new DutDriverRegistry(lf);
            // 按型号自动注册被检驱动：反射扫描打了 [DutDriver("型号")] 的驱动类（真机开关择真机/仿真变体）。
            // 新增被检驱动只需给类打特性，无需在此手写一行。未注册型号回落通用仿真。
            registry.AutoRegisterFromAssembly(useReal);

            return registry;
        });

        // 标准模块注册表：按型号自动注册标准设备驱动（实现 IStandardModule + [DutDriver]，如 DPSEX 正压/真空模块）。
        // 新增标准设备只需给驱动类打特性，无需在此手写一行。
        services.AddSingleton(sp =>
        {
            var lf = sp.GetRequiredService<ILoggerFactory>();
            var registry = new StandardModuleRegistry(lf);
            registry.AutoRegisterFromAssembly();
            return registry;
        });

        services.AddSingleton<IDeviceProviderFactory, DeviceProviderFactory>();
        return services;
    }
}
