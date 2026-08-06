using System.Globalization;
using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.ConST171.Data;

namespace TESTRIG.Devices.Dut.{{DutType}};

/// <summary>
/// {{DutType}} 控制板（设备族 {{DutType}}）被检**真机驱动**：走 Xmas11 <see cref="ConST171Base"/> 通讯库，命令层**逐条迁移**自旧
/// <c>Bots.TestBench.Device.ConST171CommonBase</c>（内部转调 <c>ConST171Base.*</c>，返回 <c>iResponse</c>）。
/// 连接按 manifest 号位 <see cref="CommEndpoint"/>（默认网络，对齐旧 ConST171CommonBase.InitEthernetConfig）建连。
/// 每条命令 <c>iResponse.IsCorrect=false</c> 即抛 <see cref="DeviceCommException"/>，交引擎按异常收尾。
/// </summary>
[DutDriver("171AKZB")]
public sealed class {{DutType}}Dut : I{{DutType}}Dut
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 连接端点（号位 Comm）。
    /// </summary>
    private readonly CommEndpoint? _comm;

    /// <summary>
    /// {{DutType}} 通讯实例（连接后有值）。
    /// </summary>
    private ConST171Base? _dev;

    /// <summary>
    /// 用设备描述符构造真机 {{DutType}} 被检（端点取号位 Comm）。
    /// </summary>
    /// <param name="descriptor">设备描述符（含号位 Comm）。</param>
    /// <param name="logger">日志。</param>
    public {{DutType}}Dut(DeviceDescriptor descriptor, ILogger logger)
    {
        _logger = logger;
        Key = descriptor.Model;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
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
    /// 取 {{DutType}} 实例，未连接抛 <see cref="DeviceCommException"/>（CommunicationError）。
    /// </summary>
    private ConST171Base Dev => _dev ?? throw new DeviceCommException("{{DutType}} 未连接", TestResultStatus.CommunicationError);

    /// <summary>
    /// 连接被检：按端点（网络/串口）建 ConST171Base，Open + IsExist 探活。PORT: 旧 ConST171CommonBase.Open()。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                _dev?.Close();
            }
            catch
            {
                // 忽略关闭异常
            }

            _dev = Build(_comm);
            var opened = _dev.Open();
            IsConnected = opened && _dev.IsExist();
            if (IsConnected)
            {
                _logger.LogInformation("{{DutType}} 真机连接成功");
            }
            else
            {
                _logger.LogWarning("{{DutType}} 连接未就绪（将重试）");
            }
        }, ct);
    }

    /// <summary>
    /// 按端点构造 ConST171Base（网络/串口）。串口先做占用预检。PORT: 旧默认走以太网。
    /// </summary>
    /// <param name="ep">连接端点。</param>
    /// <returns>{{DutType}} 通讯实例。</returns>
    private static ConST171Base Build(CommEndpoint? ep)
    {
        if (ep is null || ep.Link == LinkType.Ethernet)
        {
            var ip = ep?.Ip ?? Environment.GetEnvironmentVariable("TESTRIG_DUT_IP") ?? "192.168.40.107";
            var port = ep?.Port ?? int.Parse(Environment.GetEnvironmentVariable("TESTRIG_DUT_PORT") ?? "1030", CultureInfo.InvariantCulture);
            return new ConST171Base(IPAddress.Parse(ip), port);
        }

        if (ep.Link == LinkType.Serial)
        {
            var sp = ep.Serial ?? new SerialParams();
            var portName = string.IsNullOrWhiteSpace(ep.PhysicalLink) ? "COM1" : ep.PhysicalLink!;
            var probe = Comm.SerialPortProbe.Probe(portName);
            if (!probe.Ok)
            {
                throw new DeviceCommException($"被检串口不可用：{probe.Message}", TestResultStatus.CommunicationError);
            }
            var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;
            var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;
            return new ConST171Base(portName, sp.Baud, sp.DataBits, stopBits, parity);
        }

        throw new DeviceCommException("{{DutType}} 不支持 USB 连接（旧平台默认以太网）", TestResultStatus.CommunicationError);
    }

    /// <summary>
    /// 补充连接（重连）。PORT: 旧 ConST171CommonBase.ReplenishLink。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否连接成功。</returns>
    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)
    {
        await ConnectAsync(ct);
        return IsConnected;
    }

    // ===== 模块/阀门枚举映射（抽象层枚举 → Xmas11 枚举）=====

    /// <summary>抽象模块 → Xmas11 ModuleName（Boost=增压→Pressure）。</summary>
    /// <param name="m">抽象模块。</param>
    /// <returns>Xmas11 模块名。</returns>
    private static ModuleName Map({{DutType}}Module m)
    {
        return m switch
        {
            {{DutType}}Module.Pre => ModuleName.Pre,
            {{DutType}}Module.Boost => ModuleName.Pressure,
            {{DutType}}Module.Vacuum => ModuleName.Vacuum,
            _ => ModuleName.Pre,
        };
    }

    /// <summary>抽象阀门 → Xmas11 ValveName。</summary>
    /// <param name="v">抽象阀门。</param>
    /// <returns>Xmas11 阀门名。</returns>
    private static ValveName Map({{DutType}}Valve v)
    {
        return v switch
        {
            {{DutType}}Valve.Boost => ValveName.Boost,
            {{DutType}}Valve.Pre => ValveName.Pre,
            {{DutType}}Valve.Vacuum1 => ValveName.Vacuu1,
            {{DutType}}Valve.Vacuum2 => ValveName.Vacuu2,
            _ => ValveName.Boost,
        };
    }

    /// <summary>bool → OpenCloseState。</summary>
    /// <param name="open">是否开。</param>
    /// <returns>开关状态。</returns>
    private static OpenCloseState State(bool open)
    {
        return open ? OpenCloseState.Open : OpenCloseState.Close;
    }

    // ===== 命令层（逐条转调 ConST171Base，失败抛通讯异常）=====

    /// <summary>读控制器软件版本。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    public Task<string> ReadCtlVersionAsync(CancellationToken ct = default)
    {
        return Str(() => Dev.GetCONTrolsoftversion(), "读取控制器软件版本", ct);
    }

    /// <summary>读控制器硬件版本。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    public Task<string> ReadCtlHardVersionAsync(CancellationToken ct = default)
    {
        return Str(() => Dev.GetCONTrolhardversion(), "读取控制器硬件版本", ct);
    }

    /// <summary>读 DC24V 电压。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    public Task<double> ReadDC24VoltAsync(CancellationToken ct = default)
    {
        return Volt(() => Dev.GetDC24Volt(), "读取DC24V电压", ct);
    }

    /// <summary>读 BOOST-SENSOR 电压。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    public Task<double> ReadBoostVoltAsync(CancellationToken ct = default)
    {
        return Volt(() => Dev.GetBoostVolt(), "读取BOOST-SENSOR电压", ct);
    }

    /// <summary>读 VACUUM-SENSOR 电压。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    public Task<double> ReadVacuumVoltAsync(CancellationToken ct = default)
    {
        return Volt(() => Dev.GetVacuumVolt(), "读取VACUUM-SENSOR电压", ct);
    }

    /// <summary>设置蜂鸣器开/关。</summary>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetBuzzerAsync(bool open, CancellationToken ct = default)
    {
        return Void(() => Dev.SetBuzzer(State(open)), open ? "打开蜂鸣器" : "关闭蜂鸣器", ct);
    }

    /// <summary>读某组件压力值（读失败/无效返回 0）。</summary>
    /// <param name="module">组件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>压力值。</returns>
    public Task<double> ReadPressureAsync({{DutType}}Module module, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.GetPressure(Map(module));
            return r.IsCorrect ? r.Result.Value : 0d;
        }, ct);
    }

    /// <summary>读板载 NTC 温度。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>温度（℃）。</returns>
    public Task<double> ReadBoardTemperatureAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.GetBoardTemperature();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException("读取板载温度失败", TestResultStatus.CommunicationError);
            }
            return r.Result.Value;
        }, ct);
    }

    /// <summary>读某组件 NTC 温度。</summary>
    /// <param name="module">组件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>温度（℃）。</returns>
    public Task<double> ReadTemperatureAsync({{DutType}}Module module, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.GetTemperature(Map(module));
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"读取{module}温度失败", TestResultStatus.CommunicationError);
            }
            return r.Result.Value;
        }, ct);
    }

    /// <summary>设置某组件风扇占空比。</summary>
    /// <param name="module">组件。</param>
    /// <param name="pwm">占空比。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetFanSpeedAsync({{DutType}}Module module, double pwm, CancellationToken ct = default)
    {
        return Void(() => Dev.SetFanSpeed(Map(module), pwm), $"设置{module}风扇占空比", ct);
    }

    /// <summary>读某组件风扇转速。</summary>
    /// <param name="module">组件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>转速。</returns>
    public Task<double> ReadFanSpeedAsync({{DutType}}Module module, CancellationToken ct = default)
    {
        return Num(() => Dev.GetFanSpeed(Map(module)), $"读取{module}风扇转速", ct);
    }

    /// <summary>设置整机测试模式（下发+回读校验，失败重连重试最多 3 次）。</summary>
    /// <param name="open">true 进入，false 退出。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否设置成功。</returns>
    public Task<bool> SetDiagnosticTestAsync(bool open, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var want = State(open);
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Dev.SetDiagnosticTest(want);
                    var cur = Dev.GetDiagnosticTestState();
                    if (cur.IsCorrect && cur.Result == want)
                    {
                        return true;
                    }
                }
                catch
                {
                    // 落到重连
                }

                await ReplenishLinkAsync(ct);
                await Task.Delay(1000, ct);
            }
            return false;
        }, ct);
    }

    /// <summary>设置阀门开/关。</summary>
    /// <param name="valve">阀门。</param>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否下发成功。</returns>
    public Task<bool> SetValveAsync({{DutType}}Valve valve, bool open, CancellationToken ct = default)
    {
        return Ok(() => Dev.SetValveStatus(Map(valve), State(open)), ct);
    }

    /// <summary>设置某组件泵开/关。</summary>
    /// <param name="module">组件。</param>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否下发成功。</returns>
    public Task<bool> SetPumpAsync({{DutType}}Module module, bool open, CancellationToken ct = default)
    {
        return Ok(() => Dev.SetPumpStatus(Map(module), State(open)), ct);
    }

    /// <summary>读增压/前级泵工作电压。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(前级电压, 增压电压)。</returns>
    public Task<(double Pre, double Boost)> ReadPumpVoltageAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.GetPumpVoltage();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException("读取泵电压失败", TestResultStatus.CommunicationError);
            }
            return (r.Result.PreVoltage.Value, r.Result.BoostVoltage.Value);
        }, ct);
    }

    /// <summary>读泵控制器故障码。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(前级故障码, 增压故障码)。</returns>
    public Task<(double Pre, double Boost)> ReadPumpFaultCodeAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.GetPumpControllerFaultCode();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException("读取泵故障码失败", TestResultStatus.CommunicationError);
            }
            return (r.Result.PreFaultCode, r.Result.BoostFaultCode);
        }, ct);
    }

    /// <summary>读某组件 FOC 芯片状态是否正常。</summary>
    /// <param name="module">组件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否正常。</returns>
    public Task<bool> IsFocNormalAsync({{DutType}}Module module, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var r = Dev.IsFOCNormal(Map(module));
            return r.IsCorrect && r.Result;
        }, ct);
    }

    // ===== IDutDevice 通用面 =====

    /// <summary>读被检序列号。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>序列号。</returns>
    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)
    {
        return Str(() => Dev.GetSerialNumber(), "读序列号", ct);
    }

    /// <summary>读被检固件版本（控制器软件版本）。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本串。</returns>
    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)
    {
        return ReadCtlVersionAsync(ct);
    }

    /// <summary>写初始信息（控制板无此步，空实现）。</summary>
    /// <param name="boardType">板卡类型。</param>
    /// <param name="ct">取消令牌。</param>
    public Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>读一路测量值（控制板无通用测点，返回 DC24V）。</summary>
    /// <param name="point">测量点（未用）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值。</returns>
    public Task<double> MeasureAsync(string point, CancellationToken ct = default)
    {
        return ReadDC24VoltAsync(ct);
    }

    /// <summary>关闭连接。</summary>
    public ValueTask DisposeAsync()
    {
        try
        {
            _dev?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{{DutType}} 关闭异常");
        }
        IsConnected = false;
        return ValueTask.CompletedTask;
    }

    // ===== iResponse 包装：失败抛 DeviceCommException =====

    /// <summary>执行一条返回数值的命令，失败抛通讯异常。</summary>
    /// <param name="call">调用（返回 iResponse&lt;double&gt;）。</param>
    /// <param name="what">操作描述。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>数值。</returns>
    private Task<double> Num(Func<iResponse<double>> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"{what}失败", TestResultStatus.CommunicationError);
            }
            return r.Result;
        }, ct);
    }

    /// <summary>执行一条返回 <c>Voltage</c> 的命令，取 .Value；失败抛通讯异常。</summary>
    /// <param name="call">调用。</param>
    /// <param name="what">操作描述。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压（V）。</returns>
    private Task<double> Volt(Func<iResponse<Xmas11.Domain.Electricity.Voltage>> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"{what}失败", TestResultStatus.CommunicationError);
            }
            return r.Result.Value;
        }, ct);
    }

    /// <summary>执行一条返回字符串的命令，失败抛通讯异常。</summary>
    /// <param name="call">调用。</param>
    /// <param name="what">操作描述。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>字符串。</returns>
    private Task<string> Str(Func<iResponse<string>> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"{what}失败", TestResultStatus.CommunicationError);
            }
            return r.Result;
        }, ct);
    }

    /// <summary>执行一条无返回值命令，失败抛通讯异常。</summary>
    /// <param name="call">调用（返回 iResponse）。</param>
    /// <param name="what">操作描述。</param>
    /// <param name="ct">取消令牌。</param>
    private Task Void(Func<iResponse> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"{what}失败", TestResultStatus.CommunicationError);
            }
        }, ct);
    }

    /// <summary>执行一条无返回值命令，返回是否成功（不抛异常，供 Set 类命令判定）。</summary>
    /// <param name="call">调用（返回 iResponse）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否成功。</returns>
    private Task<bool> Ok(Func<iResponse> call, CancellationToken ct)
    {
        return Task.Run(() => call().IsCorrect, ct);
    }
}
