using Microsoft.Extensions.Logging;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Plc;

/// <summary>
/// 自动化 PLC（国锐）仿真：连接/工位切换/重新压合/回送结果都打日志。全局共享单例。
/// 就绪即已压合、回送结果即自动下料打开，故无独立压合/打开指令。
/// 真实驱动：换成 Modbus 时序即可，对外契约不变。PORT: 旧 KUS_GUORUI。
/// </summary>
public sealed class SimulatedPlc : IPlcController
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<SimulatedPlc> _logger;

    /// <summary>
    /// 用日志构造仿真 PLC。
    /// </summary>
    /// <param name="logger">日志。</param>
    public SimulatedPlc(ILogger<SimulatedPlc> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 设备键。
    /// </summary>
    public string Key => "PLC";

    /// <summary>
    /// 设备型号名。
    /// </summary>
    public string Model => "KUS_GUORUI";

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 当前物理工位（0/1/2 → A/B/C）。默认 C（2）。
    /// </summary>
    public int CurrentStation { get; private set; } = 2;

    /// <summary>
    /// 是否响应 PLC 信号。
    /// </summary>
    public bool IsWatching { get; set; } = true;

    /// <summary>
    /// 仿真连接。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct);
        IsConnected = true;
        _logger.LogInformation("国锐 PLC 仿真连接成功");
    }

    /// <summary>
    /// 仿真等待工位就绪：模拟产线连续上料，每 ~800ms 有一块板在当前工位就绪（就绪即已压合）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>就绪工位号。</returns>
    public async Task<int> WaitForStationReadyAsync(CancellationToken ct = default)
    {
        await Task.Delay(800, ct);
        _logger.LogInformation("工位 {Station} 上料就绪（已压合）", (char)('A' + CurrentStation));
        return CurrentStation;
    }

    /// <summary>
    /// 仿真切换物理工位。
    /// </summary>
    /// <param name="station">目标工位号。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SelectStationAsync(int station, CancellationToken ct = default)
    {
        await Task.Delay(15, ct);
        CurrentStation = station;
        _logger.LogInformation("切换物理工位 {Station}", (char)('A' + station));
    }

    /// <summary>
    /// 仿真重新压合（重测用）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task RePressAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        _logger.LogInformation("触发重新压合");
    }

    /// <summary>
    /// 仿真回送测试结果（回送后自动下料并打开针床）。
    /// </summary>
    /// <param name="result">测试结果。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SendResultAsync(UnloadResult result, CancellationToken ct = default)
    {
        await Task.Delay(15, ct);
        _logger.LogInformation("回送结果 {Result}，PLC 自动打开针床下料", result);
    }

    /// <summary>
    /// 仿真读批次号（B + 当天日期）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>批次号。</returns>
    public async Task<string> ReadBatchNumberAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        var batch = "B" + DateTime.Now.ToString("yyyyMMdd");
        _logger.LogInformation("从 PLC 地址位读取批次号 {Batch}", batch);
        return batch;
    }

    /// <summary>
    /// 释放（置未连接）。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
