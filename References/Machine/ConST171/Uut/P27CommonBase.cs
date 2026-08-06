using Bots.TestBench.Common.Log;
using Bots.TestBench.Device.Base.Comm;
using Bots.TestBench.Device.Upgrade;
using Bots.TestBench.Util;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.ConST171;
using Xmas11.Comm.Devices.ConST171.Data;
using Xmas11.Domain.Electricity;
using Xmas11.Domain.Mechanics;
using Xmas11.Domain.Thermology;
using LogClass = Xmas11.Comm.Devices.ConST171.LogClass;

namespace Bots.TestBench.Device
{
    public class P27CommonBase : UpgradeDevice
    {
        public P27CommonBase()
        {
            DeviceType = Base.DeviceType.DUT;
            this.InitEthernetConfig();
        }

        /// <summary>
        /// 630主机通讯实例
        /// </summary>
        public ConST171Base P27
        {
            get
            {
                if (this.CommInstance == null || !(this.CommInstance is ConST171Base))
                {
                    return null;
                }
                return this.CommInstance as ConST171Base;
            }
        }

        /// <summary>
        /// 打开
        /// </summary>
        /// <returns></returns>
        public bool isNeedleDevice;
        public bool needleDeviceRealConnect;
        public bool ReplenishLink()
        {
            if (isNeedleDevice)
            {
                needleDeviceRealConnect = true;
                return Open();
            }
            return false;
        }

