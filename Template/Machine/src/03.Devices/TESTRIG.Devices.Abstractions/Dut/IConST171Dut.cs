using TESTRIG.Core.Abstractions;

namespace TESTRIG.Devices.Abstractions;

/// <summary>
/// ConST171（P27 设备族，整机 ConST171A）被检设备接口。**人工填充**自旧
/// <c>Bots.TestBench.Device.P27CommonBase</c>：全部设备操作走 Xmas11 <c>ConST171Base</c>，
/// 失败抛 <see cref="DeviceCommException"/>（由引擎按异常收尾并落盘）。
/// 模块名统一用字符串：Pressure（正压）/ Vacuum（真空）；开关布尔：true=Open。
/// </summary>
public interface IConST171Dut : IDutDevice
{
    /// <summary>
    /// 补充连接（重连），返回是否已连接。
    /// </summary>
    Task<bool> ReplenishLinkAsync(CancellationToken ct = default);

    /// <summary>
    /// 设置系统语言并回读验证（"zh_CN" / "en_US"）。
    /// </summary>
    Task<bool> SetSystemLanguageAsync(string language, CancellationToken ct = default);

    /// <summary>
    /// 设置开机 LOGO 并回读验证（"ConST" / "Additel" / "Other"）。
    /// </summary>
    Task<bool> SetLogoInfoAsync(string logo, CancellationToken ct = default);

    /// <summary>
    /// 设置设备类型并回读验证。
    /// </summary>
    Task<bool> SetDeviceTypeAsync(string deviceType, CancellationToken ct = default);

    /// <summary>
    /// 写入整机序列号并回读验证。
    /// </summary>
    Task<bool> SetSerialNumberAsync(string serialNumber, CancellationToken ct = default);

    /// <summary>
    /// 启动屏幕自测（"BadPointTest"坏点/颜色、"TouchTest"触摸、"LightTest"亮度、"SpeakerTest"扬声器），
    /// 设备侧切屏自测，随后轮询 <see cref="GetScreenResultAsync"/>。
    /// </summary>
    Task<bool> ChangeScreenAsync(string screen, CancellationToken ct = default);

    /// <summary>
    /// 读屏幕自测结果（"NotRunning"/"Running"/"Fail"/"Pass"）。
    /// </summary>
    Task<string> GetScreenResultAsync(string screen, CancellationToken ct = default);

    /// <summary>
    /// 设置 MCU 串口参数（baudrate/databits/stopBits("One"/"Two")/parity("None"/"Odd"/"Even")）。
    /// </summary>
    Task<bool> SetMCUBaudrateAsync(int baudrate, int databits, string stopBits, string parity, CancellationToken ct = default);

    /// <summary>
    /// 设置屏幕亮度。
    /// </summary>
    Task<bool> SetScreenBrightnessAsync(double value, CancellationToken ct = default);

    /// <summary>
    /// 设置系统声音开关。
    /// </summary>
    Task<bool> SetSystemSoundAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置正压气源静音模式并回读验证。
    /// </summary>
    Task<bool> SetPressureMuteAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置真空气源开机排水模式。
    /// </summary>
    Task<bool> SetPressureVacuumVentAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置常开阀（排气）状态，带重连重试。
    /// </summary>
    Task<bool> SetPressureVentAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置造压范围（模块 "Pressure"/"Vacuum"，min/max 单位 kPa）。
    /// </summary>
    Task<bool> SetPressureRangeAsync(string module, double min, double max, CancellationToken ct = default);

    /// <summary>
    /// 设置泵状态（模块 "Pressure"/"Vacuum"，true=开泵）。
    /// </summary>
    Task<bool> SetPumpStatusAsync(string module, bool open, CancellationToken ct = default);

    /// <summary>
    /// 开始/停止控压（模块 "Pressure"/"Vacuum"，true=开始）。
    /// </summary>
    Task<bool> SetControlStateAsync(string module, bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置吹扫测试模式（true=进入吹扫，配合 <see cref="SetControlStateAsync"/> 打压）。
    /// </summary>
    Task<bool> SetBlowTestAsync(bool open, CancellationToken ct = default);

    /// <summary>
    /// 设置风扇转速（模块 "Pressure"/"Vacuum"，pwm 0~1）。
    /// </summary>
    Task<bool> SetFanSpeedAsync(string module, double pwm, CancellationToken ct = default);

    /// <summary>
    /// 读风扇当前转速（rpm）。
    /// </summary>
    Task<double> GetFanSpeedAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 读当前压力（模块 "Pressure"/"Vacuum"，kPa）。
    /// </summary>
    Task<double> GetPressureAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 读校准压力（模块 "Pressure"/"Vacuum"，kPa）。
    /// </summary>
    Task<double> GetCalPressureAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 读控制板温度（℃）。
    /// </summary>
    Task<double> GetBoardTemperatureAsync(CancellationToken ct = default);

    /// <summary>
    /// 读模块温度（模块 "Pressure"/"Vacuum"，℃）。
    /// </summary>
    Task<double> GetTemperatureAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 进入校准模式（模块 "Pressure"/"Vacuum"）。
    /// </summary>
    Task<bool> StartCalibrationAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 设置校准值（模块 "Pressure"/"Vacuum"，标准表读数）。
    /// </summary>
    Task<bool> SetCalibrationValueAsync(string module, double value, CancellationToken ct = default);

    /// <summary>
    /// 退出校准模式（模块 "Pressure"/"Vacuum"）。
    /// </summary>
    Task<bool> StopCalibrationAsync(string module, CancellationToken ct = default);

    /// <summary>
    /// 写入多点校准数据（模块、点数、标准值序列、实测值序列）。
    /// </summary>
    Task<bool> SetCalibrationDataAsync(string module, int count, double[] standValues, double[] values, CancellationToken ct = default);

    /// <summary>
    /// 读控制板软件版本。
    /// </summary>
    Task<string> GetControlSoftVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 读控制板硬件版本。
    /// </summary>
    Task<string> GetControlHardVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 读 UI 版本。
    /// </summary>
    Task<string> GetUiVersionAsync(CancellationToken ct = default);
}
