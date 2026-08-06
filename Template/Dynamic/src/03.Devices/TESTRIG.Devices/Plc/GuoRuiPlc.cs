using EasyModbus;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;

namespace TESTRIG.Devices.Plc;

/// <summary>
/// 国锐自动化 PLC **真机驱动骨架**（Modbus TCP）。对标旧 <c>Bots.TestBench.Device.KUS_GUORUI</c>（EasyModbus）。
/// 全局共享单例，负责上下料握手/工位切换/重新压合/回送 OK-NG。
/// <para>
/// <b>握手模型</b>：就绪位=1 即板已上料并**压合完成**（无独立压合指令）；回送结果即触发 PLC **自动下料打开针床**（无独立打开指令）；
/// 重测走 <see cref="RePressAsync"/> 触发重新压合后，再轮询就绪位继续。
/// </para>
/// <para>
/// <b>骨架状态</b>：连接 + 寄存器读写 + 握手主流程已按旧驱动搭好，但以下**必须现场对国锐 PLC 文档核对/联调**后才能上线：
/// ① 寄存器地址表（<see cref="Commons"/> 与工位步距 <see cref="SlotStride"/> 抄自旧硬编码，未必与本机一致）；
/// ② 就绪/重压/结果触发各寄存器位的实际语义（<see cref="WaitForStationReadyAsync"/>/<see cref="RePressAsync"/>/<see cref="SendResultAsync"/> 的 TODO）；
/// ③ 到位/节拍等待时序（压合到位、上下料到位信号）；
/// ④ <see cref="ReadBatchNumberAsync"/> 的批次寄存器地址（当前占位）。
/// </para>
/// </summary>
public sealed class GuoRuiPlc : IPlcController
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger<GuoRuiPlc> _logger;

    /// <summary>
    /// 连接配置（PLC 网络端点来源）。
    /// </summary>
    private readonly ConnectionSettings _connections;

    /// <summary>
    /// Modbus TCP 客户端（连接后有值）。
    /// </summary>
    private ModbusClient? _mc;

    /// <summary>
    /// 串行化 Modbus 读写（Modbus TCP 非线程安全，就绪轮询与结果回送会并发）。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 寄存器基址表（工位A；B/C 加 <see cref="SlotStride"/>*工位号）。下标语义（对国锐地址表核对无误）：
    /// 0=启动/就绪(读，D600 PLC 置 1 请求测试软件启动)、1=运行中(写，D620 软件进入测试后写 1 回应，PLC 据此撤销启动位)、
    /// 2=测试结果(写，D621，OK=1/NG=2)、3=测试完成(写，D622 写 1 → PLC 自动打开针床下料)、
    /// 4=重压RePress(写，D623)、5=警告信息(写，D624)。
    /// </summary>
    private static readonly int[] Commons = { 600, 620, 621, 622, 623, 624 };

    /// <summary>
    /// 每物理工位寄存器步距（PORT: 旧 WorkingSlot*40）。**TODO：现场核对。**
    /// </summary>
    private const int SlotStride = 40;

    /// <summary>
    /// 就绪轮询间隔（毫秒）。**TODO：按现场节拍调。**
    /// </summary>
    private const int PollIntervalMs = 200;

    /// <summary>
    /// 用连接配置 + 日志构造真机 PLC。
    /// </summary>
    /// <param name="connections">连接配置（取 <see cref="ConnectionSettings.Plc"/> 端点）。</param>
    /// <param name="logger">日志。</param>
    public GuoRuiPlc(ConnectionSettings connections, ILogger<GuoRuiPlc> logger)
    {
        _connections = connections;
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
    /// 当前物理工位（0/1/2 → A/B/C），决定寄存器偏移。默认 C（2）。
    /// </summary>
    public int CurrentStation { get; private set; } = 2;

    /// <summary>
    /// 是否响应 PLC 信号（全自动循环启停门）。
    /// </summary>
    public bool IsWatching { get; set; } = true;

    /// <summary>
    /// 连接 PLC（Modbus TCP）。端点取 <see cref="ConnectionSettings.Plc"/>。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var ep = _connections.Plc;
        var ip = string.IsNullOrWhiteSpace(ep?.Ip) ? "223.223.223.100" : ep!.Ip!;
        var port = ep?.Port ?? 502;

        await Task.Run(
            () =>
            {
                try
                {
                    _mc?.Disconnect();
                }
                catch
                {
                    // 忽略旧连接关闭异常
                }

                _mc = new ModbusClient(ip, port) { ConnectionTimeout = 2000 };
                try
                {
                    _mc.Connect();
                    IsConnected = _mc.Connected;
                }
                catch (Exception ex)
                {
                    // 无 PLC / 网络不通：连接失败非致命，置未连接即可，绝不上抛（否则连接配置页崩溃退出）
                    IsConnected = false;
                    _logger.LogWarning(ex, "国锐 PLC 连接异常（无 PLC 或网络不通）{Ip}:{Port}", ip, port);
                }
            },
            ct);

        if (IsConnected)
        {
            _logger.LogInformation("国锐 PLC 真机连接成功 {Ip}:{Port}", ip, port);
        }
        else
        {
            _logger.LogWarning("国锐 PLC 连接失败 {Ip}:{Port}", ip, port);
        }
    }

    /// <summary>
    /// 取当前工位下某语义寄存器的绝对地址。PORT: 旧 GetAddress（commons[idx] + slot*40）。
    /// </summary>
    /// <param name="idx"><see cref="Commons"/> 下标。</param>
    /// <returns>绝对寄存器地址。</returns>
    private int Addr(int idx)
    {
        return Commons[idx] + CurrentStation * SlotStride;
    }

    /// <summary>
    /// 读一个保持寄存器（串行化 + 未连接返回 -1）。PORT: 旧 ReadAddress。
    /// </summary>
    /// <param name="idx"><see cref="Commons"/> 下标。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>寄存器值，失败 -1。</returns>
    private async Task<int> ReadAsync(int idx, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_mc is not { Connected: true })
            {
                return -1;
            }
            return _mc.ReadHoldingRegisters(Addr(idx), 1)[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PLC 读寄存器 idx={Idx} 失败", idx);
            return -1;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 写一个保持寄存器（串行化）。PORT: 旧 WriteAddress。
    /// </summary>
    /// <param name="idx"><see cref="Commons"/> 下标。</param>
    /// <param name="val">值。</param>
    /// <param name="ct">取消令牌。</param>
    private async Task WriteAsync(int idx, int val, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_mc is not { Connected: true })
            {
                throw new DeviceCommException("PLC 未连接，无法写寄存器", TestResultStatus.HardwareError);
            }
            _mc.WriteSingleRegister(Addr(idx), val);
            _logger.LogInformation("PLC 写寄存器 {Addr}={Val}", Addr(idx), val);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 等待当前工位板到位就绪（轮询启动位 D600==1）。就绪即已压合完成。命中后**回写运行中 D620=1** 回应 PLC
    /// 「测试软件已进入测试」，PLC 据此撤销启动位（否则启动位保持 1 会导致下一轮空转）。PORT: 旧轮询就绪+写应答。
    /// 由 <see cref="IsWatching"/> 与取消令牌控制启停。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>就绪工位号。</returns>
    public async Task<int> WaitForStationReadyAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (IsWatching && await ReadAsync(0, ct) == 1)
            {
                _logger.LogInformation("工位 {Station} 上料就绪（已压合）", (char)('A' + CurrentStation));
                await WriteAsync(1, 1, ct);   // 运行中=1：回应 PLC 软件已进入测试
                return CurrentStation;
            }
            await Task.Delay(PollIntervalMs, ct);
        }
        ct.ThrowIfCancellationRequested();
        return CurrentStation;
    }

    /// <summary>
    /// 切换正在服务的物理工位（改寄存器偏移）。PORT: 旧 WorkingSlot 赋值。
    /// </summary>
    /// <param name="station">目标工位号。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SelectStationAsync(int station, CancellationToken ct = default)
    {
        CurrentStation = station;
        _logger.LogInformation("切换物理工位 {Station}", (char)('A' + station));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回送测试结果给 PLC（OK 进良品、NG 进不良品）。回送后 PLC **自动打开针床下料**，无需单独打开指令。
    /// PORT: 旧 SendTestResult（清应答位、写结果值、置结果触发）。
    /// </summary>
    /// <param name="result">测试结果。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task SendResultAsync(UnloadResult result, CancellationToken ct = default)
    {
        await WriteAsync(2, result == UnloadResult.Ok ? 1 : 2, ct);   // 测试结果：OK=1 / NG=2
        await WriteAsync(1, 0, ct);                                   // 运行中=0：测试软件退出测试
        await WriteAsync(3, 1, ct);                                   // 测试完成=1：PLC 据此打开针床下料
        _logger.LogInformation("回送结果 {Result}，PLC 自动打开针床下料", result);
    }

    /// <summary>
    /// 发送报警码（报警灯/蜂鸣）。PORT: 旧 SendWarningInfo。接口未声明，供编排器/UI 需要时直接调。
    /// </summary>
    /// <param name="code">报警码。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SendWarningAsync(int code, CancellationToken ct = default)
    {
        return WriteAsync(5, code, ct);
    }

    /// <summary>
    /// 触发重新压合（不良品重测）。发出后板重新压合，PLC 重新置启动位，编排器随后再等就绪即可继续。
    /// PORT: 旧 SendRePressDown（清运行中、置重压位）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public async Task RePressAsync(CancellationToken ct = default)
    {
        await WriteAsync(1, 0, ct);   // 运行中=0：退出本次测试
        await WriteAsync(4, 1, ct);   // 重压RePress=1：请求 PLC 重新压合
        _logger.LogInformation("触发重新压合（重压位=1）");
    }

    /// <summary>
    /// 从 PLC 读当前批次号。<b>TODO：真实批次寄存器地址待国锐文档确认；当前为占位（B+日期）。</b>
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>批次号。</returns>
    public Task<string> ReadBatchNumberAsync(CancellationToken ct = default)
    {
        // TODO: 读真实批次寄存器（多字 → 拼字符串）；现占位
        var batch = "B" + DateTime.Now.ToString("yyyyMMdd");
        _logger.LogInformation("读批次号（占位）{Batch}", batch);
        return Task.FromResult(batch);
    }

    /// <summary>
    /// 断开连接。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        try
        {
            _mc?.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PLC 关闭异常");
        }
        IsConnected = false;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
