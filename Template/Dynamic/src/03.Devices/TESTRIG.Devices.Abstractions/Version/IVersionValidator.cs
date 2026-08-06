namespace TESTRIG.Devices.Abstractions.Version;

/// <summary>
/// 版本校验服务（**非被检指令**）：把被检读回的版本号送到远程版本服务器，比对是否为最新。
/// 各被检设备测试项共用（蓝牙固件、主程序、硬件版本等），迁移自旧平台 <c>DBService</c> 版本验证区。
/// 各重载对应服务器不同的校验维度（软件版本 / +设备类型 / +硬件版本 / +主程序 / +后缀）。
/// 网络异常内部重试并降级为 <see cref="VersionValidResult.UnKnown"/>，不抛出，调用方按结果判定。
/// </summary>
public interface IVersionValidator
{
    /// <summary>按软件版本校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateAsync(string softVersion, CancellationToken ct = default);

    /// <summary>按软件版本 + 设备类型校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="deviceType">设备类型。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateByDeviceTypeAsync(string softVersion, string deviceType, CancellationToken ct = default);

    /// <summary>按软件版本 + 硬件版本校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="hardVersion">被检硬件版本。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateByHardVersionAsync(string softVersion, string hardVersion, CancellationToken ct = default);

    /// <summary>按软件版本 + 硬件版本 + 设备类型校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="hardVersion">被检硬件版本。</param>
    /// <param name="deviceType">设备类型。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateByHardVersionAndDeviceTypeAsync(string softVersion, string hardVersion, string deviceType, CancellationToken ct = default);

    /// <summary>按软件版本 + 主程序版本校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="hostVersion">主程序版本。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateByHostVersionAsync(string softVersion, string hostVersion, CancellationToken ct = default);

    /// <summary>按软件版本 + 后缀校验。</summary>
    /// <param name="softVersion">被检软件版本。</param>
    /// <param name="suffix">版本后缀。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>校验结果（失败降级为 UnKnown）。</returns>
    Task<VersionValidResponse> ValidateBySuffixAsync(string softVersion, string suffix, CancellationToken ct = default);
}
