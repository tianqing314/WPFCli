using System.Globalization;
using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.ConST171;
using Xmas11.Comm.Devices.ConST171.Data;

namespace TESTRIG.Devices.Dut.ConST171;

/// <summary>
/// ConST171（P27 设备族，整机 ConST171A）被检真机驱动。**人工填充**自旧
/// <c>Bots.TestBench.Device.P27CommonBase</c>：串口走 Xmas11 <c>ConST171Base</c>，
/// 命令执行失败抛 <see cref="DeviceCommException"/>（由引擎按异常收尾并落盘）。
/// </summary>
[DutDriver("ConST171")]
public sealed class ConST171Dut : IConST171Dut
{
    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// ConST171 通讯实例（真机）。
    /// </summary>
    private ConST171Base? _dev;

    /// <summary>
    /// 号位连接端点。
    /// </summary>
    private readonly CommEndpoint? _comm;

    /// <summary>
    /// 设备型号。
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// 设备 Key（型号）。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 构造 ConST171 被检驱动。
    /// </summary>
    /// <param name="descriptor">设备描述符（含号位 Comm）。</param>
    /// <param name="logger">日志。</param>
    public ConST171Dut(DeviceDescriptor descriptor, ILogger logger)
    {
        _logger = logger;
        Key = descriptor.Model;
        Model = descriptor.Model;
        _comm = descriptor.Comm;
    }

