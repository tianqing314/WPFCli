namespace TESTRIG.Core.Abstractions;

/// <summary>
/// 所有设备驱动的基契约。具体实现放在 TESTRIG.Devices 工程。
/// </summary>
public interface IDevice : IAsyncDisposable
{
    /// <summary>
    /// 设备键（唯一标识）。
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 设备型号名。
    /// </summary>
    string Model { get; }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接设备。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task ConnectAsync(CancellationToken ct = default);
}

/// <summary>
/// 按类型解析一组设备（被检 + 共享标准盒等）。由 <see cref="IDeviceProviderFactory"/> 按号位创建。
/// 用完须释放（<see cref="IAsyncDisposable"/>）：释放**该号位被检**的连接（串口/网络），
/// 全局共享设备（标准盒/PLC）不在此释放。
/// </summary>
public interface IDeviceProvider : IAsyncDisposable
{
    /// <summary>
    /// 按类型解析设备实例。
    /// </summary>
    /// <typeparam name="T">设备接口类型。</typeparam>
    /// <returns>设备实例。</returns>
    T GetDevice<T>() where T : class, IDevice;
}

/// <summary>
/// 按号位创建设备提供者：共享设备（标准盒/PLC）取全局单例，被检设备按 manifest 的型号由驱动注册表创建。
/// 实现放在 TESTRIG.Devices；引擎只依赖本抽象，保持分层（Core 不依赖 Devices）。
/// </summary>
public interface IDeviceProviderFactory
{
    /// <summary>
    /// 按 manifest 与号位创建设备提供者。
    /// </summary>
    /// <param name="manifest">针床清单。</param>
    /// <param name="position">号位。</param>
    /// <returns>该号位的设备提供者。</returns>
    IDeviceProvider Create(JigManifest manifest, PositionDescriptor position);
}
