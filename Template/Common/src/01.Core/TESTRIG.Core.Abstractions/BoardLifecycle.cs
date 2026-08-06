namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 板级生命周期处理器：在首次测试前执行"整体测试前"（PreTest），在应用关闭前执行"整体测试后"（PostTest）。
/// 按 <see cref="DeviceFamily"/> 关联到对应针床，每 DeviceFamily 最多注册一个实现。
/// 可通过 <see cref="ManifestKey"/> 进一步限定到具体清单（不设置 = 不限）。
/// </summary>
public interface IBoardLifecycleHandler
{
    /// <summary>
    /// 关联的设备家族，对应 <see cref="JigManifest.DeviceFamily"/>。
    /// </summary>
    string DeviceFamily { get; }

    /// <summary>
    /// 可选的清单 Key 过滤：不为 null 时仅当 manifest.Key 匹配才执行生命周期方法。
    /// 用于同一 DeviceFamily 下有多块板但只有其中一块需要生命周期处理的场景（如 E05 测量板需前/后处理、系统板不需要）。
    /// 默认 null = 不限（该 DeviceFamily 下所有板都触发）。
    /// </summary>
    string? ManifestKey => null;

    /// <summary>
    /// 整体测试前：在第一次测试运行前执行一次（同一 DeviceFamily 只执行一次）。
    /// 可用于设备初始化、环境检查等全局预备工作。
    /// </summary>
    /// <param name="context">测试上下文（提供设备解析与日志）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试结果。</returns>
    Task<StepResult> OnPreTestAsync(ITestContext context, CancellationToken ct = default);

    /// <summary>
    /// 整体测试后：在应用关闭前执行一次（同一 DeviceFamily 只执行一次）。
    /// 可用于设备复位、数据汇总、资源清理等收尾工作。
    /// </summary>
    /// <param name="context">测试上下文（提供设备解析与日志）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试结果。</returns>
    Task<StepResult> OnPostTestAsync(ITestContext context, CancellationToken ct = default);
}