    /// <summary>
    /// 连接被检：按端点（串口）建 ConST171Base，Open 探活。PORT: 旧 P27CommonBase.Open()。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try { _dev?.Close(); } catch { }
            _dev = Build(_comm);
            var opened = _dev.Open();
            IsConnected = opened && _dev.Connected && _dev.IsExist();
            _logger.LogInformation(IsConnected ? "ConST171 真机连接成功" : "ConST171 连接未就绪（将重试）");
        }, ct);
    }

    /// <summary>
    /// 补充连接（重连）。PORT: 旧 P27CommonBase.ReplenishLink。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>是否连接成功。</returns>
    public async Task<bool> ReplenishLinkAsync(CancellationToken ct = default)
    {
        await ConnectAsync(ct);
        return IsConnected;
    }

    /// <summary>
    /// 按端点构造 ConST171Base（整机 ConST171A 为串口 115200/Two/None；网络预留）。
    /// </summary>
    /// <param name="ep">连接端点。</param>
    /// <returns>通讯实例。</returns>
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
            var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.Two;
            var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;
            return new ConST171Base(portName, sp.Baud, sp.DataBits, stopBits, parity);
        }

        throw new DeviceCommException("ConST171 不支持 USB 连接（整机默认串口扫描）", TestResultStatus.CommunicationError);
    }

    /// <summary>
    /// 通讯实例（未连接抛通讯异常）。
    /// </summary>
    private ConST171Base Dev => _dev ?? throw new DeviceCommException("ConST171 未连接", TestResultStatus.CommunicationError);

    /// <summary>
    /// 模块名 → 枚举（默认正压）。
    /// </summary>
    private static ModuleName Module(string module)
        => module.Equals("Vacuum", StringComparison.OrdinalIgnoreCase) ? ModuleName.Vacuum : ModuleName.Pressure;

    /// <summary>
    /// 开关布尔 → OpenCloseState。
    /// </summary>
    private static OpenCloseState State(bool open) => open ? OpenCloseState.Open : OpenCloseState.Close;

    /// <summary>
    /// iResponse 失败抛通讯异常。
    /// </summary>
    private static void Check(iResponse r, string what)
    {
        if (!r.IsCorrect)
        {
            throw new DeviceCommException($"{what}失败：{r.ErrorCode} {r.GetContent(true, true)}", TestResultStatus.HardwareError);
        }
    }

    // ===== 信息设置（Set + 回读验证） =====

    /// <summary>设置系统语言并回读验证。</summary>
    public Task<bool> SetSystemLanguageAsync(string language, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var set = Enum.TryParse<LanguageSet>(language, true, out var lang) ? lang : LanguageSet.zh_CN;
            var rSet = Dev.SetSystemLanguage(set);
            Check(rSet, "设置系统语言");
            var rGet = Dev.GetSystemLanguage();
            Check(rGet, "回读系统语言");
            return rGet.Result == set;
        }, ct);

    /// <summary>设置开机 LOGO 并回读验证。</summary>
    public Task<bool> SetLogoInfoAsync(string logo, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var set = Enum.TryParse<LogoSet>(logo, true, out var lg) ? lg : LogoSet.ConST;
            var rSet = Dev.SetLogoInfo(set);
            Check(rSet, "设置开机 LOGO");
            var rGet = Dev.GetLogoInfo();
            Check(rGet, "回读开机 LOGO");
            return rGet.Result == set;
        }, ct);

    /// <summary>设置设备类型并回读验证。</summary>
    public Task<bool> SetDeviceTypeAsync(string deviceType, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var rSet = Dev.SetDeviceType(deviceType);
            Check(rSet, "设置设备类型");
            var rGet = Dev.GetDeviceType();
            Check(rGet, "回读设备类型");
            return rGet.Result == deviceType;
        }, ct);

    /// <summary>设置 MCU 串口参数。</summary>
    public Task<bool> SetMCUBaudrateAsync(int baudrate, int databits, string stopBits, string parity, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var sb = Enum.TryParse<StopBits>(stopBits, true, out var s) ? s : StopBits.One;
            var pa = Enum.TryParse<Parity>(parity, true, out var p) ? p : Parity.None;
            Check(Dev.SetMCUBaudrate(baudrate, databits, sb, pa), "设置 MCU 串口参数");
            return true;
        }, ct);

    /// <summary>写入整机序列号并回读验证。</summary>
    public Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetSerialNumber(serialNumber), "写入序列号");
            var rGet = Dev.GetSerialNumber();
            Check(rGet, "回读序列号");
            return rGet.Result == serialNumber;
        }, ct);

    /// <summary>启动屏幕自测（切屏）。</summary>
    public Task<bool> ChangeScreenAsync(string screen, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var item = screen.ToLowerInvariant() switch
            {
                "touchtest" => ScreenItem.TouchTestScreen,
                "lighttest" => ScreenItem.LightTestScreen,
                "speakertest" => ScreenItem.SpeakerTestScreen,
                _ => ScreenItem.BadPointTestScreen,
            };
            Check(Dev.ChangeScreenChannel(item), "启动屏幕自测");
            return true;
        }, ct);

    /// <summary>读屏幕自测结果。</summary>
    public Task<string> GetScreenResultAsync(string screen, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var item = screen.ToLowerInvariant() switch
            {
                "touchtest" => ScreenItem.TouchTestScreen,
                "lighttest" => ScreenItem.LightTestScreen,
                "speakertest" => ScreenItem.SpeakerTestScreen,
                _ => ScreenItem.BadPointTestScreen,
            };
            var r = Dev.GetScreenResult(item);
            Check(r, "读屏幕自测结果");
            return r.Result.ToString();
        }, ct);

    /// <summary>设置屏幕亮度。</summary>
    public Task<bool> SetScreenBrightnessAsync(double value, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetScreenBRIG(value), "设置屏幕亮度");
            return true;
        }, ct);

    /// <summary>设置系统声音开关。</summary>
    public Task<bool> SetSystemSoundAsync(bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetSystemSound(State(open)), "设置系统声音");
            return true;
        }, ct);

    /// <summary>设置正压气源静音模式并回读验证。</summary>
    public Task<bool> SetPressureMuteAsync(bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var rSet = Dev.SetPressureMute(State(open));
            Check(rSet, "设置静音模式");
            var rGet = Dev.GetPressureMuteState();
            Check(rGet, "回读静音模式");
            return rGet.Result == State(open);
        }, ct);

    /// <summary>设置真空气源开机排水模式。</summary>
    public Task<bool> SetPressureVacuumVentAsync(bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetPressureVacuumVent(State(open)), "设置真空排水模式");
            return true;
        }, ct);

    /// <summary>设置常开阀（排气）状态，失败重连重试最多 3 次。</summary>
    public Task<bool> SetPressureVentAsync(bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var want = State(open);
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Check(Dev.SetPressureVent(want), "设置排气阀");
                    var rGet = Dev.GetPressureVentState();
                    if (rGet.IsCorrect && rGet.Result == want)
                    {
                        return true;
                    }
                }
                catch
                {
                    // 重连后重试
                }
                try { Dev.Close(); } catch { }
                Dev.Open();
            }
            return false;
        }, ct);

    // ===== 压力/泵控制 =====

    /// <summary>设置造压范围（kPa）。</summary>
    public Task<bool> SetPressureRangeAsync(string module, double min, double max, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetPressureRange(Module(module), min, max), $"设置{module}造压范围");
            return true;
        }, ct);

    /// <summary>设置泵状态。</summary>
    public Task<bool> SetPumpStatusAsync(string module, bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetPumpStatus(Module(module), State(open)), $"设置{module}泵状态");
            return true;
        }, ct);

    /// <summary>开始/停止控压。</summary>
    public Task<bool> SetControlStateAsync(string module, bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetControlState(Module(module), State(open)), $"{(open ? "开始" : "停止")}{module}控压");
            return true;
        }, ct);

    /// <summary>设置吹扫测试模式。</summary>
    public Task<bool> SetBlowTestAsync(bool open, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetBlowTest(State(open)), "设置吹扫测试模式");
            return true;
        }, ct);

    /// <summary>设置风扇转速（pwm 0~1）。</summary>
    public Task<bool> SetFanSpeedAsync(string module, double pwm, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetFanSpeed(Module(module), pwm), $"设置{module}风扇转速");
            return true;
        }, ct);

    /// <summary>读风扇转速（rpm）。</summary>
    public Task<double> GetFanSpeedAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetFanSpeed(Module(module));
            Check(r, $"读{module}风扇转速");
            return r.Result;
        }, ct);

    /// <summary>读当前压力（kPa）。</summary>
    public Task<double> GetPressureAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetPressure(Module(module));
            Check(r, $"读{module}压力");
            return r.Result.Value;
        }, ct);

    /// <summary>读校准压力（kPa）。</summary>
    public Task<double> GetCalPressureAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetCalPressure(Module(module));
            Check(r, $"读{module}校准压力");
            return r.Result.Value;
        }, ct);

    /// <summary>读控制板温度（℃）。</summary>
    public Task<double> GetBoardTemperatureAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetBoardTemperature();
            Check(r, "读控制板温度");
            return r.Result.Value;
        }, ct);

    /// <summary>读模块温度（℃）。</summary>
    public Task<double> GetTemperatureAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetTemperature(Module(module));
            Check(r, $"读{module}温度");
            return r.Result.Value;
        }, ct);

    // ===== 校准 =====

    /// <summary>进入校准模式。</summary>
    public Task<bool> StartCalibrationAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.StartClibration(Module(module)), $"进入{module}校准模式");
            return true;
        }, ct);

    /// <summary>设置校准值（标准表读数）。</summary>
    public Task<bool> SetCalibrationValueAsync(string module, double value, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetClibrationValue(Module(module), value), $"设置{module}校准值");
            return true;
        }, ct);

    /// <summary>退出校准模式。</summary>
    public Task<bool> StopCalibrationAsync(string module, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.StopClibration(Module(module)), $"退出{module}校准模式");
            return true;
        }, ct);

    /// <summary>写入多点校准数据。</summary>
    public Task<bool> SetCalibrationDataAsync(string module, int count, double[] standValues, double[] values, CancellationToken ct = default)
        => Task.Run(() =>
        {
            Check(Dev.SetCalibrationData(Module(module), count, standValues.ToList(), values.ToList(), "3721"), $"写入{module}校准数据");
            return true;
        }, ct);

    // ===== 版本 =====

    /// <summary>读控制板软件版本。</summary>
    public Task<string> GetControlSoftVersionAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetCONTrolsoftversion();
            Check(r, "读控制板软件版本");
            return r.Result;
        }, ct);

    /// <summary>读控制板硬件版本。</summary>
    public Task<string> GetControlHardVersionAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetCONTrolhardversion();
            Check(r, "读控制板硬件版本");
            return r.Result;
        }, ct);

    /// <summary>读 UI 版本。</summary>
    public Task<string> GetUiVersionAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var r = Dev.GetGUIVersion();
            Check(r, "读 UI 版本");
            return r.Result;
        }, ct);

    // ===== IDutDevice 必需实现 =====

    /// <summary>读整机序列号。</summary>
    public Task<string> ReadSerialNumberAsync(CancellationToken ct = default)
        => Str(() => Dev.GetSerialNumber(), "读取SN", ct);

    /// <summary>读固件版本。</summary>
    public Task<string> ReadFirmwareVersionAsync(CancellationToken ct = default)
        => Str(() => Dev.GetCONTrolsoftversion(), "读取版本", ct);

    /// <summary>写板卡类型/初始信息（整机由 SetDeviceTypeAsync 承担，此处留空）。</summary>
    public Task WriteInitInfoAsync(string boardType, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>按测量点名测量（整机压力由 GetPressureAsync 承担，此处返回 0）。</summary>
    public Task<double> MeasureAsync(string point, CancellationToken ct = default)
        => Task.FromResult(0d);

    /// <summary>
    /// iResponse 包装：失败抛通讯异常。
    /// </summary>
    private Task<string> Str(Func<iResponse<string>> call, string what, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var r = call();
            if (!r.IsCorrect)
            {
                throw new DeviceCommException($"{what}失败：{r.ErrorCode} {r.GetContent(true, true)}", TestResultStatus.HardwareError);
            }
            return r.Result;
        }, ct);
    }

    /// <summary>
    /// 释放通讯连接。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        try { _dev?.Close(); } catch { }
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
