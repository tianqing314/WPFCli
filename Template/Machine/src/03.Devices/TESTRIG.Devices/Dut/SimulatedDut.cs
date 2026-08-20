using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Dut;

/// <summary>
/// 通用被检板仿真驱动。本轮用仿真让全流程在 .NET 8 上可构建可运行，不拖入旧 .NET4.5 comm 栈。
/// 真实驱动落地：按设备型号实现 IDutDevice，在 <see cref="DutDriverRegistry"/> 注册即可，无需改引擎/UI。
/// PORT: 旧 Bots.TestBench.Device.* 各被检通讯类。
/// </summary>
public sealed class SimulatedDut : IDutDevice
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 仿真随机源。
    /// </summary>
    private readonly Random _rng = new();

    /// <summary>
    /// 用设备描述符构造仿真被检。
    /// </summary>
    /// <param name="descriptor">设备描述符。</param>
    /// <param name="logger">日志。</param>
    public SimulatedDut(DeviceDescriptor descriptor, ILogger logger)
    {
        Key = descriptor.Model;
        Model = descriptor.Model;
        _logger = logger;
    }

    /// <summary>
    /// 设备键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 设备型号名。
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 仿真连接（延时后置连接标志）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct);
        IsConnected = true;
        _logger.LogInformation("被检 {Model} 仿真连接成功", Model);
    }

    /// <summary>
    /// 仿真读序列号（型号 + 时间戳）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>序列号。</returns>
    public async Task<string> ReadSerialNumberAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        return $"{Model}{DateTime.Now:yyMMddHHmmss}";
    }

    /// <summary>
    /// 仿真读固件版本。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    public async Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        // 与 manifest 的 Text 条件一致 -> 通过
        return "V1.0.5";
    }

    /// <summary>
    /// 仿真写初始信息（仅记录日志）。
    /// </summary>
    /// <param name="boardType">板卡类型。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        _logger.LogInformation("{Model} 写入初始信息：{Type}", Model, boardType);
    }

    /// <summary>
    /// 仿真读某测量点：按点位返回让 manifest 条件通过的标称值。
    /// </summary>
    /// <param name="point">测量点标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值。</returns>
    public async Task<double> MeasureAsync(string point, CancellationToken ct = default)
    {
        await Task.Delay(30, ct);
        return point switch
        {
            // 电池电压标称
            "Battery" => 3.6 + _rng.NextDouble() * 0.1,
            // AD 值标称
            "AD" => 2048 + _rng.Next(-50, 50),
            _ => _rng.NextDouble(),
        };
    }

    /// <summary>
    /// 设置被检序列号（仿真直接返回成功）。
    /// </summary>
    public Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        _logger.LogInformation("{Model} 设置序列号：{SN}", Model, serialNumber);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 设置产品型号/主设备类型（仿真直接返回成功）。
    /// </summary>
    public Task<bool> SetPrimaryDeviceTypeAsync(string deviceType, CancellationToken ct = default)
    {
        _logger.LogInformation("{Model} 设置产品型号：{Type}", Model, deviceType);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 通用布尔查询（仿真返回随机值）。
    /// </summary>
    public Task<bool> QueryBooleanAsync(string method, object? arg, CancellationToken ct = default)
    {
        _logger.LogDebug("{Model} QueryBoolean: {Method}", Model, method);
        return Task.FromResult(_rng.Next(2) == 0);
    }

    /// <summary>
    /// 通用文本查询（仿真返回空字符串）。
    /// </summary>
    public Task<string> QueryTextAsync(string method, object? arg, CancellationToken ct = default)
    {
        _logger.LogDebug("{Model} QueryText: {Method}", Model, method);
        return Task.FromResult(string.Empty);
    }

    /// <summary>
    /// 通用指令执行（仿真仅记录日志）。
    /// </summary>
    public Task CommandAsync(string method, object? arg, CancellationToken ct = default)
    {
        _logger.LogDebug("{Model} Command: {Method}", Model, method);
        return Task.CompletedTask;
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