        /// <summary>
        /// 打开
        /// </summary>
        /// <returns></returns>
        public override bool Open()
        {
            if (DeviceName.Contains("针床"))
            {
                isNeedleDevice = true;
            }
            if (isNeedleDevice && !needleDeviceRealConnect)
            {
                ConnectStatus = ConnectStatus.Connected;
                _IsSetConnected = true;
                return true;
            }
            needleDeviceRealConnect = false;

            ConnectStatus = ConnectStatus.Connectting;
            try
            {
                AddressChanged();
                // 完整释放旧连接资源，避免 USB-Serial 驱动状态异常
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    try
                    {
                        if (this.CommInstance.CommInstance != null)
                        {
                            this.CommInstance.CommInstance.Dispose();
                            this.CommInstance.CommInstance = null;
                            this.CommInstance = null;
                        }
                    }
                    catch { }
                    Thread.Sleep(300);
                }
                this.CommInstance = new ConST171Base(this.CommConfig.GetCommSettings());
                if (!this.CommInstance.Connected)
                {
                    this.CommInstance.Open();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Exception(ex);
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            bool isExist = false;
            try
            {
                isExist = this.CommInstance.IsExist();
            }
            catch (Exception)
            {
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            if (isExist)
            {
                ConnectStatus = ConnectStatus.Connected;
                return true;
            }
            else
            {
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
        }

        public bool ComOpen()
        {
            ConnectStatus = ConnectStatus.Connectting;
            bool openResult = false;
            try
            {
                // 完整释放旧连接资源，避免 USB-Serial 驱动状态异常
                if (this.CommInstance != null)
                {
                    try
                    {
                        if (this.CommInstance.CommInstance != null)
                        {
                            this.CommInstance.CommInstance.Dispose();
                            this.CommInstance.CommInstance = null;
                        }
                    }
                    catch { }
                    this.CommInstance.Close();
                    this.CommInstance = null;
                    Thread.Sleep(300);
                }
                AddressChanged();
                var cominfo = CommenHelper.GetComInfo();
                var comm = cominfo.Where(w => w.name.Contains("CH340"));
                if (comm == null)
                {
                    MessageBox.Show($"没有找到CH340", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    ConnectStatus = ConnectStatus.DisConnected;
                    return false;
                }
                //逐个打开
                foreach (var item in comm)
                {
                    (this.CommConfig as SerialPortConfig).SPName = item.value;
                    this.CommInstance = new ConST171Base((this.CommConfig as SerialPortConfig).GetCommSettings());
                    try
                    {
                        openResult = this.CommInstance.Open();
                    }
                    catch
                    {
                        continue;
                    }
                    if (openResult)
                    {
                        ConnectStatus = ConnectStatus.Connected;
                        return true;
                    }
                }

                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            catch (Exception eX)
            {
                MessageBox.Show($"异常{eX.Message}{eX.StackTrace}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
        }

        /// <summary>
        /// 获取被检SN
        /// </summary>
        /// <returns></returns>
        public override string GetDUTSN()
        {
            string result = this.DUT.DeviceCode;
            if (string.IsNullOrEmpty(result))
            {
                GetSerialNumber(out result);
                this.DUT.DeviceCode = result;
            }


            return result;
        }
        #region 功能封装

        /// <summary>
        /// 获取设备编号
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        public override bool GetSerialNumber(out string serialNumber)
        {
            var result = P27.GetSerialNumber();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            serialNumber = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 主动泄压设定点设置
        /// </summary>
        /// <returns></returns>
        public bool SetPressureADJ(OpenCloseState state)
        {
            var result = P27.SetPressureADJ(state);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            var response = GetPressureADJ(out OpenCloseState curState);

            return result.IsCorrect && response && curState == state;
        }

        /// <summary>
        /// 主动泄压设定点设置
        /// </summary>
        /// <returns></returns>
        public bool GetPressureADJ(out OpenCloseState state)
        {
            var result = P27.GetPressureADJ();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            state = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置设备波特率
        /// </summary>
        /// <returns></returns>
        public bool SetMCUBaudrate(int baudrate, int databits, StopBits stopBits, Parity parity)
        {
            iResponse response = P27.SetMCUBaudrate(baudrate, databits, stopBits, parity);
            iResponse<string> responseGet = P27.GetFullMCUBaudrate();
            return response.IsCorrect && $"{baudrate},{databits},{stopBits},{parity}" == responseGet.Result;
        }
        /// <summary>
        /// 设置屏幕亮度
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetScreenBRIG(double value)
        {
            // 手动设置
            var responseSet = P27.SetScreenBRIG(value);
            iResponse<double> responseGet = P27.GetScreenBRIG();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == value)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取设备屏幕亮度
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        //public bool GetScreenBRIG(out double bright)
        //{
        //    var result = P27.GetScreenBRIG();
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //    }
        //    bright = result.Result;
        //    return result.IsCorrect;
        //}
        /// <summary>
        /// 设置蜂鸣器
        /// </summary>
        /// <param name="state">开关</param>
        /// <returns></returns>
        public bool SetBuzzerState(OpenCloseState state)
        {
            var result = P27.SetBuzzer(state);
            OpenCloseState curState = P27.GetBuzzerState().Result;
            return state == curState;
        }

        /// <summary>
        /// FOC状态位是否正常
        /// </summary>
        /// <param name="moduleName">模块名称</param>
        /// <returns></returns>
        public bool IsFocNormal(ModuleName moduleName)
        {
            var result = P27.IsFOCNormal(moduleName);
            return result.Result;
        }
        /// <summary>
        /// 获取触摸及提示音状态
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        //public bool GetSystemSound(out OpenCloseState state)
        //{
        //    var result = P27.GetSystemSound();
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //    }
        //    state = result.Result;
        //    return result.IsCorrect;
        //}
        /// <summary>
        /// 设置触摸及提示音
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetSystemSound(OpenCloseState state)
        {
            // 手动设置
            var responseSet = P27.SetSystemSound(state);
            iResponse<OpenCloseState> responseGet = P27.GetSystemSound();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == state)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取系统语言
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        //public bool GetSystemLanguage(out LanguageSet state)
        //{
        //    var result = P27.GetSystemLanguage();
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //    }
        //    state = result.Result;
        //    return result.IsCorrect;
        //}
        /// <summary>
        /// 设置系统语言
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetSystemLanguage(LanguageSet language)
        {
            // 手动设置
            var responseSet = P27.SetSystemLanguage(language);
            Thread.Sleep(1000);
            iResponse<LanguageSet> responseGet = P27.GetSystemLanguage();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == language)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置开机LOGO
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetLogoInfo(LogoSet logoSet)
        {
            // 手动设置
            var responseSet = P27.SetLogoInfo(logoSet);
            iResponse<LogoSet> responseGet = P27.GetLogoInfo();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == logoSet)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取造压范围
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        public bool GetPressureRange(ModuleName moduleName, out PressureRange pressureRange)
        {
            var result = P27.GetPressureRange(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            pressureRange = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取增压/前级泵电压值
        /// </summary>
        /// <returns></returns>
        /// <returns></returns>
        public bool GetPumpVoltage(out PumpVoltageData voltageData)
        {
            var result = P27.GetPumpVoltage();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            voltageData = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取控制器故障码
        /// </summary>
        /// <returns></returns>
        public bool GetPumpControllerFaultCode(out PumpFaultCodeData faultCodeData)
        {
            var result = P27.GetPumpControllerFaultCode();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            faultCodeData = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取正压气源静音模式
        /// </summary>
        /// <returns></returns>
        //public bool GetPressureMuteState(out OpenCloseState state)
        //{
        //    var result = P27.GetPressureMuteState();
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //    }
        //    state = result.Result;
        //    return result.IsCorrect;
        //}
        /// <summary>
        /// 设置正压气源静音模式
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetPressureMute(OpenCloseState state)
        {
            // 手动设置
            var responseSet = P27.SetPressureMute(state);
            iResponse<OpenCloseState> responseGet = P27.GetPressureMuteState();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == state)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置真空气源开机排水模式
        /// </summary>
        /// <returns></returns>
        //public bool SetPressureVacuumVent(OpenCloseState state)
        //{
        //    var result = P27.SetPressureVacuumVent(state);
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //    }
        //    return result.IsCorrect;
        //}
        public bool SetPressureVacuumVent(OpenCloseState state)
        {
            // 手动设置
            var responseSet = P27.SetPressureVacuumVent(state);
            iResponse<OpenCloseState> responseGet = P27.GetPressureVacuumVent();
            // 验证逻辑
            if (responseSet.IsCorrect && responseGet.IsCorrect &&
               responseGet.Result == state)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取真空气源开机排水模式
        /// </summary>
        /// <returns></returns>
        public bool GetPressureVacuumVent(out OpenCloseState state)
        {
            var result = P27.GetPressureVacuumVent();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            state = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置设备编号
        /// </summary>
        /// <param name="serialNumber">设备编号</param>
        /// <returns></returns>
        public override bool SetSerialNumber(string serialNumber)
        {
            var result = P27.SetSerialNumber(serialNumber);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public override bool GetDeviceType(out string deviceType)
        {
            var result = P27.GetDeviceType();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            deviceType = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置设备编号
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public override bool SetDeviceType(string deviceType)
        {
            var result = P27.SetDeviceType(deviceType);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            var isContainDeviceType = GetDeviceType(out string type);
            return result.IsCorrect && isContainDeviceType && deviceType == type;
        }
        /// <summary>
        /// 设置阀门开关状态
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public bool SetValveStatus(ValveName valveName, OpenCloseState state)
        {
            var result = P27.SetValveStatus(valveName, state);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置正压气源静音模式
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        //public bool SetPressureMute(OpenCloseState state)
        //{
        //    var result = P27.SetPressureMute(state);
        //    if (!result.IsCorrect)
        //    {
        //        LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
        //        return false;
        //    }
        //    return state == P27.GetPressureMuteState().Result;
        //}

        /// <summary>
        /// 获取阀门状态
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public OpenCloseState GetValveStatus(ValveName valveName)
        {
            var result = P27.GetValveStatus(valveName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return OpenCloseState.UnKnown;
            }
            return result.Result;
        }
        /// <summary>
        /// 获取DC24C电压值
        /// </summary>
        /// <returns></returns>
        public bool GetDC24Volt(out Voltage volt)
        {
            volt = Voltage.Empty;
            var result = P27.GetDC24Volt();
            if (result.IsCorrect)
            {
                volt = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取增压电压值
        /// </summary>
        /// <returns></returns>
        public bool GetBoostVolt(out Voltage volt)
        {
            volt = Voltage.Empty;
            var result = P27.GetBoostVolt();
            if (result.IsCorrect)
            {
                volt = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取真空电压值
        /// </summary>
        /// <returns></returns>
        public bool GetVacuumVolt(out Voltage volt)
        {
            volt = Voltage.Empty;
            var result = P27.GetVacuumVolt();
            if (result.IsCorrect)
            {
                volt = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置泵状态
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public bool SetPumpStatus(ModuleName moduleName, OpenCloseState state)
        {
            var result = P27.SetPumpStatus(moduleName, state);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取泵状态
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns></returns>
        public OpenCloseState GetPumpStatus(ModuleName moduleName)
        {
            var result = P27.GetPumpStatus(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return OpenCloseState.UnKnown;
            }
            return result.Result;
        }
        /// <summary>
        /// 获取控制板版本
        /// </summary>
        /// <param name="module">模块</param>
        /// <param name="version">版本</param>
        /// <returns></returns>
        public bool GetControlVersion(out string version, out bool isCommError)
        {
            var result = P27.GetCONTrolsoftversion();
            isCommError = result.ErrorCode != iErrorCode.Fault3001;
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            version = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取控制板硬件版本
        /// </summary>
        /// <param name="module">模块</param>
        /// <param name="version">版本</param>
        /// <returns></returns>
        public bool GetCONTrolhardversion(out string version, out bool isCommError)
        {
            var result = P27.GetCONTrolsoftversion();
            isCommError = result.ErrorCode != iErrorCode.Fault3001;
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            version = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取UI版本
        /// </summary>
        /// <param name="module">模块</param>
        /// <param name="version">版本</param>
        /// <returns></returns>
        public bool GetUIVersion(out string version, out bool isCommError)
        {
            var result = P27.GetGUIVersion();
            isCommError = result.ErrorCode != iErrorCode.Fault3001;
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            version = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 切换屏幕测试
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool ChangeScreenChannel(ScreenItem screens)
        {
            var result = P27.ChangeScreenChannel(screens);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 切换屏幕测试
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool GetScreenResult(ScreenItem screens, out ScreensResult checkResult)
        {
            var result = P27.GetScreenResult(screens);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            checkResult = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置风扇转速比
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool SetFanSpeed(ModuleName fanName, double pwm)
        {
            var result = P27.SetFanSpeed(fanName, pwm);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置风扇转速比
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool GetFanSpeed(ModuleName fanName, out double boostPwm)
        {
            var result = P27.GetFanSpeed(fanName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            boostPwm = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取板载温度
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool GetBoardTemperature(out Temperature temperature)
        {
            var result = P27.GetBoardTemperature();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            temperature = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取正压压力值
        /// </summary>
        /// <returns></returns>
        public Pressure GetPressure(ModuleName moduleName)
        {

            var result = P27.GetPressure(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return Pressure.Empty;
            }
            return result.Result;
        }
        /// <summary>
        /// 获取温度传感器温度
        /// </summary>
        /// <returns></returns>
        public bool GetTemperature(ModuleName moduleName, out Temperature temperature)
        {

            var result = P27.GetTemperature(moduleName);
            if (!result.IsCorrect)
            {
                temperature = Temperature.Empty;
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return false;
            }
            temperature = result.Result;
            return true;
        }
        /// <summary>
        /// 获取前级增压电流值
        /// </summary>
        /// <returns></returns>
        public CurrentData GetCurrentData()
        {
            var result = P27.GetPumpCurrent();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return new CurrentData();
            }
            return result.Result;
        }
        /// <summary>
        /// 获取校准后压力值
        /// </summary>
        /// <returns></returns>
        public Pressure GetCalPressure(ModuleName moduleName)
        {
            var result = P27.GetCalPressure(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return Pressure.Empty;
            }
            return result.Result;
        }

        /// <summary>
        /// 设置压力单位
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool SetPressureUnit(ModuleName moduleName, string unit)
        {
            var result = P27.SetPressureUnit(moduleName, unit);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 开始进入校准模式
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool StartClibration(ModuleName moduleName)
        {
            var result = P27.StartClibration(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 停止进入校准模式
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool StopClibration(ModuleName moduleName)
        {
            var result = P27.StopClibration(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置校准值
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool SetClibrationValue(ModuleName moduleName, double value)
        {
            var result = P27.SetClibrationValue(moduleName, value);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 传感器校准
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool SetCalibrationData(ModuleName moduleName, int count, List<double> standValues, List<double> values, string passwd = "3721")
        {
            LogClass.WriteLog($"当前171A校准：{moduleName}，标准数据为:{string.Join(",", standValues)},被检数据为:{string.Join(",", values)}");
            var result = P27.SetCalibrationData(moduleName, count, standValues, values, passwd);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
                return false;
            }
            //var calibrationData = P27.GetCalibrationData(ModuleName.Vacuum).Result;
            return result.Result;
        }

        /// <summary>
        /// 获取压力单位
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public string GetPressureUnit(ModuleName moduleName)
        {
            var result = P27.GePressureUnit(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.Result;
        }

        /// <summary>
        /// 开始/停止控压
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public bool SetControlState(ModuleName moduleName, OpenCloseState state)
        {
            var result = P27.SetControlState(moduleName, state);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取控压状态
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public OpenCloseState GetControlState(ModuleName moduleName)
        {
            var result = P27.GetControlState(moduleName);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.Result;
        }

        /// <summary>
        /// 设置造压范围
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRange(ModuleName moduleName, double min, double max)
        {
            var result = P27.SetPressureRange(moduleName, min, max);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            var pressureRange = P27.GetPressureRange(moduleName).Result;
            return pressureRange.LowerValue == min && pressureRange.UpperValue == max;
        }


        /// <summary>
        /// 常开阀控制指令
        /// </summary>
        /// <returns></returns>
        public bool SetPressureVent(OpenCloseState state, int retryCount = 0)
        {
            bool result = false;
            try
            {
                var response = P27.SetPressureVent(state);
                if (!response.IsCorrect)
                {
                    if (retryCount < 3)
                    {
                        Close();
                        Thread.Sleep(1000);
                        Open();
                        retryCount++;
                        return SetPressureVent(state, retryCount);
                    }
                    LogHelper.Error($"{response.ErrorCode}", $"{response.GetContent(true, true)}");
                }
                result = P27.GetPressureVentState().Result == state;
                if (!result)
                {
                    if (retryCount < 3)
                    {
                        Close();
                        Thread.Sleep(1000);
                        Open();
                        retryCount++;
                        return SetPressureVent(state, retryCount);
                    }
                }
            }
            catch
            {
                if (retryCount < 3)
                {
                    Close();
                    Thread.Sleep(1000);
                    Open();
                    retryCount++;
                    return SetPressureVent(state, retryCount);
                }
            }

            return result;
        }

        /// <summary>
        /// 设置整机测试模式
        /// </summary>
        /// <returns></returns>
        public bool SetDiagnosticTest(OpenCloseState state, int tryCount = 0)
        {
            bool result = false;
            try
            {

                if (P27 == null)
                {
                    Open();
                }
                result = P27.GetDiagnosticTestState().Result == state;
                if (result)
                    return true;
                var response = P27.SetDiagnosticTest(state);
                if (!response.IsCorrect)
                {
                    LogHelper.Error($"{response.ErrorCode}", $"{response.GetContent(true, true)}");
                    if (tryCount < 3)
                    {
                        Close();
                        Thread.Sleep(1000);
                        Open();
                        tryCount++;

                        return SetDiagnosticTest(state, tryCount);
                    }
                }
                result = P27.GetDiagnosticTestState().Result == state;
                if (!result && tryCount < 3)
                {
                    Close();
                    Thread.Sleep(1000);
                    Open();

                    tryCount++;
                    return SetDiagnosticTest(state, tryCount);
                }
            }
            catch
            {
                Close();
                Thread.Sleep(1000);
                Open();

                if (tryCount < 3)
                {
                    tryCount++;
                    return SetDiagnosticTest(state, tryCount);
                }
            }


            return result;
        }
        /// <summary>
        /// 获取常开阀控制状态
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public OpenCloseState GetPressureVentState()
        {
            var result = P27.GetPressureVentState();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.Result;
        }

        /// <summary>
        /// 启动吹扫功能测试
        /// </summary>
        /// <returns></returns>
        public bool SetBlowTest(OpenCloseState state)
        {
            var result = P27.SetBlowTest(state);
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取吹扫测试状态
        /// </summary>
        /// <param name="screens">屏幕测试类型</param>
        /// <returns></returns>
        public OpenCloseState GetBlowTestState()
        {
            var result = P27.GetBlowTestState();
            if (!result.IsCorrect)
            {
                LogHelper.Error($"{result.ErrorCode}", $"{result.GetContent(true, true)}");
            }
            return result.Result;
        }


        #endregion 功能封装
    }
}
