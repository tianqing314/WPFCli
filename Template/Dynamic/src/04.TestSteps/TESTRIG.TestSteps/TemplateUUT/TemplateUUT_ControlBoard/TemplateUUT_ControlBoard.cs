using System.Globalization;
using System.IO.Ports;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using R = TESTRIG.Devices.Abstractions.BoxRelayCommand;

namespace TESTRIG.TestSteps.{{DutType}}.{{DutType}}_ControlBoard;

/// <summary>
/// {{DutType}} 控制板（设备族 {{DutType}}）测试**设备特有**处理器集合。逐项**核对并复刻**旧
/// <c>ConST171_MainBoard_Auto.cs</c>（源命名 MainBoard 有误，实为控制板）的实测时序：继电器指令序列
/// （<see cref="BoxRelayCommand"/>）、ConST326 标准表源/测切档、DAM6803D 通道电压、2 路电流表读数、
/// {{DutType}} 被检命令与内联/Range 判定。工装用 <see cref="IConST326StandardBox"/>，被检用 <see cref="I{{DutType}}Dut"/>。
/// </summary>
internal sealed class {{DutType}}Ops
{
    /// <summary>
    /// 测试项上下文。
    /// </summary>
    private readonly ITestContext _ctx;

    /// <summary>
    /// 取消令牌。
    /// </summary>
    private readonly CancellationToken _ct;

    /// <summary>
    /// 标准盒（含 ConST326/继电器/DAM6803D/电流表）。
    /// </summary>
    public readonly IConST326StandardBox Box;

    /// <summary>
    /// 被检 {{DutType}} 专属驱动。
    /// </summary>
    public readonly I{{DutType}}Dut Dut;

    /// <summary>
    /// 从上下文解析标准盒与被检。
    /// </summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    public {{DutType}}Ops(ITestContext ctx, CancellationToken ct)
    {
        _ctx = ctx;
        _ct = ct;
        Box = ctx.GetDevice<IConST326StandardBox>();
        Dut = ctx.GetDevice<I{{DutType}}Dut>();
    }

