using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// 上下料结果（送回 PLC 供分拣）。
/// </summary>
public enum UnloadResult
{
    /// <summary>
    /// 良品。
    /// </summary>
    Ok,

    /// <summary>
    /// 不良品。
    /// </summary>
    Ng,
}

/// <summary>
/// 自动化 PLC（国锐）控制器，**全局共享**。负责上下料握手、当前工位信号、重新压合、回送 OK/NG。
/// <para>
/// <b>压合/打开非独立指令</b>：等到工位就绪（<see cref="WaitForStationReadyAsync"/> 返回）时，板已自动上料并**压合完成**；
/// 回送测试结果（<see cref="SendResultAsync"/>）后 PLC 会**自动打开针床并下料**，无需单独的压合/打开指令。
/// 需要重测时，发 <see cref="RePressAsync"/> 触发重新压合，再次 <see cref="WaitForStationReadyAsync"/> 等就绪即可继续。
/// </para>
/// PORT: 旧 Bots.TestBench.Device.KUS_GUORUI（Modbus）。
/// </summary>
public interface IPlcController : IDevice
{
    /// <summary>
    /// 当前正在服务的物理工位（A/B/C → 0/1/2）。
    /// </summary>
    int CurrentStation { get; }

    /// <summary>
    /// 是否响应 PLC 信号（停止/继续响应）。全自动循环以此启停。
    /// </summary>
    bool IsWatching { get; set; }

    /// <summary>
    /// 等待下一块板在所选工位就绪。就绪即表示已自动上料并**压合完成**（无需再单独下压合指令）。返回工位号。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>就绪工位号。</returns>
    Task<int> WaitForStationReadyAsync(CancellationToken ct = default);

    /// <summary>
    /// 切换正在监视/服务的物理工位。
    /// </summary>
    /// <param name="station">目标工位号。</param>
    /// <param name="ct">取消令牌。</param>
    Task SelectStationAsync(int station, CancellationToken ct = default);

    /// <summary>
    /// 触发重新压合（重测用）。发出后板会重新压合，随后再 <see cref="WaitForStationReadyAsync"/> 等就绪即可继续测试。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    Task RePressAsync(CancellationToken ct = default);

    /// <summary>
    /// 把某板测试结果回送 PLC（OK 进良品、NG 进不良品）。**回送后 PLC 自动打开针床并下料**，无需单独打开指令。
    /// </summary>
    /// <param name="result">测试结果。</param>
    /// <param name="ct">取消令牌。</param>
    Task SendResultAsync(UnloadResult result, CancellationToken ct = default);

    /// <summary>
    /// 从 PLC 地址位读取当前批次号（供 UI「从 PLC 获取」用）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>批次号。</returns>
    Task<string> ReadBatchNumberAsync(CancellationToken ct = default);
}