    /// <summary>
    /// 数值格式化（保留三位有效小数）。
    /// </summary>
    /// <param name="v">数值。</param>
    /// <returns>格式串。</returns>
    public static string F(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 推送实时消息。
    /// </summary>
    /// <param name="m">消息。</param>
    /// <param name="l">级别。</param>
    public void Report(string m, RealtimeLevel l = RealtimeLevel.Info)
    {
        _ctx.Report(m, l);
    }

    /// <summary>
    /// 真机稳定延时（继电器切档/设值后需等待）。PORT: 旧 Thread.Sleep / ScriptHelper.Thread_Sleep。
    /// </summary>
    /// <param name="ms">毫秒。</param>
    public Task Sleep(int ms)
    {
        Report(Box.IsRealHardware ? $"等待 {ms}ms" : $"等待 {ms}ms（仿真跳过）");
        return Box.IsRealHardware ? Task.Delay(ms, _ct) : Task.CompletedTask;
    }

    /// <summary>
    /// 发继电器指令（含 UI 过程日志）。PORT: DSTB.RespondToCommand。
    /// </summary>
    /// <param name="cmd">继电器指令。</param>
    public Task Relay(R cmd)
    {
        Report($"继电器指令：{cmd}");
        return Box.RelayCommandAsync(cmd, _ct);
    }

    /// <summary>
    /// 切换 ConST326 测量档（含 UI 过程日志）。PORT: DSTB.ConST326SetMeasureGear。
    /// </summary>
    /// <param name="gear">测量档。</param>
    public Task SetGear(Gear326 gear)
    {
        Report($"ConST326 切测量档：{gear}");
        return Box.SetMeasureGearAsync(gear, _ct);
    }

    /// <summary>
    /// 读 2 路电流表通道 2 电流。PORT: GetCurrentMeasureValue(false,2)（{{DutType}} 不做 ×10）。
    /// </summary>
    /// <returns>电流。</returns>
    public Task<double> ReadCurrentCh2()
    {
        return Box.GetCurrentMeasureValueAsync(false, 2, _ct);
    }

    /// <summary>
    /// 读 ConST326 当前测量值。PORT: DSTB.ConST326ReadValue。
    /// </summary>
    /// <returns>测量值。</returns>
    public Task<double> Read326()
    {
        return Box.ReadConST326ValueAsync(_ct);
    }

    /// <summary>
    /// 读 DAM6803D 某通道电压。PORT: DSTB.GetVoltageMeasureValue。
    /// </summary>
    /// <param name="channel">通道（0 起）。</param>
    /// <returns>电压。</returns>
    public Task<double> ReadVolt(int channel)
    {
        return Box.GetVoltageMeasureValueAsync(channel, false, _ct);
    }

    /// <summary>
    /// 按名取条件（找不到返回 null）。
    /// </summary>
    /// <param name="name">条件名。</param>
    /// <returns>条件描述符。</returns>
    public ConditionDescriptor? Cond(string name)
    {
        foreach (var c in _ctx.Conditions)
        {
            if (c.Name == name)
            {
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// 对某测量值按指定条件名判定，报「读回+区间+结论」并返回是否通过（条件缺失记为不通过）。
    /// </summary>
    /// <param name="condName">条件名。</param>
    /// <param name="value">测量值。</param>
    /// <param name="label">量名（消息用）。</param>
    /// <param name="unit">单位（消息用）。</param>
    /// <returns>是否通过。</returns>
    public bool Judge(string condName, double value, string label, string unit)
    {
        var cond = Cond(condName);
        if (cond is null)
        {
            Report($"{label} {F(value)}{unit}：缺少判定条件 {condName}", RealtimeLevel.Warn);
            return false;
        }
        var r = _ctx.Evaluator.Evaluate(cond, value);
        Report($"{label} {F(value)}{unit}：{r.Message}", r.Passed ? RealtimeLevel.Info : RealtimeLevel.Warn);
        return r.Passed;
    }
}

/// <summary>
/// 01 工装准备。PORT: BenchPreparation（继电器 A/B/C 全通道复位、A9 选 CHB 24V、C20 CHB 上电，等 10s 重连被检）。
/// </summary>
public sealed class Prep{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Prep{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        await op.Box.ConnectAsync(ct);
        op.Report("标准盒已连接");
        await op.Box.CloseAllAChannelsAsync(ct);
        await op.Box.CloseAllBChannelsAsync(ct);
        await op.Box.CloseAllCChannelsAsync(true, ct);
        await op.Relay(R.继电器A_9档位_CHB_7_8_9_10_11_12_通道电源选择_24V供电);
        await op.Relay(R.继电器C_20档位_CHB_7_8_9_10_11_12_通道电源控制_上电);
        await op.Sleep(10000);

        var connected = false;
        for (var i = 0; i < 6 && !connected; i++)
        {
            connected = await op.Dut.ReplenishLinkAsync(ct);
            if (!connected)
            {
                op.Report($"被检建立连接失败，重试中（第 {i + 1} 次）", RealtimeLevel.Warn);
            }
        }
        op.Report($"被检建立连接：{(connected ? "成功" : "失败")}", connected ? RealtimeLevel.Info : RealtimeLevel.Warn);
        op.Report(connected ? "✓ 工装准备完成" : "✗ 被检建立连接失败", connected ? RealtimeLevel.Success : RealtimeLevel.Error);
        return connected ? StepResult.Pass("工装准备完成") : StepResult.Fail("被检建立连接失败");
    }
}

/// <summary>
/// 02 版本测试。PORT: VersionTest（读控制器软件/硬件版本并记录）。
/// </summary>
public sealed class Version{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Version{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var soft = await op.Dut.ReadCtlVersionAsync(ct);
        var hard = await op.Dut.ReadCtlHardVersionAsync(ct);
        op.Report($"控制器软件版本 {soft}，硬件版本 {hard}");
        op.Report($"✓ 版本读取完成 软件={soft} 硬件={hard}", RealtimeLevel.Success);
        return StepResult.Pass($"版本读取完成 软件={soft} 硬件={hard}", soft);
    }
}

/// <summary>
/// 03 电源轨测试。PORT: PowerSourceTrackTest（326 电压测量档+B14 电压输出；B1~B5/B7 连 E1~E5/E7，逐路读 326 判 Range）。
/// </summary>
public sealed class PowerTrack{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PowerTrack{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>通道继电器与对应指标条件名（E1~E5、E7；跳过 E6）。</summary>
    private static readonly (R Relay, string Cond, string Label)[] Channels =
    [
        (R.继电器B_1档位_测试通道选择_连接E1通道, "E1指标", "E1电压"),
        (R.继电器B_2档位_测试通道选择_连接E2通道, "E2指标", "E2电压"),
        (R.继电器B_3档位_测试通道选择_连接E3通道, "E3指标", "E3电压"),
        (R.继电器B_4档位_测试通道选择_连接E4通道, "E4指标", "E4电压"),
        (R.继电器B_5档位_测试通道选择_连接E5通道, "E5指标", "E5电压"),
        (R.继电器B_7档位_测试通道选择_连接E7通道, "E7指标", "E7电压"),
    ];

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        await op.SetGear(Gear326.V);
        await op.Relay(R.继电器B_14档位_测试类型选择_电压输出);
        ctx.BeginSampling("V", "通道电压");
        var pass = true;
        var idx = 0;
        foreach (var (relay, cond, label) in Channels)
        {
            await op.Relay(relay);
            await op.Sleep(1000);
            var v = await op.Read326();
            ctx.ReportSample(idx++, v);
            pass &= op.Judge(cond, v, label, "V");
        }
        op.Report(pass ? "✓ 电源轨测试通过" : "✗ 电源轨某路超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("电源轨测试通过") : StepResult.Fail("电源轨某路超差");
    }
}

/// <summary>
/// 04 电源状态测试。PORT: PowerStatusTest（被检读 DC24V/BOOST-SENSOR/VACUUM-SENSOR，逐路 Range 判定）。
/// 说明：旧代码把 VACUUM 误判到 BOOST 指标（且 PRE-SENSOR 声明未用），本迁移改为各判各的指标。
/// </summary>
public sealed class PowerStatus{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PowerStatus{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        var d24 = await op.Dut.ReadDC24VoltAsync(ct);
        pass &= op.Judge("D24指标", d24, "DC24V电压", "V");
        var boost = await op.Dut.ReadBoostVoltAsync(ct);
        pass &= op.Judge("BOOST-SENSOR", boost, "BOOST-SENSOR电压", "V");
        var vacuum = await op.Dut.ReadVacuumVoltAsync(ct);
        pass &= op.Judge("VACUUM-SENSOR", vacuum, "VACUUM-SENSOR电压", "V");
        op.Report(pass ? "✓ 电源状态测试通过" : "✗ 电源状态某路超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("电源状态测试通过") : StepResult.Fail("电源状态某路超差");
    }
}

/// <summary>
/// 05 功耗测试。PORT: ConsumeTest（2 路表通道 2 采 30 点，掐头去尾各 5 取均值 Range 判定）。
/// </summary>
public sealed class Consume{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Consume{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        op.Report("开始功耗采样（目标 30 点，间隔 500ms）");
        await op.Sleep(2000);
        var currents = new List<double>();
        ctx.BeginSampling("mA", "功耗电流");
        var i = 0;
        while (currents.Count < 30)
        {
            var v = await op.ReadCurrentCh2() * 1000;
            if (!double.IsNaN(v))
            {
                currents.Add(v);
                ctx.ReportSample(i++, v);
            }
            await op.Sleep(500);
        }

        // PORT: TrimCurrents —— 排序后掐头去尾各 5，取中段均值
        currents.Sort();
        var trimmed = currents.Count > 10 ? currents.GetRange(5, currents.Count - 10) : new List<double>();
        var avg = trimmed.Count > 0 ? trimmed.Average() / Math.Pow(10, 6) : double.NaN;
        op.Report($"采集 {currents.Count} 点，掐头去尾取中段均值 {{{DutType}}Ops.F(avg)}mA");
        var pass = op.Judge("功耗范围", avg, "功耗均值电流", "mA");
        op.Report(pass ? $"✓ 功耗测试通过 均值={{{DutType}}Ops.F(avg)}mA" : $"✗ 功耗超差 均值={{{DutType}}Ops.F(avg)}mA",
            pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass($"功耗测试通过 均值={{{DutType}}Ops.F(avg)}mA", {{DutType}}Ops.F(avg))
                    : StepResult.Fail($"功耗超差 均值={{{DutType}}Ops.F(avg)}mA", {{DutType}}Ops.F(avg));
    }
}

/// <summary>
/// 06 蜂鸣器测试。PORT: BuzzerTest（关蜂鸣器读 326 电压应在不动作区间，开蜂鸣器应在动作区间；326 V 档、B8 连 E8、B14 电压输出）。
/// </summary>
public sealed class Buzzer{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Buzzer{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        try
        {
            await op.Dut.SetBuzzerAsync(false, ct);
            op.Report("关闭蜂鸣器");
            await op.SetGear(Gear326.V);
            await op.Relay(R.继电器B_8档位_测试通道选择_连接E8通道);
            await op.Relay(R.继电器B_14档位_测试类型选择_电压输出);
            await op.Sleep(2000);
            var offVolt = await op.Read326();
            pass &= op.Judge("不动作", offVolt, "关蜂鸣器时 E8 电压", "V");

            await op.Dut.SetBuzzerAsync(true, ct);
            op.Report("打开蜂鸣器");
            await op.Sleep(2000);
            var onVolt = await op.Read326();
            pass &= op.Judge("动作", onVolt, "开蜂鸣器时 E8 电压", "V");
        }
        finally
        {
            await op.Dut.SetBuzzerAsync(false, ct);
        }
        op.Report(pass ? "✓ 蜂鸣器测试通过" : "✗ 蜂鸣器电压超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("蜂鸣器测试通过") : StepResult.Fail("蜂鸣器电压超差");
    }
}

/// <summary>
/// 07 大彩屏串口测试。PORT: ScreenSerialportTest（扫描系统串口，115200 下发大彩屏指令并校验响应头/尾）。
/// 本项不经被检/标准盒，直接扫描 PC 串口。
/// </summary>
public sealed class ScreenSerial{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ScreenSerial{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>下发数据 1（大彩屏首帧）。</summary>
    private static readonly byte[] SendData1 =
    [
        0xEE, 0xB5, 0x00, 0x00, 0x00, 0x02, 0x00, 0x02, 0x00, 0x00, 0x67, 0xD8, 0xFF, 0xFC, 0xFF, 0xFF,
        0xEE, 0xB1, 0x01, 0x00, 0x00, 0xD8, 0x76, 0xFF, 0xFC, 0xFF, 0xFF
    ];

    /// <summary>下发数据 2（大彩屏次帧）。</summary>
    private static readonly byte[] SendData2 =
    [
        0xEE, 0xB5, 0x00, 0x00, 0x00, 0x02, 0x00, 0x02, 0x01, 0x00, 0xF7, 0xD9, 0xFF, 0xFC, 0xFF, 0xFF,
        0xEE, 0xB1, 0x01, 0x00, 0x01, 0x18, 0xB7, 0xFF, 0xFC, 0xFF, 0xFF
    ];

    /// <summary>期望响应头：EE B5。</summary>
    private static readonly byte[] ExpectedHeader = [0xEE, 0xB5];

    /// <summary>期望响应尾：FF FF。</summary>
    private static readonly byte[] ExpectedTail = [0xFF, 0xFF];

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        bool foundPort = false;
        bool commOk = false;
        string foundPortName = string.Empty;

        // PORT: 异步扫描串口
        string[] portNames = SerialPort.GetPortNames();
        op.Report($"开始扫描大彩屏串口，共 {portNames.Length} 个串口");

        foreach (string portName in portNames)
        {
            ct.ThrowIfCancellationRequested();
            SerialPort? sp = null;
            try
            {
                // 使用 Task.Run 将同步串口操作包装为异步
                var result = await Task.Run(async () =>
                {
                    sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
                    sp.ReadTimeout = 3000;
                    sp.WriteTimeout = 3000;
                    sp.Open();

                    if (!sp.IsOpen)
                    {
                        return (Found: false, CommOk: false, PortName: string.Empty);
                    }
                    op.Report($"尝试串口 {portName}...");

                    // 清空缓冲区
                    sp.DiscardInBuffer();
                    sp.DiscardOutBuffer();

                    // 发送第一组数据
                    sp.Write(SendData1, 0, SendData1.Length);
                    await Task.Delay(500, ct);

                    // 读取响应
                    List<byte> receivedData = new List<byte>();
                    DateTime startTime = DateTime.Now;
                    while ((DateTime.Now - startTime).TotalMilliseconds < 2000)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            int data = sp.ReadByte();
                            receivedData.Add((byte)data);
                        }
                        catch (TimeoutException)
                        {
                            break;
                        }
                    }

                    op.Report($"串口 {portName} 收到 {receivedData.Count} 字节");

                    // 检查响应是否包含预期的头和尾
                    if (receivedData.Count >= ExpectedHeader.Length + ExpectedTail.Length)
                    {
                        // 将接收到的数据转换为十六进制字符串
                        string receivedHex = BitConverter.ToString(receivedData.ToArray()).Replace("-", "");
                        string expectedHeaderHex = BitConverter.ToString(ExpectedHeader).Replace("-", "");
                        string expectedTailHex = BitConverter.ToString(ExpectedTail).Replace("-", "");

                        op.Report($"响应数据: {receivedHex}");

                        // 检查是否包含预期的头和尾
                        bool containsHeader = receivedHex.Contains(expectedHeaderHex);
                        bool containsTail = receivedHex.Contains(expectedTailHex);

                        if (containsHeader && containsTail)
                        {
                            // 找到正确串口，发送第二组数据
                            op.Report($"找到大彩屏串口: {portName}");

                            // 清空缓冲区
                            sp.DiscardInBuffer();
                            sp.DiscardOutBuffer();

                            // 发送第二组数据
                            sp.Write(SendData2, 0, SendData2.Length);

                            // 等待5s，检查是否还有数据
                            await Task.Delay(5000, ct);

                            // 检查是否还有数据（仅记录，不影响测试结果）
                            bool noMoreData = sp.BytesToRead == 0;
                            op.Report($"发送第二组数据后，BytesToRead={sp.BytesToRead}，无更多数据={noMoreData}");

                            return (Found: true, CommOk: true, PortName: portName);
                        }
                    }

                    return (Found: false, CommOk: false, PortName: string.Empty);
                }, ct);

                if (result.Found)
                {
                    foundPort = result.Found;
                    commOk = result.CommOk;
                    foundPortName = result.PortName;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 端口被占用或无法打开，跳过
                op.Report($"串口 {portName} 访问失败: {ex.Message}", RealtimeLevel.Warn);
            }
            finally
            {
                if (sp != null && sp.IsOpen)
                {
                    sp.Close();
                }
                sp?.Dispose();
            }
        }

        if (!foundPort)
        {
            op.Report("未找到大彩屏串口", RealtimeLevel.Warn);
            op.Report("✗ 未找到大彩屏串口", RealtimeLevel.Error);
            return StepResult.Fail("未找到大彩屏串口");
        }
        op.Report($"找到大彩屏串口：{foundPortName}，通讯正常={commOk}");
        op.Report(commOk ? $"✓ 大彩屏串口通讯正常（{foundPortName}）" : $"✗ 大彩屏串口通讯异常（{foundPortName}）",
            commOk ? RealtimeLevel.Success : RealtimeLevel.Error);
        return commOk ? StepResult.Pass($"大彩屏串口通讯正常（{foundPortName}）", foundPortName)
                      : StepResult.Fail($"大彩屏串口通讯异常（{foundPortName}）", foundPortName);
    }
}

/// <summary>
/// 08 压力传感器测试。PORT: PressureSensorTest（读增压/真空组件压力，值有效≠0 即传感器通讯正常）。
/// </summary>
public sealed class PressureSensor{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PressureSensor{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var boost = await op.Dut.ReadPressureAsync({{DutType}}Module.Boost, ct);
        var boostOk = Math.Abs(boost) > 1e-9;
        op.Report($"增压组件压力 {{{DutType}}Ops.F(boost)}：传感器通讯{(boostOk ? "正常" : "异常")}");
        var vacuum = await op.Dut.ReadPressureAsync({{DutType}}Module.Vacuum, ct);
        var vacuumOk = Math.Abs(vacuum) > 1e-9;
        op.Report($"真空组件压力 {{{DutType}}Ops.F(vacuum)}：传感器通讯{(vacuumOk ? "正常" : "异常")}");
        op.Report(boostOk && vacuumOk ? "✓ 压力传感器通讯正常" : "✗ 压力传感器通讯异常",
            boostOk && vacuumOk ? RealtimeLevel.Success : RealtimeLevel.Error);
        return boostOk && vacuumOk ? StepResult.Pass("压力传感器通讯正常")
                                   : StepResult.Fail("压力传感器通讯异常");
    }
}

/// <summary>
/// 09 温度传感器测试。PORT: TemperatureSensorTest（读板载/前级/增压 NTC 温度，减环境基准温度判温差）。
/// 逻辑参考 ConST218A 温度芯片测试（<c>TempChip218A</c>）：真机从温湿度计监控服务读环境基准温度
/// （SN 取参数 <c>温湿度计SN1/SN2</c>），三路 NTC 各自 <c>温差=NTC读数-环境温度</c>，按 <c>温差合格</c> Range 判定；
/// 仿真取标称室温 25℃。旧 <c>Helper.GetEnvironmentTemperature(温湿度计SN)</c> 即此环境基准。
/// </summary>
public sealed class TempSensor{{DutType}}Handler : IStepHandler
{
    /// <summary>
    /// 环境温度读取服务（温湿度计监控）。
    /// </summary>
    private readonly IEnvironmentTemperature _envTemp;

    /// <summary>
    /// 注入环境温度服务构造。
    /// </summary>
    /// <param name="envTemp">环境温度读取服务。</param>
    public TempSensor{{DutType}}Handler(IEnvironmentTemperature envTemp)
    {
        _envTemp = envTemp;
    }

    /// <summary>处理的测试项类型。</summary>
    public string Kind => "TempSensor{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);

        // 环境基准温度：真机从温湿度计监控服务读（SN 来自参数 温湿度计SN1/SN2），仿真取标称室温
        double env;
        if (op.Box.IsRealHardware)
        {
            var sns = new[] { ctx.Parameter("温湿度计SN1")?.Value ?? "", ctx.Parameter("温湿度计SN2")?.Value ?? "" };
            var t = await _envTemp.ReadAsync(sns, ct);
            if (t is null)
            {
                op.Report("✗ 环境温度获取失败（温湿度计无新鲜数据）", RealtimeLevel.Error);
                return StepResult.Fail("环境温度获取失败（温湿度计无新鲜数据）");
            }

            env = t.Value;
        }
        else
        {
            env = 25.0;
        }

        op.Report($"环境基准温度 {{{DutType}}Ops.F(env)}℃");

        // 三路 NTC 各自减环境温度得温差，按「温差合格」判定并采曲线
        ctx.BeginSampling("℃", "温差");
        var pass = true;
        var idx = 0;
        foreach (var (read, label) in new (Func<Task<double>>, string)[]
        {
            (() => op.Dut.ReadBoardTemperatureAsync(ct), "板载NTC"),
            (() => op.Dut.ReadTemperatureAsync({{DutType}}Module.Pre, ct), "前级NTC"),
            (() => op.Dut.ReadTemperatureAsync({{DutType}}Module.Boost, ct), "增压NTC"),
        })
        {
            var ntc = await read();
            var diff = ntc - env;
            ctx.ReportSample(idx++, diff);
            op.Report($"{label} {{{DutType}}Ops.F(ntc)}℃，环境 {{{DutType}}Ops.F(env)}℃，温差 {{{DutType}}Ops.F(diff)}℃");
            pass &= op.Judge("温差合格", diff, $"{label}温差", "℃");
        }

        op.Report(pass ? "✓ 温度传感器测试通过" : "✗ 温度传感器温差超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("温度传感器测试通过") : StepResult.Fail("温度传感器温差超差");
    }
}

/// <summary>
/// 10 风扇测试。PORT: FANTest（C5 常闭点设前级风扇占空比、C6 常开点设增压风扇占空比，各等 10s 读转速判区间）。
/// </summary>
public sealed class Fan{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Fan{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pwm = double.TryParse(ctx.Parameter("转速")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 50;
        var pass = true;

        await op.Relay(R.继电器C_5档位_3通道控制_常闭点);
        await op.Dut.SetFanSpeedAsync({{DutType}}Module.Pre, pwm, ct);
        op.Report($"前级风扇占空比 {{{DutType}}Ops.F(pwm)}");
        await op.Sleep(10000);
        var preSpeed = await op.Dut.ReadFanSpeedAsync({{DutType}}Module.Pre, ct);
        pass &= op.Judge("转速范围", preSpeed, "前级风扇转速", "rpm");

        await op.Relay(R.继电器C_6档位_3通道控制_常开点);
        await op.Dut.SetFanSpeedAsync({{DutType}}Module.Boost, pwm, ct);
        op.Report($"增压风扇占空比 {{{DutType}}Ops.F(pwm)}");
        await op.Sleep(10000);
        var boostSpeed = await op.Dut.ReadFanSpeedAsync({{DutType}}Module.Boost, ct);
        pass &= op.Judge("转速范围", boostSpeed, "增压风扇转速", "rpm");

        op.Report(pass ? "✓ 风扇测试通过" : "✗ 风扇转速超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("风扇测试通过") : StepResult.Fail("风扇转速超差");
    }
}

/// <summary>
/// 11 控制阀测试。PORT: ControlValveTest（进整机测试模式；C1 开 V1/V2、C2 开 V3/V4，DAM6803D 通道 0/1 读开/关阀电压判区间）。
/// </summary>
public sealed class ControlValve{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "ControlValve{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        try
        {
            await op.Relay(R.继电器C_1档位_1通道控制_常闭点);
            var diag = await op.Dut.SetDiagnosticTestAsync(true, ct);
            op.Report($"进入整机测试模式：{(diag ? "成功" : "失败")}");

            // V1/V2 开 → 打开区间
            await op.Dut.SetValveAsync({{DutType}}Valve.Boost, true, ct);
            await op.Dut.SetValveAsync({{DutType}}Valve.Pre, true, ct);
            op.Report("开启 V1/V2 阀");
            await op.Sleep(3000);
            pass &= op.Judge("打开", await op.ReadVolt(0), "开V1/V2 通道0电压", "V");
            await op.Sleep(1000);
            pass &= op.Judge("打开", await op.ReadVolt(1), "开V1/V2 通道1电压", "V");

            // V1/V2 关 → 关闭区间
            await op.Dut.SetValveAsync({{DutType}}Valve.Boost, false, ct);
            await op.Dut.SetValveAsync({{DutType}}Valve.Pre, false, ct);
            op.Report("关闭 V1/V2 阀");
            await op.Sleep(3000);
            pass &= op.Judge("关闭", await op.ReadVolt(0), "关V1/V2 通道0电压", "V");
            await op.Sleep(1000);
            pass &= op.Judge("关闭", await op.ReadVolt(1), "关V1/V2 通道1电压", "V");

            // V3/V4 开 → 打开区间
            await op.Relay(R.继电器C_2档位_1通道控制_常开点);
            await op.Dut.SetValveAsync({{DutType}}Valve.Vacuum1, true, ct);
            await op.Dut.SetValveAsync({{DutType}}Valve.Vacuum2, true, ct);
            op.Report("开启 V3/V4 阀");
            await op.Sleep(3000);
            pass &= op.Judge("打开", await op.ReadVolt(0), "开V3/V4 通道0电压", "V");
            await op.Sleep(1000);
            pass &= op.Judge("打开", await op.ReadVolt(1), "开V3/V4 通道1电压", "V");

            // V3/V4 关 → 关闭区间
            await op.Dut.SetValveAsync({{DutType}}Valve.Vacuum1, false, ct);
            await op.Dut.SetValveAsync({{DutType}}Valve.Vacuum2, false, ct);
            op.Report("关闭 V3/V4 阀");
            await op.Sleep(3000);
            pass &= op.Judge("关闭", await op.ReadVolt(0), "关V3/V4 通道0电压", "V");
            await op.Sleep(1000);
            pass &= op.Judge("关闭", await op.ReadVolt(1), "关V3/V4 通道1电压", "V");
        }
        finally
        {
            await op.Dut.SetDiagnosticTestAsync(false, ct);
        }
        op.Report(pass ? "✓ 控制阀测试通过" : "✗ 控制阀电压超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("控制阀测试通过") : StepResult.Fail("控制阀电压超差");
    }
}

/// <summary>
/// 12 真空组件泵控制测试。PORT: VacuumControlTest（C4 常开点，开真空泵，DAM6803D 通道 3/4/5 读电压判区间；收尾关泵）。
/// </summary>
public sealed class VacuumCtl{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "VacuumCtl{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        try
        {
            await op.Relay(R.继电器C_4档位_2通道控制_常开点);
            await op.Dut.SetPumpAsync({{DutType}}Module.Vacuum, true, ct);
            op.Report("控制真空组件泵动作");
            foreach (var ch in new[] { 3, 4, 5 })
            {
                pass &= op.Judge("电压范围", await op.ReadVolt(ch), $"真空泵通道{ch}电压", "V");
            }
        }
        finally
        {
            await op.Dut.SetPumpAsync({{DutType}}Module.Vacuum, false, ct);
            op.Report("关闭真空组件泵动作");
        }
        op.Report(pass ? "✓ 真空组件泵控制通过" : "✗ 真空组件泵电压超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("真空组件泵控制通过") : StepResult.Fail("真空组件泵电压超差");
    }
}

/// <summary>
/// 13 增压组件泵控制测试。PORT: BoostControlTest（C3 常闭点，开增压泵，DAM6803D 通道 3/4/5 读电压判区间；收尾关泵）。
/// </summary>
public sealed class BoostCtl{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "BoostCtl{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        try
        {
            await op.Relay(R.继电器C_3档位_2通道控制_常闭点);
            await op.Dut.SetPumpAsync({{DutType}}Module.Boost, true, ct);
            op.Report("控制增压组件泵动作");
            foreach (var ch in new[] { 3, 4, 5 })
            {
                pass &= op.Judge("电压范围", await op.ReadVolt(ch), $"增压泵通道{ch}电压", "V");
            }
        }
        finally
        {
            await op.Dut.SetPumpAsync({{DutType}}Module.Boost, false, ct);
            op.Report("关闭增压组件泵动作");
        }
        op.Report(pass ? "✓ 增压组件泵控制通过" : "✗ 增压组件泵电压超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("增压组件泵控制通过") : StepResult.Fail("增压组件泵电压超差");
    }
}

/// <summary>
/// 14 前级组件泵控制测试。PORT: PreControlTest（C3 常闭点，开泵，DAM6803D 通道 6/7/8 读电压判区间；收尾关泵）。
/// 注：旧代码此项开/关的是增压(Pressure)泵（疑似复制粘贴遗留），本迁移忠实复刻；现场如需前级泵请改 <see cref="{{DutType}}Module.Pre"/>。
/// </summary>
public sealed class PreCtl{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "PreCtl{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;
        try
        {
            await op.Relay(R.继电器C_3档位_2通道控制_常闭点);
            await op.Dut.SetPumpAsync({{DutType}}Module.Boost, true, ct);
            op.Report("控制前级组件泵动作");
            foreach (var ch in new[] { 6, 7, 8 })
            {
                pass &= op.Judge("电压范围", await op.ReadVolt(ch), $"前级泵通道{ch}电压", "V");
            }
        }
        finally
        {
            await op.Dut.SetPumpAsync({{DutType}}Module.Vacuum, false, ct);
            op.Report("关闭前级组件泵动作");
        }
        op.Report(pass ? "✓ 前级组件泵控制通过" : "✗ 前级组件泵电压超差", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("前级组件泵控制通过") : StepResult.Fail("前级组件泵电压超差");
    }
}

/// <summary>
/// 15 FOC驱动测试。PORT: FOCDriverTest（故障码非零先重连；进整机测试模式，读泵电压判区间，前级/增压泵运行后故障码应>100000）。
/// </summary>
public sealed class FocDriver{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "FocDriver{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var pass = true;

        // PORT: 初始故障码非零则重连重试（旧递归 BenchPreparation），此处限次数
        for (var i = 0; i < 3; i++)
        {
            var (pre0, boost0) = await op.Dut.ReadPumpFaultCodeAsync(ct);
            if (pre0 == 0 && boost0 == 0)
            {
                break;
            }
            op.Report($"初始故障码非零（前级={{{DutType}}Ops.F(pre0)} 增压={{{DutType}}Ops.F(boost0)}），重连重试", RealtimeLevel.Warn);
            await op.Dut.ReplenishLinkAsync(ct);
            await op.Sleep(3000);
        }

        try
        {
            var diag = await op.Dut.SetDiagnosticTestAsync(true, ct);
            op.Report($"开启整机测试模式：{(diag ? "成功" : "失败")}");

            var (preV, boostV) = await op.Dut.ReadPumpVoltageAsync(ct);
            pass &= op.Judge("电压范围", preV, "前级泵电压", "V");
            pass &= op.Judge("电压范围", boostV, "增压泵电压", "V");

            await op.Dut.SetPumpAsync({{DutType}}Module.Pre, true, ct);
            op.Report("控制前级泵动作");
            await op.Sleep(10000);
            var (preFc, _) = await op.Dut.ReadPumpFaultCodeAsync(ct);
            var preOk = preFc > 100000;
            op.Report($"前级泵运行后故障码 {{{DutType}}Ops.F(preFc)}（期望>100000）→ {(preOk ? "正常" : "异常")}");
            pass &= preOk;

            await op.Dut.SetPumpAsync({{DutType}}Module.Boost, true, ct);
            op.Report("控制增压泵动作");
            await op.Sleep(10000);
            var (_, boostFc) = await op.Dut.ReadPumpFaultCodeAsync(ct);
            var boostOk = boostFc > 100000;
            op.Report($"增压泵运行后故障码 {{{DutType}}Ops.F(boostFc)}（期望>100000）→ {(boostOk ? "正常" : "异常")}");
            pass &= boostOk;
        }
        finally
        {
            await op.Dut.SetPumpAsync({{DutType}}Module.Pre, false, ct);
            await op.Dut.SetPumpAsync({{DutType}}Module.Boost, false, ct);
            await op.Dut.SetDiagnosticTestAsync(false, ct);
        }
        op.Report(pass ? "✓ FOC驱动测试通过" : "✗ FOC驱动异常", pass ? RealtimeLevel.Success : RealtimeLevel.Error);
        return pass ? StepResult.Pass("FOC驱动测试通过") : StepResult.Fail("FOC驱动异常");
    }
}

/// <summary>
/// 16 FOC芯片通讯测试。PORT: FOCCommunicationTest（读增压/前级组件 FOC 芯片状态是否正常）。
/// </summary>
public sealed class FocComm{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "FocComm{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        var boostOk = await op.Dut.IsFocNormalAsync({{DutType}}Module.Boost, ct);
        op.Report($"增压组件 FOC 芯片状态：{(boostOk ? "正常" : "异常")}");
        var preOk = await op.Dut.IsFocNormalAsync({{DutType}}Module.Pre, ct);
        op.Report($"前级组件 FOC 芯片状态：{(preOk ? "正常" : "异常")}");
        op.Report(boostOk && preOk ? "✓ FOC芯片通讯正常" : "✗ FOC芯片通讯异常",
            boostOk && preOk ? RealtimeLevel.Success : RealtimeLevel.Error);
        return boostOk && preOk ? StepResult.Pass("FOC芯片通讯正常") : StepResult.Fail("FOC芯片通讯异常");
    }
}

/// <summary>
/// 17 测试结束。PORT: TestFinish（继电器 C9 常闭点、C19 CHB 断电，复位工装）。
/// </summary>
public sealed class Finish{{DutType}}Handler : IStepHandler
{
    /// <summary>处理的测试项类型。</summary>
    public string Kind => "Finish{{DutType}}";
    /// <summary>限定设备家族（仅 {{DutType}} 的板使用）。</summary>
    public string? DeviceFamily => "{{ProductCode}}";

    /// <summary>执行本测试项。</summary>
    /// <param name="ctx">测试项上下文。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测试项结果。</returns>
    public async Task<StepResult> ExecuteAsync(ITestContext ctx, CancellationToken ct = default)
    {
        var op = new {{DutType}}Ops(ctx, ct);
        await op.Relay(R.继电器C_9档位_5通道控制_常闭点);
        await op.Relay(R.继电器C_19档位_CHB_7_8_9_10_11_12_通道电源控制_断电);
        op.Report("✓ 测试完成", RealtimeLevel.Success);
        return StepResult.Pass("测试完成");
    }
}
