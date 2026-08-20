using System;
using System.Collections.Generic;
using Xmas11.Comm.Devices;
using Bots.TestBench.Device.Base;
using Xmas11.Domain.Mechanics;
using Xmas11.Comm.Devices.APC2.Data;
using Xmas11.Comm.Data.Common;
using Bots.TestBench.Device.Base.Comm;
using System.Net;
using System.IO;
using Bots.TestBench.Util.IO.Zip;
using System.Linq;
using Xmas11.Domain.Thermology;
using Bots.TestBench.Device.Upgrade;
using Bots.TestBench.Device.Properties;
using Bots.TestBench.Util;
using Bots.TestBench.Model.Task;
using Bots.TestBench.Model.Scripts;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading.Tasks;
using Xmas11.Domain;
using Bots.TestBench.Model.Upgrade.Model;
using Bots.TestBench.Model.Upgrade.Enum;
using Bots.Service.ServiceHelper;
using Bots.TestBench.DataAccess.DataClass;
using System.Text;
using System.Threading;

namespace Bots.TestBench.Device
{
    [Serializable]
    public class ConST811A : UpgradeDevice
    {
        #region Ctors

        /// <summary>
        /// 构造函数 
        /// </summary>
        public ConST811A()
        {
            this.DeviceType = DeviceType.DUT;
        }

        #endregion

        #region Properties

        /// <summary>
        /// 获取811A设备通讯
        /// </summary>
        public APC2Device APC2
        {
            get
            {
                //为空异常怎么处理????
                return this.CommInstance as APC2Device;
            }
        }

        #endregion

        #region BasicMethods


        CommInstanceFactory factory;
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
            AddressChanged();
            try
            {
                if (this.CommInstance != null && this.CommInstance.CommInstance != null)
                {
                    this.CommInstance.Close();
                    if (this.CommInstance.CommInstance != null)
                    {
                        this.CommInstance.CommInstance.Dispose();
                        this.CommInstance.CommInstance = null;
                    }
                    this.CommInstance = null;
                }
                factory = new CommInstanceFactory();
                this.CommInstance = factory.BeginCreate<APC2Device>(this.CommConfig);
                if (this.CommInstance != null)
                {
                    AddressChanged();
                    ConnectStatus = ConnectStatus.Connected;
                    return true;
                }
                else
                {
                    ConnectStatus = ConnectStatus.Error;
                    return false;
                }

            }
            catch (Exception ex)
            {
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        /// <returns></returns>
        public override void Close()
        {
            if (factory != null)
            {
                if (factory.IsCreateing)
                {
                    factory.CancelCreate();
                }
                ConnectStatus = ConnectStatus.DisConnectting;
                factory.CloseInstance(this.CommInstance, this.CommConfig.ToCommConfigKey());
                this.CommConfig.SetSerialNumberEmpty();
                ConnectStatus = ConnectStatus.DisConnected;
            }
        }


        /// <summary>
        /// 获取信息
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return base.GetInfo();
        }
        /// <summary>
        /// 通讯获取DUT信息
        /// </summary>
        /// <returns></returns>
        public override DUT InitDUT()
        {
            if (!this.IsOpen)
            {
                return this.DUT;
            }
            string result = string.Empty;
            if (this.DeviceType == Base.DeviceType.DUT)
            {
                this.DUT.DeviceName = this.DeviceName;
                this.DUT.DeviceMode = this.DeviceMode;
                //设备编号         
                if (GetSerialNumber(out result))
                {
                    this.DUT.DeviceCode = result;
                }

                //模块编号   
                string hmoduleSN = string.Empty;
                if (GetHPMCode(out hmoduleSN))
                {
                    this.DUT.AddInfo("HMSN", hmoduleSN);
                }
                //模块激励值
                double horiv = double.NaN;
                if (GetHPMSensorPowerSupplyValue(out horiv))
                {
                    this.DUT.AddInfo("HMORIV", horiv.ToString());
                }

                //模块编号   
                string lmoduleSN = string.Empty;
                if (GetLPMCode(out lmoduleSN))
                {
                    this.DUT.AddInfo("LMSN", lmoduleSN);
                }
                //模块激励值
                double loriv = double.NaN;
                if (GetLPMSensorPowerSupplyValue(out loriv))
                {
                    this.DUT.AddInfo("LMORIV", loriv.ToString());
                }
            }
            return this.DUT;
        }
        /// <summary>
        /// 检查被检SN
        /// </summary>
        /// <returns></returns>
        public override bool CheckDUTSN()
        {
            string result = this.DUT.DeviceCode;
            if (this.DeviceType == Base.DeviceType.DUT)
            {
                GetSerialNumber(out result);
            }
            return !string.IsNullOrEmpty(this.DUT.DeviceCode) && this.DUT.DeviceCode == result;
        }
        /// <summary>
        /// 获取被检SN
        /// </summary>
        /// <returns></returns>
        public override string GetDUTSN()
        {
            string result = this.DUT.DeviceCode;
            if (this.DeviceType == Base.DeviceType.DUT)
            {
                GetSerialNumber(out result);
            }
            return result;
        }
        /// <summary>
        /// 获取通讯类型
        /// </summary>
        /// <returns></returns>
        public override CommType GetCommType()
        {
            if (CommInstance.CommInstance is Xmas11.Comm.Core.iSerialPortClient)
            {
                return CommType.SeriialPort;
            }
            else if (CommInstance.CommInstance is Xmas11.Comm.Core.iTcpClient || CommInstance.CommInstance is Xmas11.Comm.Core.iUdpClient)
            {
                string IPAddress = string.Empty;
                string realIP = string.Empty;
                realIP = CommInstance.CommInstance.Settings.ToString().Split(':')[0];

                if (GetEthemetIPAddress(out IPAddress) && IPAddress == realIP)
                {
                    return CommType.Ethernet;
                }
                else if (GetWifiIPAddress(out IPAddress) && IPAddress == realIP)
                {
                    return CommType.WLAN;
                }
                else
                {
                    return CommType.None;
                }
            }
            else if (CommInstance.CommInstance is Xmas11.Comm.Core.iUsbClient || CommInstance.CommInstance is Xmas11.Comm.Core.iHidClient)
            {
                return CommType.USB;
            }
            else
            {
                return CommType.None;
            }
        }
        public ScriptHelperKVP GetCommType_KVP(out CommType commType)
        {
            commType = GetCommType(); // 调用原方法
            string typeName = commType.ToString();
            return new ScriptHelperKVP($"811A获取通讯类型:{typeName}", true);
        }
        public ScriptHelperKVP SetSwitchValveState(string openValves)
        {
            var res = APC2.SetSwitchValveState(
                openValves.Contains("1") ? 1 : 0,
                openValves.Contains("2") ? 1 : 0,
                openValves.Contains("3") ? 1 : 0,
                openValves.Contains("4") ? 1 : 0,
                openValves.Contains("5") ? 1 : 0,
                openValves.Contains("6") ? 1 : 0,
                openValves.Contains("7") ? 1 : 0,
                openValves.Contains("8") ? 1 : 0);
            if (openValves.Contains("i"))
            {
                APC2.SetControlValveState(1);
            }
            else if (openValves.Contains("o"))
            {
                APC2.SetControlValveState(-1);
            }
            else if (openValves.Contains("x"))
            {
                APC2.SetControlValveState(0);
            }
            return new ScriptHelperKVP($"811A设置{openValves}开启,其余关闭:{res.IsCorrect}", res.IsCorrect);
        }
        /// <summary>
        /// 获取序列号
        /// </summary>
        /// <returns></returns>
        public bool GetSerialNumber(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            code = result.Result;
            return true;
        }
        public ScriptHelperKVP GetSerialNumber_KVP(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetSerialNumber();
            bool isCorrect = result.IsCorrect;

            if (isCorrect)
            {
                code = result.Result;
            }

            string serialNumberDisplay = isCorrect ? code : "获取失败";
            return new ScriptHelperKVP($"811A获取序列号:{serialNumberDisplay}", isCorrect);
        }
        /// <summary>
        /// 设备序列号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetSerialNumber(string code)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetSerialNumber(code);
            return result.IsCorrect;
        }
        public ScriptHelperKVP SetSerialNumber_KVP(string code)
        {
            if (!IsOpen)
                return new ScriptHelperKVP("811A设备序列号:设备未连接", false);

            iResponse result = APC2.SetSerialNumber(code);
            return new ScriptHelperKVP($"811A设备序列号:{code}", result.IsCorrect);
        }
        /// <summary>
        /// 获取设备主类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetDevType(out string type)
        {
            type = string.Empty;
            if (!IsOpen)
                return false;
            iResponse<string> result = APC2.GetPrimaryDevType();
            if (!result.IsCorrect)
            {
                return false;
            }
            type = result.Result;
            return true;
        }
        /// <summary>
        /// 获取设备全类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetAllDevType(out string type)
        {
            type = string.Empty;
            if (!IsOpen)
                return false;
            iResponse<string> result = APC2.GetDevType();
            if (!result.IsCorrect)
            {
                return false;
            }
            type = result.Result.Replace(",", "").Trim();
            return true;
        }
        /// <summary>
        ///设置设备主类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetPrimaryDevType(string type)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetPrimaryDevType(type);
            return result.IsCorrect;
        }
        public ScriptHelperKVP SetPrimaryDevType_KVP(string type)
        {
            bool success = SetPrimaryDevType(type);
            return new ScriptHelperKVP($"811A设置设备主类型:{type}", success);
        }
        /// <summary>
        /// 设置设备子类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetSecondaryDevType(string type)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetSecondaryDevType(type);
            return result.IsCorrect;
        }
        #endregion

        #region Methods
        /// <summary>
        /// 获取设备图片
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Bitmap GetDeviceMainImage()
        {
            return Resources.main;
        }
        #region 量程限制
        /// <summary>
        /// 开启量程限制(恢复默认量程限制)
        /// </summary>
        /// <returns></returns>
        public bool OpenPressureModelLimit()
        {
            iResponse result = APC2.DeletePressureModelLimitSetting();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 关闭量程限制
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool ClosePressureModelLimit(DeviceModelEnum model)
        {
            iResponse result = APC2.SetPressureModelLimitSetting(model, false);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 获取当前机型量程限制信息
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        public bool GetPressureModelLimitInfo(DeviceModelEnum modelEnum, out ModelLimitSetting setting)
        {
            setting = new ModelLimitSetting();
            iResponse<ModelLimitSetting> result = APC2.GetPressureModelLimitSetting(modelEnum);

            if (!result.IsCorrect)
            {
                return false;
            }
            setting = result.Result;
            return true;
        }

        /// <summary>
        /// 获取当前机型量程限制信息
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        public bool GetPressureModelLimitInfo(out ModelLimitSetting setting)
        {
            setting = new ModelLimitSetting();
            iResponse<ModelLimitSetting> result = APC2.GetPressureModelLimitSetting();

            if (!result.IsCorrect)
            {
                return false;
            }
            setting = result.Result;
            return true;
        }
        /// <summary>
        /// 设置量程限制信息
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        public bool SetPressureModelLimitInfo(ModelLimitSetting setting)
        {
            iResponse result = APC2.SetPressureModelLimitSetting(setting);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 判断当前机型量程限制是否正确
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        public bool CheckPressureLimitIsCorrect(ModelLimitSetting setting, out ModelLimitSetting limitSetting)
        {
            limitSetting = new ModelLimitSetting()
            {
                DeviceModel = setting.DeviceModel,
                Enable = setting.Enable,
                LowerValue = setting.LowerValue,
                UpperValue = setting.UpperValue
            };
            Dictionary<DeviceModelEnum, PressureRange> dictionaty = new Dictionary<DeviceModelEnum, PressureRange>()
            {
                { DeviceModelEnum.ConST811A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_2_5M,new PressureRange(50,3000,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_4M,new PressureRange(50,4500,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_6M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_7M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_500,new PressureRange(60,3800,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_1K,new PressureRange(150,7100,PressureUnit.kPa)},
            };
            if (dictionaty.ContainsKey(limitSetting.DeviceModel))
            {
                limitSetting.LowerValue = dictionaty[limitSetting.DeviceModel].LowerValue;
                limitSetting.UpperValue = dictionaty[limitSetting.DeviceModel].UpperValue;
                if (dictionaty[setting.DeviceModel] == new PressureRange(setting.LowerValue, setting.UpperValue, PressureUnit.kPa))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 设置量程限制配置文件
        /// </summary>
        /// <returns></returns>
        public bool SetAllPressureModelLimitInfo(ModelLimitSetting setting)
        {

            Dictionary<DeviceModelEnum, PressureRange> dictionaty = new Dictionary<DeviceModelEnum, PressureRange>()
            {
                { DeviceModelEnum.ConST811A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_2_5M,new PressureRange(50,3000,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_4M,new PressureRange(50,4500,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_6M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_7M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_500,new PressureRange(60,3800,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_1K,new PressureRange(150,7100,PressureUnit.kPa)},
            };
            if (dictionaty.ContainsKey(setting.DeviceModel))
            {
                dictionaty[setting.DeviceModel] = new PressureRange(setting.LowerValue, setting.UpperValue, PressureUnit.kPa);
            }
            bool result = true;
            foreach (var d in dictionaty)
            {
                result &= APC2.SetPressureModelLimitSetting(d.Key, true, d.Value.LowerValue, d.Value.UpperValue).IsCorrect;
            }
            return result;
        }
        /// <summary>
        /// 设置量程限制配置文件
        /// </summary>
        /// <returns></returns>
        public bool SetAllPressureModelLimitInfo()
        {

            Dictionary<DeviceModelEnum, PressureRange> dictionaty = new Dictionary<DeviceModelEnum, PressureRange>()
            {
                { DeviceModelEnum.ConST811A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_2_5M,new PressureRange(50,3000,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_4M,new PressureRange(50,4500,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_6M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ConST811A_7M,new PressureRange(200,7100,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_D,new PressureRange(2,300,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_LLP,new PressureRange(0.05,12,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_500,new PressureRange(60,3800,PressureUnit.kPa)},
                { DeviceModelEnum.ADT761A_1K,new PressureRange(150,7100,PressureUnit.kPa)},
            };
            bool result = true;
            foreach (var d in dictionaty)
            {
                result &= APC2.SetPressureModelLimitSetting(d.Key, true, d.Value.LowerValue, d.Value.UpperValue).IsCorrect;
            }
            return result;
        }
        #endregion

        #region 系统
        /// <summary>
        /// 设置设备重启
        /// </summary>
        /// <returns></returns>
        public bool SetReboot()
        {
            iResponse result = APC2.SetReboot();
            return result.IsCorrect;
        }
        /// <summary>
        /// 同步系统日期和时间
        /// </summary>
        /// <returns></returns>
        public bool SetSystemAllTime()
        {
            DateTime dateTime = DateTime.Now;
            iResponse result1 = APC2.SetSystemDate(dateTime);
            iResponse result2 = APC2.SetSystemTime(dateTime);
            if (result1.IsCorrect && result2.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 设置系统时间
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool SetSystemTime(DateTime dateTime)
        {
            iResponse result = APC2.SetSystemTime(dateTime);
            return result.IsCorrect;
        }
        /// <summary>
        /// 开启24小时制，时区UTC+0:00
        /// </summary>
        /// <returns></returns>
        public bool SetSystemTimeFormat()
        {
            iResponse result = APC2.SetSystemTimeFormat(true, 0);
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置系统日期
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool SetSystemDate(DateTime dateTime)
        {
            iResponse result = APC2.SetSystemDate(dateTime);
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置日期格式为yyyy MM dd
        /// </summary>
        /// <returns></returns>
        public bool SetFactoryDateFormat()
        {
            return APC2.SetFactoryDateFormat(0).IsCorrect;
        }
        /// <summary>
        /// 设置日期格式分隔符为-
        /// </summary>
        /// <returns></returns>
        public bool SetDateSeparator()
        {
            return APC2.SetDateSeparator(0).IsCorrect;
        }
        /// <summary>
        /// 获取系统时间
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool GetSystemTime(out DateTime time)
        {
            iResponse<DateTime> result = APC2.GetSystemTime();
            if (!result.IsCorrect)
            {
                time = DateTime.MinValue;
                return false;
            }
            time = result.Result;
            return true;
        }
        public ScriptHelperKVP GetSystemTime_KVP(out DateTime time)
        {
            iResponse<DateTime> result = APC2.GetSystemTime();

            bool isCorrect = result.IsCorrect;
            if (isCorrect)
            {
                time = result.Result;
            }
            else
            {
                time = DateTime.MinValue;
            }

            string timeDisplay = isCorrect ? time.ToString("yyyy-MM-dd HH:mm:ss") : "获取失败";
            return new ScriptHelperKVP($"811A获取系统时间:{timeDisplay}", isCorrect);
        }
        /// <summary>
        /// 设置进气传感器校准日期
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool SetCalibrationSensorDate(DateTime dateTime)
        {
            iResponse result = APC2.SetCalibrationSensorDate(dateTime);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取进气传感器校准日期
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool GetCalibrationSensorDate(out DateTime time)
        {
            iResponse<DateTime> result = APC2.GetCalibrationSensorDate();
            if (!result.IsCorrect)
            {
                time = DateTime.MinValue;
                return false;
            }
            time = result.Result;
            return true;
        }
        /// <summary>
        /// 设置自整定日期
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool SetCalibrationAutoDate(DateTime dateTime)
        {
            iResponse result = APC2.SetCalibrationAutoDate(dateTime);
            return result.IsCorrect;
        }
        public ScriptHelperKVP SetCalibrationAutoDate_KVP(DateTime dateTime)
        {
            iResponse result = APC2.SetCalibrationAutoDate(dateTime);

            string formattedDate = dateTime.ToString("yyyy-MM-dd");
            return new ScriptHelperKVP($"811A设置自整定日期:{formattedDate}", result.IsCorrect);
        }
        /// <summary>
        /// 获取自整定日期
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool GetCalibrationAutoDate(out DateTime time)
        {
            iResponse<DateTime> result = APC2.GetCalibrationAutoDate();
            if (!result.IsCorrect)
            {
                time = DateTime.MinValue;
                return false;
            }
            time = result.Result;
            return true;
        }
        /// <summary>
        /// RTC检测
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool RTCCheck(DateTime time)
        {
            return APC2.RTCCheck(time);
        }

        /// <summary>
        /// 获取系统时间
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool GetDevSysDate(out DateTime time)
        {
            iResponse<DateTime> result = APC2.GetSystemDateTime();
            if (!result.IsCorrect)
            {
                time = DateTime.MinValue;
                return false;
            }
            time = result.Result;
            return true;
        }

        /// <summary>
        /// 设置设备出厂日期
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public bool SetDeviceFactoryDate(DateTime date)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetDeviceFactoryDate(date);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取设备出厂日期
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public bool GetDeviceFactoryDate(out DateTime date)
        {
            date = DateTime.MinValue;
            bool result = false;
            if (IsOpen)
            {
                iResponse<DateTime> returnValue = APC2.GetDeviceFactoryDate();
                if (returnValue.IsCorrect)
                {
                    result = true;
                    date = returnValue.Result;
                }
            }
            return result;
        }
        /// <summary>
        /// 设置启动LOGO
        /// </summary>
        /// <returns></returns>
        public bool SetStartLogo()
        {
            return APC2.SetChangeStartLogo().IsCorrect;
        }
        #endregion

        #region 开始前准备


        public override bool GetRS1(string DevCode, out string Msg)
        {
            Msg = "";

            if (GetVersion_Controller(out string controllerVersion) && controllerVersion.Contains("APC-BP"))
            {
                return true;
            }

            var Devtype = Helper.GetSNType(DevCode)?.Trim();
            try
            {

                #region 量程确认

                var getresult = IsHaveDoubleRange(out Msg);
                if (getresult == null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (IsHaveDoubleRange(out Msg) == null)
                        {
                            Task.Delay(200).Wait();
                            continue;
                        }
                        else
                            break;
                    }
                    if (getresult == null)
                    {
                        Msg = $"模块不在线！{Msg}";
                    }
                }
                else if ((bool)getresult)
                {
                    Msg = "";
                    PressureRange highRange = new PressureRange();
                    GetHighPressureModelRange(out highRange);

                    PressureRange lowRange = new PressureRange();
                    GetLowPressureModelRange(out lowRange);

                    lowRange = PressureRange.ConvertTo(lowRange, Unit.Parse("kPa"));
                    highRange = PressureRange.ConvertTo(highRange, Unit.Parse("kPa"));

                    //outmsg = string.Format("高压:{0},低压:{1}", highRange.ToString(), lowRange.ToString());

                    //差压，-100~700，-2.5~2.5
                    if (Devtype == "ConST811AD")
                    {
                        bool isok = true;
                        if (!(highRange.LowerValue == -100 && highRange.UpperValue == 250))
                            isok = false;
                        if (!(lowRange.LowerValue == -10 && lowRange.UpperValue == 10))
                            isok = false;
                        if (!isok)
                        {
                            Msg = $"当前设备为差压类型，标准模块量程为：\r\n" +
                                $"-10~10kPa --  -100~250kPa，\r\n" +
                                $"实际模块量程为：\r\n" +
                                $"{lowRange} -- {highRange}\r\n";
                        }
                    }
                    //表压，-100~7000，-100~250
                    else if (Devtype == "ConST811AG")
                    {
                        bool isok = true;
                        if (!(highRange.LowerValue == -100 && highRange.UpperValue == 7000))
                            isok = false;
                        if (!(lowRange.LowerValue == -100 && lowRange.UpperValue == 250))
                            isok = false;
                        if (!isok)
                        {
                            Msg = $"当前设备为表绝压，标准模块量程为：\r\n" +
                                $"-100~250kPa --  -100~7000kPa，\r\n" +
                                $"实际模块量程为：\r\n" +
                                $"{lowRange} -- {highRange}\r\n";
                        }
                    }
                    //表压，-100~10000，-100~250
                    else if (Devtype == "ConST811AG-10M")
                    {
                        bool isok = true;
                        if (!(highRange.LowerValue == -100 && highRange.UpperValue == 10000))
                            isok = false;
                        if (!(lowRange.LowerValue == -100 && lowRange.UpperValue == 250))
                            isok = false;
                        if (!isok)
                        {
                            Msg = $"当前设备为表绝压10MPa，标准模块量程为：\r\n" +
                                $"-100~250kPa --  -100~10000kPa，\r\n" +
                                $"实际模块量程为：\r\n" +
                                $"{lowRange} -- {highRange}\r\n";
                        }
                    }
                    //微差压，-10~10，-0.5~0.5
                    else if (Devtype == "ConST811AL")
                    {
                        bool isok = true;
                        if (!(highRange.LowerValue == -10 && highRange.UpperValue == 10))
                            isok = false;
                        if (!(lowRange.LowerValue == -10 && lowRange.UpperValue == 10))
                            isok = false;
                        if (!isok)
                        {
                            Msg = $"当前设备为微差压，标准模块量程为：\r\n" +
                                $"-10~10kPa --  -10~10kPa，\r\n" +
                                $"实际模块量程为：\r\n" +
                                $"{lowRange} -- {highRange}\r\n";
                        }
                    }
                }
                else
                {
                    Msg = "设备为单模块，正常测试应该安装2个工装模块，请检测是否漏装或者没拧紧。";
                    return false;
                }
                #endregion

            }
            catch (Exception ex)
            {
                Msg = ex.Message + ex.StackTrace;
                return false;
            }
            return true;
        }


        public override bool GetRS2(string DevtCode, out string result)
        {
            result = "";
            //是否检验通过
            bool isCheckPass = false;
            //主版本是否高于最新版本
            bool isGreaterHost = false;
            //电测板版本是否高于最新版本
            bool isGreaterELE = false;
            //控制板版本是否高于最新版本
            bool isGreaterCOL = false;

            bool isGreaterDD = false;

            #region 获取系统版本
            string version;
            if (!GetVersion(out version))
            {
                result = "读取系统版本失败";
                return false;
            }

            VersionValidResponse response = DBService.VersionValid(version);
            string versionSTD = response.LatestVersion;
            string versionUUD = version;
            switch (response.Result)
            {
                case VersionValidResult.NonStandard:
                    result = string.Format("验证主程序版本{0}格式不规范", versionUUD);
                    break;
                case VersionValidResult.UnKnown:
                    result = string.Format("验证主程序版本{0}未匹配到服务器版本", versionUUD);
                    break;
                case VersionValidResult.Less:
                    result = string.Format("验证主程序版本{0}不是最新版本{1}", versionUUD, versionSTD);
                    break;
                case VersionValidResult.Equal:
                    isCheckPass = true;
                    break;
                case VersionValidResult.Greater:
                    isGreaterHost = true;
                    break;
            }

            #endregion

            #region 获取电测版本
            string coreVersion;

            if (!GetVersion_Electricity(out coreVersion))
            {
                result += "获取电测版本失败\r\n";
                return false;
            }

            response = Bots.Service.ServiceHelper.DBService.VersionValid(coreVersion);
            string versionSTDELE = response.LatestVersion;
            string versionUUDELE = coreVersion;
            switch (response.Result)
            {
                case VersionValidResult.NonStandard:
                    result += string.Format("验证电测版本{0}格式不规范\r\n", versionUUDELE);
                    break;
                case VersionValidResult.UnKnown:
                    result += string.Format("验证电测版本{0}未匹配到服务器版本\r\n", versionUUDELE);
                    break;
                case VersionValidResult.Less:
                    result += string.Format("验证电测版本{0}不是最新版本{1}\r\n", versionUUDELE, versionSTDELE);
                    break;
                case VersionValidResult.Equal:
                    isCheckPass = true;
                    break;
                case VersionValidResult.Greater:
                    isGreaterELE = true;
                    break;
            }

            #endregion

            #region 获取控制板固件版本
            string type;
            if (!GetDevType(out type))
            {
                result += "获取整机设备类型失败\r\n";
                return false;
            }

            //获取控制板固件版本
            string controllerVersion;
            if (!GetVersion_Controller(out controllerVersion))
            {
                result += "获取控制板版本失败\r\n";
                return false;
            }

            response = Bots.Service.ServiceHelper.DBService.VersionValid(controllerVersion);
            string versionSTDCOL = response.LatestVersion;
            string versionUUDCOL = controllerVersion;
            switch (response.Result)
            {
                case VersionValidResult.NonStandard:
                    result += string.Format("验证控制版本{0}格式不规范", versionUUDCOL);
                    break;
                case VersionValidResult.UnKnown:
                    result += string.Format("验证控制版本{0}未匹配到服务器版本", versionUUDCOL);
                    break;
                case VersionValidResult.Less:
                    result += string.Format("验证控制版本{0}不是最新版本{1}", versionUUDCOL, versionSTDCOL);
                    break;
                case VersionValidResult.Equal:
                    isCheckPass = true;
                    break;
                case VersionValidResult.Greater:
                    isGreaterCOL = true;
                    break;
            }

            #endregion

            #region 获取DD库版本
            //获取系统版本，气压1.0.0.57才支持DD库的信息
            Version Taget = new Version("1.0.0.57");

            var verlist = version.Split(' ');
            Version nowver = new Version(verlist[1]);
            string ddversion = "";
            string ddversionSTD = "";

            if (nowver >= Taget)
            {
                if (!GetDeviceDDT(out ddversion))
                {
                    result += "读取DD库版本指令执行失败\r\n";
                }
                if (string.IsNullOrWhiteSpace(ddversion))
                {
                    result += "设备没有安装DD库，联系研发人员安装。\r\n";
                }

                response = Bots.Service.ServiceHelper.DBService.VersionValidByDeviceType(ddversion, "ConST811A");
                ddversionSTD = response.LatestVersion;
                switch (response.Result)
                {
                    case VersionValidResult.NonStandard:
                        result += string.Format("DD库版本{0}格式不规范，没有正确解析", ddversion);
                        break;
                    case VersionValidResult.UnKnown:
                        result += string.Format("DD库版本{0}未匹配到服务器版本，请确认服务器是否管控", ddversion);
                        break;
                    case VersionValidResult.Less:
                        result += $"DD库版本{ddversion}不是最新版本{response.LatestVersion}，升级到最新版本再进行测试。";
                        break;
                    case VersionValidResult.Equal:
                        isCheckPass = true;
                        break;
                    case VersionValidResult.Greater:
                        isGreaterDD = true;
                        break;
                }
            }
            else
            {

            }

            #endregion

            type = type = Helper.GetSNType(DevtCode)?.Trim();
            if (type.Contains("BP"))
            {
                return true;
            }

            var tempType = "";
            if (type.Contains("10M"))
            {
                tempType = "ConST811A-H";
            }
            else if (type.Contains("M") || type.Contains("U") || type.Contains("AG"))
            {
                tempType = "ConST811A-M";
            }
            else if (type.Contains("D"))
            {
                tempType = "ConST811A-D";
            }
            else if (type.Contains("L"))
            {
                tempType = "ConST811A-LP";
            }
            else
            {
                tempType = "ConST811A-M";
            }

            if (string.IsNullOrWhiteSpace(tempType))
            {
                result += ($"当前控制板版本 {controllerVersion} 与当前机型 {type} 不匹配。没有在MES系统中查到对应的型号信息，无法进行时比较。\r\n请确认设备压力类型与控制板是否匹配，\r\n");
                return false;
            }

            if (controllerVersion.Contains("APC-BP"))
            {
                return true;
            }

            if (!controllerVersion.Contains(tempType.Split('-')[1]))
            {
                result += $"当前控制板版本{controllerVersion}与当前机型{type}不匹配，请使用U盘升级最新固件！确认设备压力类型与控制板是否匹配，\r\n";
                return false;
            }
            return true;
        }
        #endregion

        #region 软件版本
        /// <summary>
        /// 获取软件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public override bool GetVersion(out string version)
        {
            iResponse<string> response = APC2.GetVersion_Application();
            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                version = string.Empty;
                return false;
            }
        }
        /// <summary>
        /// 获取核心版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion_OS(out string version)
        {
            iResponse<string> response = APC2.GetVersion_OS();
            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                version = string.Empty;
                return false;
            }
        }

        public ScriptHelperKVP GetFirmVersion_PA(out string version)
        {
            var result = APC2.GetVersion_PA();
            version = result.IsCorrect ? result.Result : string.Empty;
            return new ScriptHelperKVP($"811A获取PA固件版本:{version}", result.IsCorrect);
        }

        public ScriptHelperKVP GetHardVersion_PA(out string version)
        {
            var result = APC2.GetHardVersion_PA();
            version = result.IsCorrect ? result.Result : string.Empty;
            return new ScriptHelperKVP($"811A获取PA硬件版本:{version}", result.IsCorrect);
        }

        /// <summary>
        /// 获取控制板固件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        /// <returns></returns>
        public bool GetVersion_Controller(out string version)
        {
            iResponse<string> response = APC2.GetVersion_Controller();
            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                version = string.Empty;
                return false;
            }
        }
        public ScriptHelperKVP GetVersion_Controller_KVP(out string version, out bool result)
        {
            result = GetVersion_Controller(out version);
            string versionDisplay = result ? version : "获取失败";
            return new ScriptHelperKVP($"811A获取控制板固件版本:{versionDisplay}", result);
        }
        /// <summary>
        /// 获取控制板硬件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetHardVersion_Controller(out string version)
        {
            iResponse<string> response = APC2.GetHardVersion_Controller();
            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                version = string.Empty;
                return false;
            }
        }
        public ScriptHelperKVP GetHardVersion_Controller_KVP(out string version, out bool result)
        {
            result = GetHardVersion_Controller(out version);
            string versionDisplay = result ? version : "获取失败";
            return new ScriptHelperKVP($"811A获取控制板硬件版本:{versionDisplay}", result);
        }
        /// <summary>
        /// 获取电测板固件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion_Electricity(out string version)
        {
            iResponse<string> response = APC2.GetVersion_Electricity();
            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                version = string.Empty;
                return false;
            }
        }
        public ScriptHelperKVP GetVersion_Electricity_KVP(out string version)
        {
            bool success = GetVersion_Electricity(out version);
            string versionDisplay = success ? version : "获取失败";
            return new ScriptHelperKVP($"811A获取电测板固件版本:{versionDisplay}", success);
        }
        #endregion

        #region 液压版本使用指令
        /// <summary>
        /// 设置电磁阀24V状态
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetVent24V(bool value)
        {
            iResponse result = APC2.SetVent24V(value);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取模块量程数量信息
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetManyRangeCount(out int PRinfo)
        {
            PRinfo = 0;
            iResponse<int> result = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase).GetManyRangeCount();
            if (!result.IsCorrect)
            {
                return false;
            }
            PRinfo = result.Result;
            return true;
        }

        /// <summary>
        /// 获取模块量程具体信息
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetManyRangeInfoByID(int num, out PressureRangeDetailedInfo PRinfo)
        {
            PRinfo = new PressureRangeDetailedInfo();
            iResponse<PressureRangeDetailedInfo> result = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase).GetManyRangeInfoByID(num);
            if (!result.IsCorrect)
            {
                return false;
            }
            PRinfo = result.Result;
            return true;
        }

        /// <summary>
        /// 获取高压模块量程数量信息
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetHPMManyRangeCount(out int PRinfo)
        {
            PRinfo = 0;
            iResponse<int> result = (APC2.FW_HPM as Xmas11.Comm.Devices.DPSEXBase).GetManyRangeCount();
            if (!result.IsCorrect)
            {
                return false;
            }
            PRinfo = result.Result;
            return true;
        }

        /// <summary>
        /// 获取模块量程具体信息
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetHPMManyRangeInfoByID(int num, out PressureRangeDetailedInfo PRinfo)
        {
            PRinfo = new PressureRangeDetailedInfo();
            iResponse<PressureRangeDetailedInfo> result = (APC2.FW_HPM as Xmas11.Comm.Devices.DPSEXBase).GetManyRangeInfoByID(num);
            if (!result.IsCorrect)
            {
                return false;
            }
            PRinfo = result.Result;
            return true;
        }

        /// <summary>
        /// 获取介质类型，水1还是油0
        /// </summary>
        /// <returns></returns>
        public bool GetDeviceJZ(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetDeviceMedium();
            if (result.IsCorrect)
            {
                if (result.Result == "0")
                {
                    code = "油";
                }
                else if (result.Result == "1")
                {
                    code = "水";
                }
                else
                {
                    code = "类型获取异常";
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置介质类型，水还是油
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetDeviceJZ(string code)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetDeviceMedium(code);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取风扇转速
        /// </summary>
        /// <returns></returns>
        public bool GetDeviceFans(out double code)
        {
            code = -1;
            iResponse<double> result = APC2.GetDeviceFans();
            if (result.IsCorrect)
            {
                if (result.Result != -1)
                {
                    code = result.Result;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置风扇占空比
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetDeviceFans(double code)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetDeviceFans(code);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取铁电数据
        /// </summary>
        /// <returns></returns>
        public bool GetDeviceROMData(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetDeviceROMData();
            if (result.IsCorrect)
            {
                if (result.Result != "X")
                {
                    code = result.Result;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置设备铁电数据
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetDeviceROM(string code)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetDeviceROMData(code);
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置设备自检类型与动作信息
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetSelfCheck(CheckType Tpye, CheckDo doing)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetSelfCheck(Tpye, doing);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取设备自检进度
        /// </summary>
        /// <returns></returns>
        public bool GetSelfCheck(CheckType Tpye, out string value)
        {
            value = string.Empty;
            iResponse<string> result = APC2.GetSelfCheck(Tpye);
            if (result.IsCorrect)
            {
                if (result.Result != "X")
                {
                    value = result.Result;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取设备自检异常码
        /// </summary>
        /// <returns></returns>
        public bool GetSelfCheckError(CheckType Tpye, out string value)
        {
            value = string.Empty;
            iResponse<string> result = APC2.GetSelfCheckError(Tpye);
            if (result.IsCorrect)
            {
                if (result.Result != "X")
                {
                    value += "\r\n" + result.Result;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取容栅尺位置
        /// </summary>
        /// <returns></returns>
        public bool GetRulerValue(out double value)
        {
            value = -1;
            iResponse<double> result = APC2.GetRulerValue();
            if (result.IsCorrect)
            {
                if (result.Result >= 0)
                {
                    value = result.Result;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取液源模块校准进度
        /// </summary>
        /// <returns></returns>
        public bool GetYYSensorState(out YYSensorCalibrationData value)
        {
            value = null;
            iResponse<YYSensorCalibrationData> result = APC2.GetYYSensorState();
            if (result.IsCorrect)
            {
                value = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取液源整机校准进度
        /// </summary>
        /// <returns></returns>
        public bool GetYYSelfCheck(out string Pvalue, out string Progress)
        {
            Pvalue = "";
            Progress = "";
            iResponse<string> result = APC2.GetYYSelfTuningState();
            if (result.IsCorrect)
            {
                var sptemp = result.Result.Split(',');
                Pvalue = sptemp[0];
                Progress = sptemp[1];
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置设备铁电数据
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetBumpRun(double value)
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetBumpRun(value);
            return result.IsCorrect;
        }


        /// <summary>
        /// 设置设备维修排液
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetRepairVent()
        {
            if (!IsOpen)
                return false;
            iResponse result = APC2.SetRepairVent();
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取DD库版本
        /// </summary>
        /// <returns></returns>
        public bool GetDeviceDDT(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetDeviceDDT();
            if (result.IsCorrect)
            {
                code = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 打开Vout阀
        /// </summary>
        /// <returns></returns>
        public bool OpenVout<T>(AutoTestItem item, Result<List<T>> result, bool isopen = true)
        {
            RealTimeMsg msg2 = new RealTimeMsg("打开Vout阀");
            item.AddRealTimeMsgs(msg2);
            iResponse Xresult = APC2.SetRepairVent();
            if (Xresult.IsCorrect)
            {
                msg2.Content = " √ ";
                return true;
            }
            else
            {
                msg2.Content = $" X \r\n{Xresult.GetContent(true, true)}";
                result.AddTestErrMsgs(new ErrMsg(201003, "打开Vout阀失败，通讯可能异常，会导致后续测试无法正常进行。"));
                return false;
            }
        }
        /// <summary>
        /// 获取泵与旋转电机转速
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetSpeedInfo(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetSpeedInfo();
            if (result.IsCorrect)
            {
                code = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取PMH、PML、Pin、Pctl压力值
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetPressureInfo(out string code)
        {
            code = string.Empty;
            iResponse<string> result = APC2.GetPressureInfo();
            if (result.IsCorrect)
            {
                code = result.Result;
                return true;
            }
            return false;
        }


        /// <summary>
        /// 获取电池电压（v）,充电电流（mA）/放电电流（mA）
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetBATTery2(out double[] Value)
        {
            Value = new double[3] { 0, 0, 0 };
            iResponse<double[]> result = APC2.GetBATTery();
            if (result.IsCorrect)
            {
                Value = result.Result;
                return true;
            }
            return false;
        }


        /// <summary>
        /// 指定模块的版本号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool VerOfPressureModel(int address, out string version)
        {
            version = string.Empty;
            iResponse<string> result = APC2.VerOfPressureModel(address);
            if (result.IsCorrect)
            {
                version = result.Result;
                return true;
            }
            return false;
        }

        #region 液压增压组件用

        /// <summary>
        /// 设置压力测试状态
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetMeasure<T>(AutoTestItem item, Result<List<T>> result)
        {
            RealTimeMsg msg2 = new RealTimeMsg("切为测量状态");
            item.AddRealTimeMsgs(msg2);
            iResponse Xresult = APC2.SetPressureControlMode(DevicePressureControlMode.MEASURE);
            if (Xresult.IsCorrect)
            {
                msg2.Content = " √ ";
                return true;
            }
            else
            {
                msg2.Content = $" X \r\n{Xresult.GetContent(true, true)}";
                var ems = new ErrMsg(201003, $"切为测量状态失败，无法进行下一步测试。");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }

        /// <summary>
        /// 设置设备维修排液,增压组件用
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetRepairVent<T>(AutoTestItem item, Result<List<T>> result)
        {
            RealTimeMsg msg2 = new RealTimeMsg("启动维修排液");
            item.AddRealTimeMsgs(msg2);
            iResponse Xresult = APC2.SetRepairVent();
            if (Xresult.IsCorrect)
            {
                msg2.Content = " √ ";
                return true;
            }
            else
            {
                msg2.Content = $" X \r\n{Xresult.GetContent(true, true)}";
                var ems = new ErrMsg(201003, $"启动失败，会造成组件内有残留的液体，无法直接拆卸。");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }
        /// <summary>
        /// 设置设备排空
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetVentMode<T>(AutoTestItem item, Result<List<T>> result)
        {
            RealTimeMsg msg2 = new RealTimeMsg("启动排空");
            item.AddRealTimeMsgs(msg2);
            iResponse Xresult = APC2.SetPressureControlMode(DevicePressureControlMode.VENT);
            if (Xresult.IsCorrect)
            {
                msg2.Content = " √ ";
                return true;
            }
            else
            {
                msg2.Content = $" X \r\n{Xresult.GetContent(true, true)}";
                var ems = new ErrMsg(201003, $"启动失败，会造成组件内有残留的液体，无法直接拆卸。");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }
        /// <summary>
        /// 获取当前压力
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetPressure<T>(AutoTestItem item, Result<List<T>> result, out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<Pressure> Xresult = APC2.GetPressure_IPM();
            if (Xresult.IsCorrect)
            {
                pressure = Xresult.Result;
                return true;
            }
            else
            {
                Xresult = APC2.GetPressure_IPM();
                if (Xresult.IsCorrect)
                {
                    pressure = Xresult.Result;
                    return true;
                }
                else
                {
                    Xresult = APC2.GetPressure_IPM();
                    if (Xresult.IsCorrect)
                    {
                        pressure = Xresult.Result;
                        return true;
                    }
                    else
                    {
                        var ems = new ErrMsg(201003, $"获取压力失败，无法进行下一步测试\r\n{Xresult.GetContent(true, true)}");
                        result.SetConclusion($"通讯不稳定，请重测。", ems);
                        return false;
                    }
                }
            }
        }
        /// <summary>
        /// 获取控压是否稳定
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetPressureStableState<T>(AutoTestItem item, Result<List<T>> result, out PressureStableState pressure)
        {
            pressure = PressureStableState.UnKnown;
            iResponse<PressureStableState> Xresult = APC2.GetPressureModelStableState(1);
            if (Xresult.IsCorrect)
            {
                pressure = Xresult.Result;
                return true;
            }
            else
            {
                var ems = new ErrMsg(201003, $"获取控压是否稳定失败，无法进行下一步测试\r\n{Xresult.GetContent(true, true)}");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }
        /// <summary>
        /// 获取容栅尺位置
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetBumpMove<T>(AutoTestItem item, Result<List<T>> result, out double Rulers)
        {
            Rulers = 0;
            iResponse<string> Xresult = APC2.GetSerialNumber();
            if (Xresult.IsCorrect)
            {
                var strlist = Xresult.Result.Split(',');
                Rulers = double.Parse(strlist[0]);
                return true;
            }
            else
            {
                var ems = new ErrMsg(201003, $"获取获取容栅尺位置失败，无法进行下一步测试\r\n{Xresult.GetContent(true, true)}");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }
        /// <summary>
        /// 设置液压泵打开与关闭
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetBump<T>(AutoTestItem item, Result<List<T>> result, bool OC = true)
        {
            var str = OC ? "打开液压泵" : "关闭液压泵";
            RealTimeMsg msg2 = new RealTimeMsg($"{str}");
            item.AddRealTimeMsgs(msg2);
            iResponse Xresult = APC2.SetRepairVent();
            if (Xresult.IsCorrect)
            {
                msg2.Content = " √ ";
                return true;
            }
            else
            {
                msg2.Content = $" X \r\n{Xresult.GetContent(true, true)}";
                var ems = new ErrMsg(201003, $"{str}失败，请断开工装供电！");
                result.SetConclusion($"联系项目组处理。", ems);
                return false;
            }
        }


        /// <summary>
        /// 获取当前控制模块量程上限
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetMoudelRange(out Pressure pressure)
        {
            pressure = new Pressure();
            iResponse<string> result = APC2.GetMoudelRange();
            if (result.IsCorrect)
            {
                var psplist = result.Result.Split(':')[4];
                string pat = "[a-zA-Z]+";
                var unit = Regex.Match(psplist, pat).Value;
                var pressureRange = double.Parse(psplist.Replace(unit, ""));
                pressure.Value = pressureRange;
                pressure = new Pressure(pressureRange, Xmas11.Domain.Unit.Parse(unit));
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前控制模块温度
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetMoudelTemp(out string Temp)
        {
            Temp = "";
            iResponse<string> result = APC2.GetMoudelTemp();
            if (result.IsCorrect)
            {
                var psplist = result.Result;
                string pat = @"\d+.\d+";
                var unit = Regex.Match(psplist, pat);
                if (unit.Success)
                {
                    Temp = unit.Value;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }


        /// <summary>
        /// 获取当前控制模块量程上下限
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetMoudelRangeAll(out PressureRange pressure)
        {
            pressure = new PressureRange();
            iResponse<string> result = APC2.GetMoudelRange();
            if (result.IsCorrect)
            {
                var psplist = result.Result.Split(':')[4];
                string pat = "[a-zA-Z]+";
                var unit = Regex.Match(psplist, pat).Value;
                var pressureRange = double.Parse(psplist.Replace(unit, ""));
                pressure.UpperValue = pressureRange;

                var psplist2 = result.Result.Split(':')[3];
                var pressureRange2 = double.Parse(psplist2.Replace(unit, ""));
                pressure.LowerValue = pressureRange2;

                pressure = new PressureRange(pressureRange2, pressureRange, Xmas11.Domain.Unit.Parse(unit));
                return true;
            }
            return false;
        }

        #endregion

        #endregion

        #region 版本升级

        /// <summary>
        /// 升级微差压控制板程序
        /// </summary>
        /// <returns></returns>
        public bool Update()
        {
            //查看WIFI状态并打开
            if (!SetWifiOpen())
            {
                return false;
            }
            int count = 0;
            while (true)
            {
                iResponse<OpenCloseState> response = APC2.GetWifiState();
                if (response.IsCorrect && response.Result == OpenCloseState.Open)
                {
                    break;
                }
                count++;
                System.Threading.Thread.Sleep(1000);
                if (count > 10)
                {
                    return false;
                }
            }
            //连接指定热点,并获取
            if (!ConnectWifiToHotspot("CONSTRD", "WPA2", "56975300"))
            {
                return false;
            }
            string targetIP = string.Empty;
            if (!GetWifiIPAddress(out targetIP))
            {
                return false;
            }
            //获取源文件路径和目标文件路径
            string targetFullPath = string.Format("ftp://{0}/", targetIP);
            string sourceFullPath = "ftp://rd.const.cc/FirmwareProgram/APC2/OS-LLP/APC2-LLP.apcupdate";
            //拷贝升级包文件
            if (!CopeFileBetweenServers(targetFullPath, sourceFullPath))
            {
                return false;
            }
            //发送升级指令
            iResponse result = APC2.SoftwareUpgrade("APC2-LLP.apcupdate");
            //判断是否重启完成，等待升级完毕
            System.Threading.Thread.Sleep(10000);
            while (true)
            {
                if (Open())
                {
                    break;
                }
                System.Threading.Thread.Sleep(2000);
            }

            return true;
        }

        /// <summary>
        /// 将升级文件从源服务器拷贝到目标服务器
        /// </summary>
        /// <param name="sourceFullPath">源文件路径</param>
        /// <param name="targetFullPath">目标文件路径</param>
        /// <returns></returns>
        public bool CopeFileBetweenServers(string sourceFullPath, string targetFullPath)
        {
            //源服务器账号密码
            string sourceUserName = "rdadmin";
            string sourcePassWord = "const-123456";
            //目标服务器账号密码
            string targetUserName = "cst";
            string targetPassWord = "cst";

            try
            {
                //1.1.从源服务器下载
                FtpWebRequest reqFtp;
                reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(sourceFullPath));//源ip与文件路径
                reqFtp.Credentials = new NetworkCredential(sourceUserName, sourcePassWord);
                reqFtp.Method = WebRequestMethods.Ftp.DownloadFile; //下载方法
                reqFtp.KeepAlive = false;
                reqFtp.UseBinary = true;
                reqFtp.Proxy = null;
                FtpWebResponse responseDate = (FtpWebResponse)reqFtp.GetResponse();
                //将从服务器1下载的响应流直接作为上传到服务器2的上传流
                Stream streamdate = responseDate.GetResponseStream();

                byte[] text = ReadFull(streamdate);
                //下载流
                Stream stream = ReadStream(text);
                //2.上传到目标服务器
                FtpWebRequest reqFTPUpload;
                reqFTPUpload = (FtpWebRequest)FtpWebRequest.Create(new Uri(targetFullPath));
                reqFTPUpload.Credentials = new NetworkCredential(targetUserName, targetPassWord);
                reqFTPUpload.Method = WebRequestMethods.Ftp.UploadFile; //上传方法
                reqFTPUpload.KeepAlive = false;
                reqFTPUpload.UseBinary = true;
                reqFTPUpload.Proxy = null;
                Stream requestStream = reqFTPUpload.GetRequestStream();

                int buffLength = 2048;  //每次读入文件流2kb
                byte[] buff = new byte[buffLength];
                int len = stream.Read(buff, 0, buff.Length);  //文件大小
                while (len > 0)
                {
                    requestStream.Write(buff, 0, len);
                    len = stream.Read(buff, 0, buffLength);
                }

                stream.Close();
                requestStream.Close();
                stream.Dispose();//释放资源
                requestStream.Dispose();//释放资源
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 将网络流转化为byte数组
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private byte[] ReadFull(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 将byte数组转化为网络流
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private Stream ReadStream(byte[] input)
        {
            MemoryStream stream = new MemoryStream(input);
            stream.Seek(0, SeekOrigin.Begin);
            //设置stream的position为流的开始
            return stream;
        }

        #endregion

        #region 程序自检
        /// <summary>
        /// 主板电源状态自检
        /// </summary>
        /// <param name="checkState">主板电源状态</param>
        /// <returns></returns>
        public bool GetMainBoardCheckState(out CheckState checkState)
        {
            iResponse<CheckState> response = APC2.GetMainBoardCheckState();
            if (response.IsCorrect)
            {
                checkState = response.Result;
                return true;
            }
            else
            {
                checkState = CheckState.UnKnown;
                return false;
            }
        }
        /// <summary>
        /// 控制板电源状态自检
        /// </summary>
        /// <param name="checkState"></param>
        /// <returns></returns>
        public bool GetControllerBroadPowerCheckState(out CheckState checkState)
        {
            iResponse<string> response = APC2.GetControllerException();
            if (response.IsCorrect)
            {
                if (response.Result == "00-00-00-00-00-00")
                    checkState = CheckState.OK;
                else
                    checkState = CheckState.Fault;
                return true;
            }
            else
            {
                checkState = CheckState.UnKnown;
                return false;
            }
        }
        public ScriptHelperKVP GetControllerBroadPowerCheckState_KVP(out CheckState checkState)
        {
            bool success = GetControllerBroadPowerCheckState(out checkState);
            string stateDisplay = success ? checkState.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A控制板电源状态自检:{stateDisplay}", success);
        }
        /// <summary>
        /// 电测板电源状态自检
        /// </summary>
        /// <param name="checkState"></param>
        /// <returns></returns>
        public bool GetElectricalBroadPowerCheckState(out CheckState checkState)
        {
            iResponse<string> response = APC2.GetElectricalException();
            if (response.IsCorrect)
            {
                if (response.Result == "00-00-00-00" || response.Result == "00-04-00-00")
                    checkState = CheckState.OK;
                else
                    checkState = CheckState.Fault;
                return true;
            }
            else
            {
                checkState = CheckState.UnKnown;
                return false;
            }
        }
        public ScriptHelperKVP GetElectricalBroadPowerCheckState_KVP(out CheckState checkState)
        {
            bool success = GetElectricalBroadPowerCheckState(out checkState);
            string stateDisplay = success ? checkState.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A电测板电源状态自检:{stateDisplay}", success);
        }
        /// <summary>
        /// 获取设备自检程序指定项结果
        /// </summary>
        /// <param name="function"></param>
        /// <param name="checkState"></param>
        /// <returns></returns>
        public bool GetCheckerState(ProgramFunction function, out ProgramFunctionCheckResult checkState)
        {
            iResponse<ProgramFunctionCheckResult> response = APC2.GetCheckerState(function);
            if (response.IsCorrect)
            {
                checkState = response.Result;
                return true;
            }
            else
            {
                checkState = ProgramFunctionCheckResult.Unknow;
                return false;
            }
        }
        #endregion 程序自检

        #region 读写控制板机型参数
        /// <summary>
        ///  读取控制板机型参数
        /// </summary>
        /// <param name="parameter">控制板机型参数</param>
        /// <returns></returns>
        public bool GetControlPanelModelParameter(out double parameter)
        {
            Xmas11.Comm.Devices.iResponse<double> getParameter = APC2.GetControlPanelModelParameter();
            if (!getParameter.IsCorrect)
            {
                parameter = 0;
                return false;
            }
            parameter = getParameter.Result;
            return true;
        }
        /// <summary>
        /// 写入控制板机型参数
        /// </summary>
        /// <returns></returns>
        public bool SetControlPanelModelParameter(double parameter)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse setParameter = APC2.SetControlPanelModelParameter(parameter);
            return setParameter.IsCorrect;
        }
        #endregion

        #region 屏幕显示
        /// <summary>
        /// 设置屏幕亮度
        /// </summary>
        /// <param name="type">亮度类型</param>
        /// <param name="level">亮度等级</param>
        /// <returns></returns>
        public bool SetBrightness(BrightnessType type, string level)
        {
            iResponse result = APC2.SetBrightness(type, level);
            return result.IsCorrect;
        }
        /// <summary>
        /// 杨声器发出DIO声
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP SetSpeakerDio()
        {
            APC2.SetSpeakerDio();
            return new ScriptHelperKVP("扬声器发出声音", true);
        }
        /// <summary>
        /// 关闭 屏幕扬声器检查程序
        /// </summary>
        /// <returns></returns>
        public bool SetCheckerClose()
        {
            iResponse result = APC2.SetCheckerClose();
            return result.IsCorrect;
        }
        /// <summary>
        /// 启动 屏幕扬声器检查程序
        /// </summary>
        /// <param name="function">检查项</param>
        /// <returns></returns>
        public bool SetCheckerOpen(ProgramFunction function)
        {
            iResponse result = APC2.SetCheckerOpen(function);
            return result.IsCorrect;
        }
        /// <summary>
        /// 切换 屏幕扬声器检查程序
        /// </summary>
        /// <param name="function">检查项</param>
        /// <returns></returns>
        public bool SetCheckerSelect(ProgramFunction function)
        {
            iResponse result = APC2.SetCheckerSelect(function);
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置30分钟自动关背光
        /// </summary>
        /// <returns></returns>
        public bool SetBackLightTime()
        {
            return APC2.SetBackLightTime(5).IsCorrect;
        }
        /// <summary>
        /// 设置30分钟自动休眠
        /// </summary>
        /// <returns></returns>
        public bool SetSleepTime()
        {
            return APC2.SetSleepTime(4).IsCorrect;
        }
        /// <summary>
        /// 设置从不自动关机
        /// </summary>
        /// <returns></returns>
        public bool SetCloseDeviceTime()
        {
            return APC2.SetCloseDeviceTime(0).IsCorrect;
        }
        #endregion 屏幕显示

        #region 按键指令
        /// <summary>
        /// 获取上次按键值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool GetLastKey(out KeyEnum key)
        {
            iResponse<KeyEnum> response = APC2.GetLastKey();
            if (response.IsCorrect)
            {
                key = response.Result;
                return true;
            }
            else
            {
                key = KeyEnum.Unknow;
                return false;
            }
        }
        #endregion

        #region 模块在线状态
        /// <summary>
        /// 获取压力模块在线状态
        /// </summary>
        /// <param name="mode">模块</param>
        /// <param name="state">状态</param>
        /// <returns></returns>
        public bool GetPressureModelOnlineState(OnLinePressureModelType mode, out OnOFFLineState state)
        {
            iResponse<OnOFFLineState> response = APC2.GetPressureModelOnlineState(mode);
            if (response.IsCorrect)
            {
                state = response.Result;
                return true;
            }
            else
            {
                state = OnOFFLineState.UnKnown;
                return false;
            }
        }
        /// <summary>
        /// 获取内部高压模块在线状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetInterHighPressureModelOnlineState(out OnOFFLineState state)
        {
            return GetPressureModelOnlineState(OnLinePressureModelType.InterHighPressure, out state);
        }
        /// <summary>
        /// 获取内部低压模块在线状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetInterLowPressureModelOnlineState(out OnOFFLineState state)
        {
            return GetPressureModelOnlineState(OnLinePressureModelType.InterLowPressure, out state);
        }

        /// <summary>
        /// 获取内部高压模块序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterHighPressureModelSerialNumber(out string serialNumber)
        {
            serialNumber = string.Empty;
            dynamic cdps = null;
            if (APC2.FW_HPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_HPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.DPSEXBase);

            iResponse<string> result = cdps.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            serialNumber = result.Result;
            return true;
        }
        public ScriptHelperKVP GetInterHighPressureModelSerialNumber_KVP(out string serialNumber)
        {
            bool success = GetInterHighPressureModelSerialNumber(out serialNumber);
            string display = success ? serialNumber : "获取失败";
            return new ScriptHelperKVP($"811A获取内部高压模块序列号:{display}", success);
        }

        public ScriptHelperKVP GetInterLowPressureModelSerialNumber_KVP(out string serialNumber)
        {
            bool success = GetInterLowPressureModelSerialNumber(out serialNumber);
            string display = success ? serialNumber : "获取失败";
            return new ScriptHelperKVP($"811A获取内部低压模块序列号:{display}", success);
        }
        /// <summary>
        /// 获取内部低压模块序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterLowPressureModelSerialNumber(out string serialNumber)
        {
            serialNumber = string.Empty;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<string> result = cdps.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            serialNumber = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部高压模传感器激励值
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterHighPressureModelSensorPowerSupplyValue(out double supplyValue)
        {
            supplyValue = 0;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyValue = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部低压模传感器激励值
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterLowPressureModelSensorPowerSupplyValue(out double supplyValue)
        {
            supplyValue = 0;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyValue = result.Result;
            return true;
        }

        /// <summary>
        /// 获取所有压力模块在线状态
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public bool GetPressureModelsOnlineState(out List<OpenCloseState> states)
        {
            states = new List<OpenCloseState>();
            iResponse<List<OpenCloseState>> response = APC2.GetPressureModelOnlineState();
            if (response.IsCorrect)
            {
                states = response.Result;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取内部高压模块序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterHighPressureSerialNumber(out string serialNumber)
        {
            serialNumber = string.Empty;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<string> result = cdps.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            serialNumber = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部低压模块序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterLowPressureSerialNumber(out string serialNumber)
        {
            serialNumber = string.Empty;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<string> result = cdps.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            serialNumber = result.Result;
            return true;
        }

        /// <summary>
        /// 获取内部高压模传感器激励值
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterHighPressureSensorPowerSupplyValue(out double supplyValue)
        {
            supplyValue = 0;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyValue = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部低压模传感器激励值
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetInterLowPressureSensorPowerSupplyValue(out double supplyValue)
        {
            supplyValue = 0;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyValue = result.Result;
            return true;
        }
        /// <summary>
        /// 是否复合量程
        /// </summary>
        /// <returns></returns>
        public bool? IsDoubleRange()
        {
            List<OpenCloseState> state = new List<OpenCloseState>();
            if (GetPressureModelsOnlineState(out state))
            {
                if (state[1] == OpenCloseState.Open && state[0] == OpenCloseState.Open)
                {
                    return true;
                }
                else if (state[1] == OpenCloseState.Open && state[0] == OpenCloseState.Close)
                {
                    return false;
                }
                else if (state[1] == OpenCloseState.Close && state[0] == OpenCloseState.Open)
                {
                    return false;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// 是否复合量程
        /// </summary>
        /// <returns></returns>
        public bool? IsHaveDoubleRange(out string msg)
        {
            msg = "";
            List<OpenCloseState> state = new List<OpenCloseState>();
            iResponse<List<OpenCloseState>> response = APC2.GetPressureModelOnlineState();
            if (response.IsCorrect)
            {
                state = response.Result;

                if (state[1] == OpenCloseState.Open && state[0] == OpenCloseState.Open)
                {
                    return true;
                }
                else if (state[1] == OpenCloseState.Open && state[0] == OpenCloseState.Close)
                {
                    return false;
                }
                else if (state[1] == OpenCloseState.Close && state[0] == OpenCloseState.Open)
                {
                    return false;
                }
                else
                {
                    msg = response.GetContent(true, true);
                    return null;
                }
            }
            else
            {
                msg = response.GetContent(true, true);
                return null;
            }

        }
        #endregion

        #region 风扇设置
        /// <summary>
        /// 设置风扇开启
        /// </summary>
        /// <returns></returns>
        public bool SetFANOn()
        {
            iResponse result = APC2.SetFANOn();
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置风扇关闭
        /// </summary>
        /// <returns></returns>
        public bool SetFANClose()
        {
            iResponse result = APC2.SetFANClose();
            return result.IsCorrect;
        }
        #endregion

        #region Hart
        public ScriptHelperKVP GetHartDeviceInfo(out string info)
        {
            var strRes = "811A获取HART设备信息";
            var res = APC2.GetHartDeviceInfo();
            if (res.IsCorrect)
            {
                info = res.Result;
                return new ScriptHelperKVP(strRes + "成功:" + info, true);
            }
            else
            {
                info = string.Empty;
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }

        /// <summary>
        /// 获取HART供电模式
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool GetSupplyMode(out ResistancePowerSupplyMode supplyMode)
        {
            supplyMode = ResistancePowerSupplyMode.Unknow;
            iResponse<ResistancePowerSupplyMode> result = APC2.GetHartSupplyMode();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyMode = result.Result;
            return true;
        }
        public ScriptHelperKVP GetSupplyMode_KVP(out ResistancePowerSupplyMode supplyMode)
        {
            bool success = GetSupplyMode(out supplyMode);
            string modeDisplay = success ? supplyMode.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A获取HART供电模式:{modeDisplay}", success);
        }
        /// <summary>
        /// 设置HART供电模式为IPIR
        /// </summary>
        /// <returns></returns>
        public bool SetSwitchMode_IPIR()
        {
            //0或IPIR：内部电源内部电阻；
            //1或EPER：外部电源外部电阻;
            //2或EPIR：外部电源内部电阻;
            //3或IPER：内部电源外部电阻
            iResponse result = APC2.SetHartSupplyMode(ResistancePowerSupplyMode.IPIR);
            return result.IsCorrect;
        }
        public ScriptHelperKVP SetHARTPowerSupplyMode_KVP(ResistancePowerSupplyMode mode)
        {
            bool success = APC2.SetHartSupplyMode(mode).IsCorrect;
            return new ScriptHelperKVP("811A设置HART供电模式为"+mode, success);
        }

        /// <summary>
        /// 获取HART地址和设备类型
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool GetEleHartMassage(out string msg)
        {
            msg = string.Empty;
            iResponse<string> result = APC2.GetHartMassage();
            if (!result.IsCorrect)
            {
                return false;
            }
            msg = result.Result;
            return true;
        }
        public class HARTMessage
        {
            public string Address { get; set; }
            public string DeviceType { get; set; }
            public string DeviceName { get; set; }
            public string DeviceModel { get; set; }
            public string DeviceSerialNumber { get; set; }
            public string DeviceManufacturer { get; set; }
            public string DeviceSoftwareVersion { get; set; }
            public string DeviceHardwareVersion { get; set; }
        }
        public ScriptHelperKVP GetEleHartMassage_KVP(out List<HARTMessage> msg)
        {
            bool success = GetEleHartMassage(out var msgstr);
            string display = success ? msgstr : "获取失败";
            msg = new List<HARTMessage>();
            if (msgstr != "0")
            {
                var spl1 = msgstr.Split(';');
                if (Convert.ToInt32(spl1[0]) > 0)
                {
                    var spl3 = spl1[1].Split('|');
                    foreach (var item in spl3)
                    {
                        var spl2 = item.Split(',');
                        msg.Add(new HARTMessage
                        {
                            Address = spl2.Length > 0 ? spl2[0] : string.Empty,
                            DeviceType = spl2.Length > 1 ? spl2[1] : string.Empty,
                            DeviceName = spl2.Length > 2 ? spl2[2] : string.Empty,
                            DeviceModel = spl2.Length > 3 ? spl2[3] : string.Empty,
                            DeviceSerialNumber = spl2.Length > 4 ? spl2[4] : string.Empty,
                            DeviceManufacturer = spl2.Length > 5 ? spl2[5] : string.Empty,
                            DeviceSoftwareVersion = spl2.Length > 6 ? spl2[6] : string.Empty,
                            DeviceHardwareVersion = spl2.Length > 7 ? spl2[7] : string.Empty
                        });
                    }
                }
            }
            return new ScriptHelperKVP($"811A获取HART地址和设备类型:{display}", success);
        }
        /// <summary>
        /// 设置电测类型为HART档
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_HART()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.HART, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_HART_KVP()
        {
            bool success = SetEleChannelItem_HART();
            return new ScriptHelperKVP("811A设置电测类型为HART档", success);
        }
        /// <summary>
        /// 设置电测类型为HART档
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_HARTClose()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.NONE, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_HARTClose_KVP()
        {
            bool success = SetEleChannelItem_HARTClose();
            return new ScriptHelperKVP("811A关闭HART电测档", success);
        }
        /// <summary>
        /// 切换电测功能为None
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_NONE()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.NONE, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 开始搜索
        /// </summary>
        /// <returns></returns>
        public bool StartSearchHart()
        {
            iResponse result = APC2.SearchHart(SearchState.Start);
            return result.IsCorrect;
        }
        public ScriptHelperKVP StartSearchHart_KVP()
        {
            bool success = StartSearchHart();
            return new ScriptHelperKVP("811A开始搜索HART设备", success);
        }
        /// <summary>
        /// 停止搜索
        /// </summary>
        /// <returns></returns>
        public bool StopSearchHart()
        {
            iResponse result = APC2.SearchHart(SearchState.Stop);
            return result.IsCorrect;
        }
        public ScriptHelperKVP StopSearchHart_KVP()
        {
            bool success = StopSearchHart();
            return new ScriptHelperKVP("811A停止搜索HART设备", success);
        }
        /// <summary>
        /// 只搜零地址
        /// </summary>
        /// <returns></returns>
        public bool ZeroSearchHart()
        {
            iResponse result = APC2.SearchHart(SearchState.Zero);
            return result.IsCorrect;
        }

        /// <summary>
        /// 连接指定地址的HART变送器
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public bool ConnectHart(int address)
        {
            iResponse result = APC2.ConnectHartDevice(address);
            return result.IsCorrect;
        }
        public ScriptHelperKVP ConnectHart_KVP(int address)
        {
            bool success = ConnectHart(address);
            return new ScriptHelperKVP($"811A连接指定地址的HART变送器:{address}", success);
        }
        #endregion


        #region PA变送器
        /// <summary>
        /// 搜索PA变送器
        /// </summary>
        /// <returns></returns>
        public bool SearchPA()
        {
            iResponse result = APC2.SearchPA();
            return result.IsCorrect;
        }
        public ScriptHelperKVP SearchPA_KVP()
        {
            iResponse result = APC2.SearchPA();
            return new ScriptHelperKVP("811A搜索PA变送器", result.IsCorrect);
        }
        /// <summary>
        /// 获取PA搜索列表
        /// </summary>
        /// <param name="massage">PA信息</param>
        /// <returns></returns>
        public bool GetPAMassage(out List<PAMassage> massage)
        {
            massage = new List<PAMassage>();
            try
            {
                iResponse<List<PAMassage>> result = APC2.GetPAMassage();
                massage = result.Result;
                if (massage == null)
                {
                    massage = new List<PAMassage>();
                }
                return result.IsCorrect;
            }
            catch (Exception)
            {
                massage = new List<PAMassage>();
                return false;
            }
        }
        public ScriptHelperKVP GetPAMassage_KVP(out List<PAMassage> massage)
        {
            massage = new List<PAMassage>();
            bool success = false;
            try
            {
                iResponse<List<PAMassage>> result = APC2.GetPAMassage();
                success = result.IsCorrect;
                massage = result.Result;
                if (massage == null)
                {
                    massage = new List<PAMassage>();
                }
            }
            catch (Exception)
            {
                success = false;
                massage = new List<PAMassage>();
            }
            return new ScriptHelperKVP("811A获取PA搜索列表", success);
        }
        /// <summary>
        /// 连接指定地址的PA变送器
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public bool ConnectPA(string address)
        {
            iResponse result = APC2.ConnectPA(address);
            return result.IsCorrect;
        }
        public ScriptHelperKVP ConnectPA_KVP(string address)
        {
            iResponse result = APC2.ConnectPA(address);
            return new ScriptHelperKVP($"811A连接PA变送器:地址{address}", result.IsCorrect);
        }
        #endregion

        #region 判稳使能

        /// <summary>
        /// 设置模块判稳使能
        /// </summary>
        /// <param name="type"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetModuleStableEnable(StableModuleType type, OpenCloseState enable)
        {
            iResponse response = APC2.SetModuleStableEnable(type, enable);
            return response.IsCorrect;
        }

        #endregion

        #region 电测
        /// <summary>
        /// 读取当前电测测量值
        /// </summary>
        /// <param name="electricMeasure">当前电测测量值</param>
        /// <returns></returns>
        public bool GetCurrentElectricMeasure(out ElectricMeasure electricMeasure)
        {
            iResponse<ElectricMeasure> result = APC2.GetCurrentElectricMeasure();
            if (!result.IsCorrect)
            {
                electricMeasure = new ElectricMeasure();
                return false;
            }
            electricMeasure = result.Result;
            return true;
        }
        public ScriptHelperKVP GetCurrentElectricMeasure_KVP(out ElectricMeasure electricMeasure)
        {
            var result = APC2.GetCurrentElectricMeasure();
            bool success = result.IsCorrect;
            electricMeasure = success ? result.Result : new ElectricMeasure();
            return new ScriptHelperKVP("811A读取当前电测测量值"+(success?electricMeasure.MeasureValue.ToString():"x"), success);
        }
        /// <summary>
        /// 设置电测类型为电压档
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_VOL()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.VOL, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public bool SetEleChannelItem_MVOL()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.MVOL, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_VOL_KVP()
        {
            bool success = SetEleChannelItem_VOL();
            return new ScriptHelperKVP("811A设置电测类型为电压档", success);
        }
        public ScriptHelperKVP SetEleChannelItem_MVOL_KVP()
        {
            bool success = SetEleChannelItem_MVOL();
            return new ScriptHelperKVP("811A设置电测类型为毫伏电压档", success);
        }
        /// <summary>
        /// 设置电测类型为电流档
        /// </summary>
        /// <param name="isSupply">电流档位环路供电</param>
        /// <returns></returns>
        public bool SetEleChannelItem_CURR(bool isSupply)
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.CURR, Convert.ToInt32(isSupply));
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_CURR_KVP(bool isSupply)
        {
            bool success = SetEleChannelItem_CURR(isSupply);
            string supplyDesc = isSupply ? "开启" : "关闭";
            return new ScriptHelperKVP($"811A设置电测类型为电流档,环路供电:{supplyDesc}", success);
        }
        /// <summary>
        /// 设置电测类型为PA变送器档
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_PA()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.PA, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_PA_KVP()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.PA, 0);
            return new ScriptHelperKVP("811A设置电测类型为PA变送器档", result.IsCorrect);
        }
        #region 开关测试
        /// <summary>
        /// 设置电测类型为开关档,普通开关
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_SW_Normal()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.SW, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_SW_Normal_KVP()
        {
            bool success = SetEleChannelItem_SW_Normal();
            return new ScriptHelperKVP("811A设置电测类型为开关档,普通开关", success);
        }
        /// <summary>
        /// 设置电测类型为开关档,NPN型
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_SW_NPN()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.SW, 1);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 设置电测类型为开关档,PNP型
        /// </summary>
        /// <returns></returns>
        public bool SetEleChannelItem_SW_PNP()
        {
            iResponse result = APC2.SetElectricMeasureFunction(ElectricMeasureFunction.SW, 2);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetEleChannelItem_SW_PNP_KVP()
        {
            bool success = SetEleChannelItem_SW_PNP();
            return new ScriptHelperKVP("811A设置电测类型为开关档,PNP型", success);
        }

        public ScriptHelperKVP SetEleChannelItem_SW_NPN_KVP()
        {
            bool success = SetEleChannelItem_SW_NPN();
            return new ScriptHelperKVP("811A设置电测类型为开关档,NPN型", success);
        }
        #endregion

        #endregion


        #region 电输出
        /// <summary>
        /// 设置电测输出档位
        /// </summary>
        /// <param name="electricSourceFunction">电测输出档</param>
        /// <returns></returns>
        public bool SetElectricSourceFunction(ElectricSourceFunction electricSourceFunction)
        {
            iResponse result = APC2.SetElectricSourceFunction(electricSourceFunction, 0);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetElectricSourceFunction_KVP(ElectricSourceFunction electricSourceFunction)
        {
            bool success = SetElectricSourceFunction(electricSourceFunction);
            return new ScriptHelperKVP($"811A设置电测输出档位:{electricSourceFunction}", success);
        }
        /// <summary>
        /// 设置电测输出档位为电流档
        /// </summary>
        /// <param name="isSupply">电流档位环路供电</param>
        /// <returns></returns>
        public bool SetElectricSource_MA(bool isSupply)
        {
            iResponse result = APC2.SetElectricSourceFunction(ElectricSourceFunction.mA, Convert.ToInt32(isSupply));
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetElectricSource_MA_KVP(bool isSupply)
        {
            bool success = SetElectricSource_MA(isSupply);
            string supplyDesc = isSupply ? "开启" : "关闭";
            return new ScriptHelperKVP($"811A设置电测输出档位为电流档,环路供电:{supplyDesc}", success);
        }
        /// <summary>
        /// 设置电测输出目标值
        /// </summary>
        /// <param name="target">目标值</param>
        /// <returns></returns>
        public bool SetElectricSourceTarget(double target)
        {
            iResponse result = APC2.SetElectricSourceTarget(target);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetElectricSourceTarget_KVP(double target)
        {
            bool success = SetElectricSourceTarget(target);
            return new ScriptHelperKVP($"811A设置电测输出目标值:{target}", success);
        }
        #endregion


        #region WIFI
        /// <summary>
        /// 链接wifi到指定热点
        /// </summary>
        /// <param name="encryptionMode">wifi加密方式</param>
        /// <param name="password">wifi密码</param>
        /// <param name="ssid">wifi名称</param>
        /// <returns></returns>
        public bool ConnectWifiToHotspot(string ssid, string encryptionMode, string password)
        {
            iResponse response = APC2.ConnectWifiToHotspot(ssid, encryptionMode, password);
            return response.IsCorrect;
        }
        public ScriptHelperKVP ConnectWifiToHotspot_KVP(string ssid, string encryptionMode, string password)
        {
            iResponse response = APC2.ConnectWifiToHotspot(ssid, encryptionMode, password);
            return new ScriptHelperKVP($"811A链接wifi到指定热点:{ssid}", response.IsCorrect);
        }
        /// <summary>
        /// 获取以太网IP地址
        /// </summary>
        /// <param name="IPAddress"></param>
        /// <returns></returns>
        public bool GetEthemetIPAddress(out string IPAddress)
        {
            IPAddress = string.Empty;
            iResponse<string> response = APC2.GetStaticETHemetIPAddress();
            if (response.IsCorrect)
            {
                IPAddress = response.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取WIFI IP地址
        /// </summary>
        /// <param name="IPAddress"></param>
        /// <returns></returns>
        public bool GetWifiIPAddress(out string IPAddress)
        {
            IPAddress = string.Empty;
            iResponse<string> response = APC2.GetWifiIPAddress();
            if (response.IsCorrect)
            {
                IPAddress = response.Result;
                return true;
            }
            return false;
        }
        public ScriptHelperKVP GetWifiIPAddress_KVP(out string IPAddress)
        {
            IPAddress = string.Empty;
            iResponse<string> response = APC2.GetWifiIPAddress();
            bool success = response.IsCorrect;
            if (success)
            {
                IPAddress = response.Result;
            }
            string addressDisplay = success ? IPAddress : "获取失败";
            return new ScriptHelperKVP($"811A获取WIFI IP地址:{addressDisplay}", success);
        }
        /// <summary>
        /// 获取wifi功能状态
        /// </summary>
        /// <param name="functionState"></param>
        /// <returns></returns>
        public bool GetWLANFunctionState(out bool functionState)
        {
            functionState = false;
            iResponse<List<bool>> response = APC2.GetFunctionState(FunctionType.WLAN);
            if (response.IsCorrect)
            {
                functionState = response.Result[0];
                return true;
            }
            return false;
        }
        public ScriptHelperKVP GetWLANFunctionState_KVP(out bool functionState)
        {
            functionState = false;
            //XMAS11 看枚举对应不要看注释!!!
            iResponse<List<bool>> response = APC2.GetFunctionState(FunctionType.WLAN);
            bool success = response.IsCorrect;
            if (success)
            {
                functionState = response.Result[0];
            }
            return new ScriptHelperKVP("811A获取wifi功能状态", success);
        }

        public bool GetFunctionState(FunctionType functionType, out OpenCloseState functionState)
        {
            functionState = OpenCloseState.UnKnown;
            iResponse<List<bool>> response = APC2.GetFunctionState(functionType);
            if (response.IsCorrect)
            {
                functionState = response.Result[0] ? OpenCloseState.Open : OpenCloseState.Close;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 打开wifi功能状态
        /// </summary>
        /// <returns></returns>
        public bool OpenWLANFunction()
        {
            return APC2.SetFunctionState(FunctionType.WLAN, OpenCloseState.Open).IsCorrect;
        }
        public ScriptHelperKVP OpenWLANFunction_KVP()
        {
            bool success = APC2.SetFunctionState(FunctionType.WLAN, OpenCloseState.Open).IsCorrect;
            return new ScriptHelperKVP("811A打开wifi功能状态", success);
        }
        /// <summary>
        ///  关闭wifi功能状态
        /// </summary>
        /// <returns></returns>
        public bool CloseWLANFunction()
        {
            return APC2.SetFunctionState(FunctionType.WLAN, OpenCloseState.Close).IsCorrect;
        }



        /// <summary>
        /// 获取WIFI开关状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetWifiState(out bool state)
        {
            state = false;
            iResponse<OpenCloseState> result = APC2.GetWifiState();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result == OpenCloseState.Open ? true : false;
            return true;
        }
        public ScriptHelperKVP GetWifiState_KVP(out bool state)
        {
            state = false;
            iResponse<OpenCloseState> result = APC2.GetWifiState();
            bool success = result.IsCorrect;
            if (success)
            {
                state = result.Result == OpenCloseState.Open;
            }
            string stateDisplay = success ? (state ? "开启" : "关闭") : "获取失败";
            return new ScriptHelperKVP($"811A获取WIFI开关状态:{stateDisplay}", success);
        }
        /// <summary>
        /// 设置WIFI开
        /// </summary>
        /// <returns></returns>
        public bool SetWifiOpen()
        {
            iResponse result = APC2.SetWifiState(OpenCloseState.Open);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetWifiOpen_KVP()
        {
            iResponse result = APC2.SetWifiState(OpenCloseState.Open);
            return new ScriptHelperKVP("811A设置WIFI开", result.IsCorrect);
        }
        /// <summary>
        /// 设置WIFI关
        /// </summary>
        /// <returns></returns>
        public bool SetWifiClose()
        {
            iResponse result = APC2.SetWifiState(OpenCloseState.Close);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SetWifiClose_KVP()
        {
            iResponse result = APC2.SetWifiState(OpenCloseState.Close);
            return new ScriptHelperKVP("811A设置WIFI关", result.IsCorrect);
        }
        /// <summary>
        /// 获取WIFI地址
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool GetWiFiAddress(out string address)
        {
            address = string.Empty;
            iResponse<string> result = APC2.GetWiFiAddress();
            if (!result.IsCorrect)
            {
                return false;
            }
            address = result.Result;
            return true;
        }
        public ScriptHelperKVP GetWiFiAddress_KVP(out string address)
        {
            address = string.Empty;
            iResponse<string> result = APC2.GetWiFiAddress();
            bool success = result.IsCorrect;
            if (success)
            {
                address = result.Result;
            }
            string addressDisplay = success ? address : "获取失败";
            return new ScriptHelperKVP($"811A获取WIFI地址:{addressDisplay}", success);
        }
        /// <summary>
        /// 获取WIFI连接状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetConnectWifiState(out string state)
        {
            state = string.Empty;
            iResponse<string> result = APC2.GetConnectWifiState();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }
        public ScriptHelperKVP GetConnectWifiState_KVP(out string state)
        {
            state = string.Empty;
            iResponse<string> result = APC2.GetConnectWifiState();
            bool success = result.IsCorrect;
            if (success)
            {
                state = result.Result;
            }
            string stateDisplay = success ? state : "获取失败";
            return new ScriptHelperKVP($"811A获取WIFI连接状态:{stateDisplay}", success);
        }
        #endregion

        #region 声音

        /// <summary>
        /// 设置系统音量100%
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetSoundValue()
        {
            return APC2.SetSoundValue(100).IsCorrect;
        }
        /// <summary>
        /// 打开按键音
        /// </summary>
        /// <returns></returns>
        public bool SetKeyToneStateOpen()
        {
            return APC2.SetKeyToneState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 打开提示音
        /// </summary>
        /// <returns></returns>
        public bool SetPromptSoundStateOpen()
        {
            return APC2.SetPromptSoundState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 打开超量程报警音
        /// </summary>
        /// <returns></returns>
        public bool SetAlarmSoundStateOpen()
        {
            return APC2.SetAlarmSoundState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 打开快照提示音
        /// </summary>
        /// <returns></returns>
        public bool SetPotoshopSoundStateOpen()
        {
            return APC2.SetSnapshotState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 打开系统稳定提示音
        /// </summary>
        /// <returns></returns>
        public bool SetStableSoundStateOpen()
        {
            return APC2.SetStableSoundState(OpenCloseState.Open).IsCorrect;
        }

        #endregion

        #region 蓝牙
        /// <summary>
        /// 打开蓝牙
        /// </summary>
        /// <returns></returns>
        public bool OpenBlueTooth()
        {
            iResponse result = APC2.SetBlueToothState(OpenCloseState.Open);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP OpenBlueTooth_KVP()
        {
            iResponse result = APC2.SetBlueToothState(OpenCloseState.Open);
            return new ScriptHelperKVP("811A打开蓝牙", result.IsCorrect);
        }
        /// <summary>
        /// 关闭蓝牙
        /// </summary>
        /// <returns></returns>
        public bool CloseBlueTooth()
        {
            iResponse result = APC2.SetBlueToothState(OpenCloseState.Close);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP CloseBlueTooth_KVP()
        {
            iResponse result = APC2.SetBlueToothState(OpenCloseState.Close);
            return new ScriptHelperKVP("811A关闭蓝牙", result.IsCorrect);
        }
        /// <summary>
        /// 获取蓝牙状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetBlueToothState(out OpenCloseState state)
        {
            iResponse<OpenCloseState> result = APC2.GetBlueToothState();
            if (result.IsCorrect)
            {
                state = result.Result;
                return true;
            }
            state = OpenCloseState.UnKnown;
            return false;
        }
        public ScriptHelperKVP GetBlueToothState_KVP(out OpenCloseState state)
        {
            iResponse<OpenCloseState> result = APC2.GetBlueToothState();
            bool success = result.IsCorrect;
            if (success)
            {
                state = result.Result;
            }
            else
            {
                state = OpenCloseState.UnKnown;
            }
            string stateDisplay = success ? state.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A获取蓝牙状态:{stateDisplay}", success);
        }
        /// <summary>
        /// 获取蓝牙名称
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool GetBlueToothName(out string name)
        {
            iResponse<string> result = APC2.GetBlueToothName();
            if (result.IsCorrect)
            {
                name = result.Result;
                return true;
            }
            name = string.Empty;
            return false;
        }
        public ScriptHelperKVP GetBlueToothName_KVP(out string name)
        {
            iResponse<string> result = APC2.GetBlueToothName();
            bool success = result.IsCorrect;
            if (success)
            {
                name = result.Result;
            }
            else
            {
                name = string.Empty;
            }
            string nameDisplay = success ? name : "获取失败";
            return new ScriptHelperKVP($"811A获取蓝牙名称:{nameDisplay}", success);
        }
        public ScriptHelperKVP GetBluetoothMAC(out string mac)
        {
            var res = APC2.GetBlueToothMAC();
            mac=res.IsCorrect?res.Result:string.Empty;
            return new ScriptHelperKVP($"811A获取蓝牙MAC:{mac}", res.IsCorrect);
        }
        #endregion


        #region 存储器
        /// <summary>
        /// 获取UBSlocation
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public bool GetUSBLocation(out string location)
        {
            location = string.Empty;
            iResponse<List<string>> result = APC2.GetUSB_Location();
            if (!result.IsCorrect)
            {
                return false;
            }
            location = string.Join("", result.Result.ToArray());
            return true;
        }
        /// <summary>
        /// 读取USB设备状态判断是否有U盘
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetUSBdriveState(out bool state)
        {
            state = false;
            iResponse<bool> result = APC2.USBdriveState();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }
        /// <summary>
        /// 获取USB设备U盘大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool GetUSBdriveSize(out long[] size)
        {
            size = null;
            iResponse<long[]> result = APC2.DiskSize("Hard Disk");
            if (!result.IsCorrect)
            {
                return false;
            }
            size = result.Result;
            return true;
        }
        /// <summary>
        /// 获取SD卡大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool GetStorageCardSize(out long[] size)
        {
            size = null;
            iResponse<long[]> result = APC2.DiskSize("Storage_Card");
            if (!result.IsCorrect)
            {
                return false;
            }
            size = result.Result;
            return true;
        }
        public ScriptHelperKVP GetStorageCardSize_KVP(out long[] size)
        {
            size = null;
            iResponse<long[]> result = APC2.DiskSize("Storage_Card");
            bool success = result.IsCorrect;
            if (success)
            {
                size = result.Result;
            }
            return new ScriptHelperKVP("811A获取SD卡大小", success);
        }
        /// <summary>
        /// 通过USB向U盘添加文件
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool AddDataToUSB(string file, string value)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            iResponse result = APC2.AddDatatoUSB(file, Convert.ToBase64String(bytes), FileWriteType.TRUNcate);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 删除U盘指定路径文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool? DelUSBfile(string fileName)
        {
            string path = string.Format("\\Hard Disk\\{0}", fileName);
            iResponse result = APC2.Delfile(path);
            return result.IsCorrect;
        }
        /// <summary>
        /// 判断U盘文件是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool QueryUSBfileExists(string fileName)
        {
            string path = string.Format("\\Hard Disk\\{0}", fileName);
            iResponse<bool> result = APC2.QueryfileExists(path);
            if (!result.IsCorrect)
            {
                return false;   //指令未执行
            }
            return result.Result;
        }
        /// <summary>
        ///  通过USB读取U盘指定文件信息
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadDataFromUSB(string file, out string value)
        {
            value = string.Empty;
            iResponse<string> result = APC2.ReadDatatoUSB(file);
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }
        /// <summary>
        ///  读取SD状态判断是否存在
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetStorageCardState(out bool state)
        {
            state = false;
            iResponse<bool> result = APC2.StorageCardState();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }
        public ScriptHelperKVP GetStorageCardState_KVP(out bool state)
        {
            state = false;
            iResponse<bool> result = APC2.StorageCardState();
            bool success = result.IsCorrect;
            if (success)
            {
                state = result.Result;
            }
            string stateDisplay = success ? (state ? "存在" : "不存在") : "获取失败";
            return new ScriptHelperKVP($"811A读取SD状态判断是否存在:{stateDisplay}", success);
        }
        /// <summary>
        /// 向SD添加文件
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool AddDataToSD(string file, string value)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            iResponse result = APC2.DataAddtoStorageCard(file, Convert.ToBase64String(bytes), FileWriteType.TRUNcate);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP AddDataToSD_KVP(string file, string value)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            iResponse result = APC2.DataAddtoStorageCard(file, Convert.ToBase64String(bytes), FileWriteType.TRUNcate);
            return new ScriptHelperKVP($"811A向SD添加文件:{file}", result.IsCorrect);
        }
        /// <summary>
        /// 读取SD指定文件信息
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ReadDataFromSD(string file, out string value)
        {
            value = string.Empty;
            iResponse<string> result = APC2.DataReadtoStorageCard(file);
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }
        public ScriptHelperKVP ReadDataFromSD_KVP(string file, out string value)
        {
            value = string.Empty;
            iResponse<string> result = APC2.DataReadtoStorageCard(file);
            bool success = result.IsCorrect;
            if (success)
            {
                value = result.Result;
            }
            string contentDisplay = success ? value : "获取失败";
            return new ScriptHelperKVP($"811A读取SD指定文件信息:{contentDisplay}", success);
        }
        /// <summary>
        /// 删除SD卡指定路径文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool? DelSDCardfile(string fileName)
        {
            string path = string.Format("\\Storage_Card\\{0}", fileName);
            iResponse result = APC2.Delfile(path);
            return result.IsCorrect;
        }
        public ScriptHelperKVP DelSDCardfile_KVP(string fileName, out bool? result)
        {
            result = DelSDCardfile(fileName);
            bool success = result.HasValue;
            string resultDesc = success ? result.Value.ToString() : "操作失败";
            return new ScriptHelperKVP($"811A删除SD卡指定路径文件:{fileName},结果:{resultDesc}", success);
        }
        /// <summary>
        /// 判断SD卡文件是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool QuerySDCardfileExists(string fileName)
        {
            string path = string.Format("\\Storage_Card\\{0}", fileName);
            iResponse<bool> result = APC2.QueryfileExists(path);
            if (!result.IsCorrect)
            {
                return false;   //指令未执行
            }
            return result.Result;
        }
        public ScriptHelperKVP QuerySDCardfileExists_KVP(string fileName, out bool exists)
        {
            exists = QuerySDCardfileExists(fileName);
            return new ScriptHelperKVP($"811A判断SD卡文件是否存在:{fileName},结果:{exists}", exists);
        }
        /// <summary>
        /// 设置CSV文件格式为小数点
        /// </summary>
        /// <returns></returns>
        public bool SetCSVFileFormat()
        {
            return APC2.SetCSVFileFormat(0).IsCorrect;
        }
        /// <summary>
        /// 设置开启主题暗色
        /// </summary>
        /// <returns></returns>
        public bool SetTheme()
        {
            return APC2.SetTheme(SystemThemeModel.Dark, false).IsCorrect;
        }
        #endregion


        #region 压力

        /// <summary>
        /// 获取表绝压切换功能状态
        /// </summary>
        /// <returns></returns>
        public bool GetControlFeatureConfig(out OpenCloseState state)
        {
            var response = APC2.GetControlFeatureConfig("SupportPTypeChange");
            state = response.Result;
            return response.IsCorrect;
        }

        /// <summary>
        /// 获取大气压传感器压力值
        /// </summary>
        /// <param name="AtmosSensor"></param>
        /// <returns></returns>
        public bool GetAtmosSensor(out Xmas11.Domain.Mechanics.Pressure AtmosSensor)
        {
            iResponse<Xmas11.Domain.Mechanics.Pressure> getAtmosSensor = APC2.GetAtmos();
            if (!getAtmosSensor.IsCorrect)
            {
                AtmosSensor = new Pressure(0, PressureUnit.kPa);
                return false;
            }

            AtmosSensor = Pressure.ConvertTo(getAtmosSensor.Result, PressureUnit.kPa);
            return true;
        }
        /// <summary>
        /// 获取大气压模块SN号
        /// </summary>
        /// <param name="AtmosphericSensorSN"></param>
        /// <returns></returns>
        public bool GetAtmosSensorSN(out string AtmosphericSensorSN)
        {
            Xmas11.Comm.Devices.iResponse<string> getAtmosphericSensorSN = APC2.GetAtmosSensorSN();
            if (!getAtmosphericSensorSN.IsCorrect)
            {
                AtmosphericSensorSN = string.Empty;
                return false;
            }
            AtmosphericSensorSN = getAtmosphericSensorSN.Result;
            return true;
        }
        /// <summary>
        /// 设置大气压模块SN号
        /// </summary>
        /// <param name="AtmosphericSensorSN"></param>
        /// <returns></returns>
        public bool SetAtmosSensorSN(string AtmosphericSensorSN)
        {
            return APC2.SetAtmosSensorSN(AtmosphericSensorSN);
        }
        /// <summary>
        /// 获取大气压模块量程
        /// </summary>
        /// <param name="AtmosphericSensorSN"></param>
        /// <returns></returns>
        public bool GetAtmosSensorPressureRange(out PressureRange AtmosphericSensorSN)
        {
            Xmas11.Comm.Devices.iResponse<PressureRange> getAtmosphericSensorSN = APC2.GetAtmosSensorPressureRange();
            if (!getAtmosphericSensorSN.IsCorrect)
            {
                AtmosphericSensorSN = new PressureRange();
                return false;
            }
            AtmosphericSensorSN = getAtmosphericSensorSN.Result;
            return true;
        }
        /// <summary>
        /// 正压泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestPositivePump()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.TestPump(PumpTestItem.Positive);
            return testPump.IsCorrect;
        }
        /// <summary>
        /// 正压泵测试
        /// </summary>
        /// <param name="PressureTime">造压时间(默认120s)</param>
        /// <param name="Franchise">超差标准(默认0.005)</param>
        /// <returns></returns>
        public bool TestPositivePump(int PressureTime, double Franchise)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.TestPump(PumpTestItem.Positive, PressureTime, Franchise);
            return testPump.IsCorrect;
        }
        /// <summary>
        /// 负压泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestNegativePump()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.TestPump(PumpTestItem.Negative);
            return testPump.IsCorrect;
        }
        /// <summary>
        /// 负压泵测试
        /// </summary>
        /// <param name="PressureTime">造压时间(默认120s)</param>
        /// <param name="Franchise">超差标准(默认0.005)</param>
        /// <returns></returns>
        public bool TestNegativePump(int PressureTime, double Franchise)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.TestPump(PumpTestItem.Negative, PressureTime, Franchise);
            return testPump.IsCorrect;
        }
        /// <summary>
        /// 终止泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestPumpStop()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPumpStop = APC2.TestPump(PumpTestItem.Stop);
            return testPumpStop.IsCorrect;
        }
        /// <summary>
        /// 气泵测试状态
        /// </summary>
        /// <param name="testState"></param>
        /// <returns></returns>
        public bool GetPumpTestState(out PumpTestState testState)
        {
            testState = new PumpTestState();
            Xmas11.Comm.Devices.iResponse<PumpTestState> getPumpTestState = APC2.GetPumpTestState();
            if (getPumpTestState.IsCorrect)
            {
                testState = getPumpTestState.Result;
            }
            return getPumpTestState.IsCorrect;
        }
        /// <summary>
        /// 获取内部模块压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressure_IPM(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<Pressure> getInternalModulePressure = APC2.GetPressure_IPM();
            if (getInternalModulePressure.IsCorrect)
            {
                pressure = getInternalModulePressure.Result;
            }
            return getInternalModulePressure.IsCorrect;
        }
        /// <summary>
        ///  获取内部模块温度
        /// </summary>
        /// <param name="temperature"></param>
        /// <returns></returns>
        public bool GetTemperature_IPM(out Temperature temperature)
        {
            temperature = new Temperature() { Value = 0, Unit = TemperatureUnit.C };
            iResponse<InterPressureModuleInfo> getInternalModuleInfo = APC2.GetInterPressureModuleInfo();
            if (getInternalModuleInfo.IsCorrect)
            {
                temperature.Value = getInternalModuleInfo.Result.HighModuleTemperature;
            }
            return getInternalModuleInfo.IsCorrect;
        }
        /// <summary>
        ///  获取内部两个模块温度
        /// </summary>
        /// <param name="temperature"></param>
        /// <returns></returns>
        public bool GetTemperature_IPMALL(out InterPressureModuleInfo temperature)
        {
            temperature = new InterPressureModuleInfo();
            iResponse<InterPressureModuleInfo> getInternalModuleInfo = APC2.GetInterPressureModuleInfo();
            if (getInternalModuleInfo.IsCorrect)
            {
                temperature = getInternalModuleInfo.Result;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetPressureType(out PressureType pressureType)
        {
            pressureType = PressureType.UnKnown;
            iResponse<PressureType> getPressureType = APC2.GetPressureModelPressureType(PressureModel.ControllerModule);
            if (getPressureType.IsCorrect)
            {
                pressureType = getPressureType.Result;
            }
            return getPressureType.IsCorrect;
        }
        /// <summary>
        /// 设置压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool SetPressureType(PressureType pressureType)
        {
            iResponse setPressureType = APC2.SetPressureModelPressureType(PressureModel.ControllerModule, pressureType);
            return setPressureType.IsCorrect;
        }

        /// <summary>
        /// 获取正压气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetSupplyPressure(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getSupplyPressure = APC2.GetSupplyPressure();
            if (getSupplyPressure.IsCorrect)
            {
                pressure = getSupplyPressure.Result;
            }
            return getSupplyPressure.IsCorrect;
        }
        public ScriptHelperKVP GetSupplyPressure_KVP(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            bool success = GetSupplyPressure(out pressure);
            string pressureDisplay = success ? pressure.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A获取正压气源压力:{pressureDisplay}", success);
        }
        /// <summary>
        /// 获取真空气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetVacuumPressure(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getVacuumPressure = APC2.GetVacuumPressure();
            if (getVacuumPressure.IsCorrect)
            {
                pressure = getVacuumPressure.Result;
            }
            return getVacuumPressure.IsCorrect;
        }
        public ScriptHelperKVP GetVacuumPressure_KVP(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            bool success = GetVacuumPressure(out pressure);
            string pressureDisplay = success ? pressure.ToString() : "获取失败";
            return new ScriptHelperKVP($"811A获取真空气源压力:{pressureDisplay}", success);
        }
        /// <summary>
        /// 设定开关阀状态
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetValveStata(int value)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse setValveStata = APC2.SetSwitchValveState(value);
            return setValveStata.IsCorrect;
        }
        /// <summary>
        /// 获取阀状态
        /// </summary>
        /// <param name="stateValue"></param>
        /// <returns></returns>
        public bool GetValveStateValue(out int stateValue)
        {
            Xmas11.Comm.Devices.iResponse<int> getValveStateValue = APC2.GetSwitchValveState();
            if (getValveStateValue.IsCorrect)
            {
                stateValue = getValveStateValue.Result;
                return true;
            }
            stateValue = 0;
            return false;
        }
        /// <summary>
        /// 获取当前压力测试状态
        /// </summary>
        /// <param name="controlMode"></param>
        /// <returns></returns>
        public bool GetPressureControlMode(out DevicePressureControlMode controlMode)
        {
            Xmas11.Comm.Devices.iResponse<DevicePressureControlMode> getPressureControlMode = APC2.GetPressureControlMode();
            if (getPressureControlMode.IsCorrect)
            {
                controlMode = getPressureControlMode.Result;
                return true;
            }
            controlMode = DevicePressureControlMode.UnKnown;
            return false;
        }
        /// <summary>
        /// 读取设定点编辑范围
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetSetPointLimitPressureRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange() { LowerValue = 0, UpperValue = 0, Unit = PressureUnit.kPa };
            iResponse<PressureRange> result = APC2.GetSetPointEditPressureRange();
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 读取输出压力的设定点上限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureControlRange_UpperLimit(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<PressureRange> result = APC2.GetSetPointEditPressureRange();
            if (result.IsCorrect)
            {
                double value = GetSmallerValue(result.Result.Upper.Value);
                pressure.Value = value;
                pressure.Unit = result.Result.Upper.Unit;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 读取输出压力的设定点下限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureControlRange_LowerLimit(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<PressureRange> result = APC2.GetSetPointEditPressureRange();
            if (result.IsCorrect)
            {
                double value = GetSmallerValue(result.Result.Lower.Value);
                pressure.Value = value;
                pressure.Unit = result.Result.Lower.Unit;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取当前控制模块量程
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetPressureControlRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            iResponse<PressureRange> result = APC2.GetSetPointEditPressureRange();
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前模块稳定度和稳定时间
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetControllerModuleConfig(out string Result)
        {
            Result = string.Empty;
            iResponse<string> result = APC2.GetControlPressureModelInfo(4);
            if (result.IsCorrect)
            {
                Result = $"高压 {result.Result}";
                return true;
            }
            result = APC2.GetControlPressureModelInfo(5);
            if (result.IsCorrect)
            {
                Result += $"低压 {result.Result}";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取内部高压模块编号
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetControllerModuleSerialNumber(out string SN)
        {
            SN = string.Empty;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.ControllerModule);
            if (result.IsCorrect)
            {
                SN = result.Result.Split(',')[0].Trim();
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部高压模块编号
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetHighPressureModelSerialNumber(out string SN)
        {
            SN = string.Empty;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.InterHighPressure);
            if (result.IsCorrect)
            {
                SN = result.Result.Split(',')[0].Trim();
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部低压模块编号
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetLowPressureModelSerialNumber(out string SN)
        {
            SN = string.Empty;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.InterLowPressure);
            if (result.IsCorrect)
            {
                SN = result.Result.Split(',')[0].Trim();
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取当前模块精度
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool GetControllerModelAccuracyLevel(out double level)
        {
            level = 0;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.ControllerModule);
            if (result.IsCorrect)
            {
                level = Convert.ToDouble(result.Result.Split(',')[4].Trim('%')) /*/ 100*/;//取消百分比转换
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部高压模块精度
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool GetHighPressureModelAccuracyLevel(out double level)
        {
            level = 0;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.InterHighPressure);
            if (result.IsCorrect)
            {
                if (result.Result.Contains("---"))
                {
                    level = 999;
                }
                else
                {
                    level = Convert.ToDouble(result.Result.Split(',')[4].Trim('%')) / 100;
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部低压模块精度
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool GetLowPressureModelAccuracyLevel(out double level)
        {
            level = 0;
            iResponse<string> result = APC2.GetControlPressureModelInfo(PressureModel.InterLowPressure);
            if (result.IsCorrect)
            {
                if (result.Result.Contains("---"))
                {
                    level = 999;
                }
                else
                {
                    level = Convert.ToDouble(result.Result.Split(',')[4].Trim('%')) / 100;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前控制模块量程
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetControllerModuleRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            iResponse<PressureRange> result = APC2.GetControlPressureModelRange(PressureModel.ControllerModule);
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部高压模块量程
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetHighPressureModelRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            iResponse<PressureRange> result = APC2.GetControlPressureModelRange(PressureModel.InterHighPressure);
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部低压模块量程
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetLowPressureModelRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            iResponse<PressureRange> result = APC2.GetControlPressureModelRange(PressureModel.InterLowPressure);
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取内部模块量程上限
        /// </summary>
        /// <param name="pressure">压力上限</param>
        /// <returns></returns>
        public bool GetPressureUpper_IPM(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<Pressure> result = APC2.GetPressureUpper_IPM();
            if (result.IsCorrect)
            {
                double value = GetSmallerValue(result.Result.Value);

                pressure.Value = value;
                pressure.Unit = result.Result.Unit;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 读取内部模块的量程下限
        /// </summary>
        /// <param name="pressure">压力下限</param>
        /// <returns></returns>
        public bool GetPressureLowerer_IPM(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<Pressure> result = APC2.GetPressureLowerer_IPM();
            if (result.IsCorrect)
            {
                double value = GetSmallerValue(result.Result.Value);

                pressure.Value = value;
                pressure.Unit = result.Result.Unit;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置内部压力模块单位为kPa
        /// </summary>
        /// <returns></returns>
        public bool SetPressureUnit_IPM()
        {
            iResponse setInnerModulePressureUnit = APC2.SetPressureModelUnit(PressureModel.ControllerModule, PressureUnit.kPa);
            return setInnerModulePressureUnit.IsCorrect;
        }
        /// <summary>
        /// 设置内部压力模块单位为MPa
        /// </summary>
        /// <returns></returns>
        public bool SetPressureUnit_MPa()
        {
            iResponse setInnerModulePressureUnit = APC2.SetPressureModelUnit(PressureModel.ControllerModule, PressureUnit.MPa);
            return setInnerModulePressureUnit.IsCorrect;
        }
        /// <summary>
        /// 设置目标压力值
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool SetTargetPressure(double pressure)
        {
            iResponse setInnerModulePressureUnit = APC2.SetTargetPressureValue(pressure);
            return setInnerModulePressureUnit.IsCorrect;
        }
        /// <summary>
        /// 设置目标压力值
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool SetTargetPressure(Pressure pressure)
        {
            iResponse setInnerModulePressureUnit = APC2.SetTargetPressureValue(pressure);
            //return setInnerModulePressureUnit.IsCorrect;

            if (Math.Abs(pressure.Value) > 0.1)
            {//增加判断压力波动的逻辑
                Thread.Sleep(1000);
                Pressure oldPressure = new Pressure(0, PressureUnit.kPa);

                for (int i = 0; i < 3; i++)
                {
                    APC2.SetTargetPressureValue(pressure);

                    Thread.Sleep(1000);

                    if (GetPressure_IPM(out Pressure newPressure))
                    {
                        if (Math.Abs(newPressure.Value - oldPressure.Value) > 0.1)
                        {
                            break;
                        }
                        else
                        {
                            oldPressure.Value = newPressure.Value;
                        }
                    }
                }
            }
            else
            {
                return setInnerModulePressureUnit.IsCorrect;
            }

            return true;
        }
        /// <summary>
        ///  读取目标压力值(返回为目标值、压力单位、压力类型)
        /// </summary>
        /// <param name="pressure"></param>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetTargetPressure(out Pressure pressure, out PressureType pressureType)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            pressureType = PressureType.G;
            iResponse<RoundedPressure> result = APC2.GetTargetPressure();
            if (result.IsCorrect)
            {
                pressure = result.Result.pressure;
                pressureType = result.Result.pressureType;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置控制模块稳定误差和稳定时间
        /// </summary>
        /// <param name="stability">稳定误差</param>
        /// <returns></returns>
        public bool SetPressureStability(double stability)
        {
            //波动度初始默认0.005 * 0.01 * FS，其中0.005可以通过518指令更新（0.003~1）、控制状态下波动时间5s

            iResponse result = APC2.SetPressureModelStableParam(1, stability, 5);
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置控制模块稳定误差和稳定时间
        /// </summary>
        /// <param name="stability">稳定误差</param>
        /// <param name="time">稳定时间</param>
        /// <returns></returns>
        public bool SetPressureStability(double stability, int time)
        {
            iResponse result = APC2.SetPressureModelStableParam(1, stability, time);
            return result.IsCorrect;
        }

        /// <summary>
        /// 设定排空压力并输出
        /// </summary>
        /// <param name="setInnerPressure"></param>
        /// <returns></returns>
        public bool SetVentPressure(Xmas11.Domain.Mechanics.Pressure ventPressure)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse setVentPressure = APC2.SetVentPressure(ventPressure);
            return setVentPressure.IsCorrect;
        }
        /// <summary>
        /// 获取控制模块的稳定状态
        /// </summary>
        /// <param name="state">控制模块的稳定状态</param>
        /// <returns></returns>
        public bool GetPressureStableState(out PressureStableState state)
        {
            state = PressureStableState.UnKnown;
            iResponse<PressureStableState> result = APC2.GetPressureModelStableState(1);
            if (result.IsCorrect)
            {
                state = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置压力控制速率
        /// </summary>
        /// <param name="pressureRate">目标压力控制速率</param>
        /// <returns></returns>
        public bool SetPressureRate(double pressureRate)
        {
            iResponse result = APC2.SetPressureControlRate(pressureRate);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取压力控制速率范围
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetPressureRateRange(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            iResponse<PressureRange> result = APC2.GetPressureControlRateRange();
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取控压速率上限
        /// </summary>
        /// <param name="pressureRateUpper"></param>
        /// <returns></returns>
        public bool GetPressureRateUpper(out Xmas11.Domain.Mechanics.Pressure pressureRateUpper)
        {
            pressureRateUpper = new Pressure(10, PressureUnit.kPa);
            PressureRange pressureRange = new PressureRange();
            if (GetPressureRateRange(out pressureRange))
            {
                pressureRateUpper = pressureRange.Upper;
            }
            else
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 读取当前控制模块
        /// </summary>
        /// <returns></returns>
        public bool GetControlPressureModel(out string pressureModel)
        {
            pressureModel = string.Empty;
            iResponse<string> result = APC2.GetControlPressureModel();
            if (result.IsCorrect)
            {
                pressureModel = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 切换控制模块高低压量程
        /// </summary>
        /// <param name="mode">控制模块</param>
        /// <returns></returns>
        public bool SetControlPressureModel(PressureSwitchTripType mode)
        {
            if (mode == PressureSwitchTripType.High)
            {
                return APC2.SetControlPressureModel_Q(PressureModel.InterHighPressure).IsCorrect;
            }
            else if (mode == PressureSwitchTripType.Low)
            {
                return APC2.SetControlPressureModel_Q(PressureModel.InterLowPressure).IsCorrect;
            }
            return false;
        }

        /// <summary>
        /// 切换控制模块高低压量程
        /// </summary>
        /// <param name="mode">控制模块</param>
        /// <returns></returns>
        public bool SetControlPressureModel(PressureSwitchTripType mode, int index)
        {
            if (mode == PressureSwitchTripType.High)
            {
                return SetControlPressureModelToH(index);
            }
            else if (mode == PressureSwitchTripType.Low)
            {
                return SetControlPressureModelToL(index);
            }
            return false;
        }
        /// <summary>
        ///  切换控制模块高量程
        /// </summary>
        /// <returns></returns>
        public bool SetControlPressureModelToH(int index)
        {
            return APC2.SetControlPressureModel(PressureModel.InterHighPressure, index).IsCorrect;
        }
        /// <summary>
        ///  切换控制模块低量程
        /// </summary>
        /// <returns></returns>
        public bool SetControlPressureModelToL(int index)
        {
            return APC2.SetControlPressureModel(PressureModel.InterLowPressure, index).IsCorrect;
        }

        /// <summary>
        ///  切换控制模块高量程
        /// </summary>
        /// <returns></returns>
        public bool SetControlPressureModelToH()
        {
            return APC2.SetControlPressureModel_Q(PressureModel.InterHighPressure).IsCorrect;
        }
        /// <summary>
        ///  切换控制模块低量程
        /// </summary>
        /// <returns></returns>
        public bool SetControlPressureModelToL()
        {
            return APC2.SetControlPressureModel_Q(PressureModel.InterLowPressure).IsCorrect;
        }


        /// <summary>
        /// 获取控制器量程列表
        /// </summary>
        /// <param name="rangeInfo"></param>
        /// <returns></returns>
        public bool GetControllerRanges(PressureModel model, string id, out List<PressureModuleInfo> ModuleInfo)
        {
            ModuleInfo = null;
            iResponse<List<PressureModuleInfo>> result = APC2.GetControllerRanges(model, id);
            if (result.IsCorrect)
            {
                if (result.Result.Count == 0)
                {
                    return false;
                }
                ModuleInfo = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置压力排空状态
        /// </summary>
        /// <returns></returns>
        public bool SetVentMode()
        {
            if (!IsOpen)
                return false;
            iResponse setVentMode = APC2.SetPressureControlMode(DevicePressureControlMode.VENT);
            return setVentMode.IsCorrect;
        }
        /// <summary>
        /// 设置压力测试状态
        /// </summary>
        /// <returns></returns>
        public bool SetTestMode()
        {
            if (!IsOpen)
                return false;
            iResponse setTestMode = APC2.SetPressureControlMode(DevicePressureControlMode.MEASURE);
            return setTestMode.IsCorrect;
        }
        public ScriptHelperKVP SetTestMode_KVP()
        {
            bool success = SetTestMode();
            return new ScriptHelperKVP("811A设置压力测试状态", success);
        }
        /// <summary>
        /// 设置压力控制状态
        /// </summary>
        /// <returns></returns>
        public bool SetControlMode()
        {
            if (!IsOpen)
                return false;
            iResponse setControlMode = APC2.SetPressureControlMode(DevicePressureControlMode.CONTROL);
            return setControlMode.IsCorrect;
        }
        /// <summary>
        /// 设置气源端排空
        /// </summary>
        /// <returns></returns>
        public bool SetSupplyVentMode()
        {
            if (!IsOpen)
                return false;
            iResponse setControlMode = APC2.SetPressureControlMode(DevicePressureControlMode.EXHAUST);
            return setControlMode.IsCorrect;
        }
        /// <summary>
        /// 设置是否开启最大控压速率
        /// </summary>
        /// <param name="isOpen">0禁用 1启用</param>
        /// <returns></returns>
        public bool SetOpenMaxControlPressureSpeed(bool isOpen)
        {
            if (!IsOpen)
                return false;
            iResponse setControlMode = APC2.SetOpenMaxControlPressureSpeed(isOpen);
            return setControlMode.IsCorrect;
        }
        /// <summary>
        /// 获取是否开启最大控压速率
        /// </summary>
        /// <param name="state">0禁用 1启用</param>
        /// <returns></returns>
        public bool GetOpenMaxControlPressureSpeed(out bool state)
        {
            state = false;
            iResponse<bool> result = APC2.GetOpenMaxControlPressureSpeed();
            if (result.IsCorrect)
            {
                state = result.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置全部压力类型
        /// </summary>
        /// <param name="type">0=G;1=A;2=D</param>
        /// <returns></returns>
        public bool SetPressureModelPressureType(int type)
        {
            return APC2.SetPressureModelPressureType(PressureModel.ControllerModule, (PressureType)type).IsCorrect;
        }
        /// <summary>
        /// 设置自动清零
        /// </summary>
        /// <param name="isOpen"></param>
        /// <returns></returns>
        public bool SetClearStateOfVent(bool isOpen)
        {
            if (isOpen)
            {
                return APC2.SetClearStateOfVent(OpenCloseState.Open).IsCorrect;
            }
            else
            {
                return APC2.SetClearStateOfVent(OpenCloseState.Close).IsCorrect;
            }
        }
        /// <summary>
        /// 设置气柱头校正
        /// </summary>
        /// <param name="isOpen"></param>
        /// <returns></returns>
        public bool SetAirStigmaState(bool isOpen)
        {
            if (isOpen)
            {
                return APC2.SetAirStigmaState(OpenCloseState.Open).IsCorrect;
            }
            else
            {
                return APC2.SetAirStigmaState(OpenCloseState.Close).IsCorrect;
            }
        }
        /// <summary>
        /// 关闭(-100~10000)kPa设定点限制
        /// </summary>
        /// <returns></returns>
        public bool CloseSetPointLimitPressureRange()
        {
            if (APC2.SetSetPointLimitPressureRange(-100, 10000).IsCorrect && APC2.SetSetPointLimitState(OpenCloseState.Close).IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭设定点限制
        /// </summary>
        /// <returns></returns>
        public bool CloseSetPointLimit()
        {
            if (APC2.SetSetPointLimitState(OpenCloseState.Close).IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region 温度
        /// <summary>
        /// 获取设备内部所有温度
        /// 高压模块，低压模块，泵，电测板
        /// </summary>
        /// <param name="controlMode"></param>
        /// <returns></returns>
        public bool GetDev_T(out string Temperature)
        {
            Temperature = "";
            var Ptemperature = new InterPressureModuleInfo();
            iResponse<InterPressureModuleInfo> getInternalModuleInfo = APC2.GetInterPressureModuleInfo();
            if (getInternalModuleInfo.IsCorrect)
            {
                Ptemperature = getInternalModuleInfo.Result;
                Temperature = $"{Ptemperature.HighModuleTemperature},{Ptemperature.LowModuleTemperature}";
            }
            else
                return false;

            iResponse<double> getPumpTemperature = APC2.GetPumpTemperature();
            if (getPumpTemperature.IsCorrect)
            {
                Temperature += $",{getPumpTemperature.Result}";
            }
            else
                return false;

            iResponse<double> ET = APC2.GetEtlTemperature();
            if (ET.IsCorrect)
            {
                Temperature += $",{ET.Result}^";
            }
            else
                return false;

            return true;
        }
        #endregion

        #region 泵相关
        /// <summary>
        /// 获取泵温度
        /// </summary>
        /// <param name="controlMode"></param>
        /// <returns></returns>
        public bool GetMotor_Temperature(out double pumpTemperature)
        {
            Xmas11.Comm.Devices.iResponse<double> getPumpTemperature = APC2.GetPumpTemperature();
            if (getPumpTemperature.IsCorrect)
            {
                pumpTemperature = getPumpTemperature.Result;
                return true;
            }
            pumpTemperature = double.NaN;
            return false;
        }
        public ScriptHelperKVP GetMotor_Temperature_KVP(out double pumpTemperature)
        {
            bool success = GetMotor_Temperature(out pumpTemperature);
            string tempDisplay = success ? pumpTemperature.ToString("F2") : "获取失败";
            return new ScriptHelperKVP($"811A获取泵温度:{tempDisplay}", success);
        }
        /// <summary>
        /// 读取电池的电压和充放电电流
        /// </summary>
        /// <returns></returns>
        public bool GetBATTery(out double[] arr)
        {
            var result = APC2.GetBATTery();
            if (result.IsCorrect)
            {
                arr = result.Result;
                return true;
            }
            arr = null;
            return false;
        }

        /// <summary>
        /// 获取泵电流
        /// </summary>
        /// <param name="pumpCurrent"></param>
        /// <returns></returns>
        public bool GetPumpCurrent(out double pumpCurrent)
        {
            Xmas11.Comm.Devices.iResponse<double> getPumpCurrent = APC2.GetPumpCurrent();
            if (getPumpCurrent.IsCorrect)
            {
                pumpCurrent = getPumpCurrent.Result;
                return true;
            }
            pumpCurrent = double.NaN;
            return false;
        }
        /// <summary>
        /// 获取泵状态
        /// </summary>
        /// <param name="pumpOpenCloseState"></param>
        /// <returns></returns>
        public bool GetPumpState(out OpenCloseState pumpOpenCloseState)
        {
            Xmas11.Comm.Devices.iResponse<OpenCloseState> getPumpOpenCloseState = APC2.GetPumpOpenCloseState();
            if (getPumpOpenCloseState.IsCorrect)
            {
                pumpOpenCloseState = getPumpOpenCloseState.Result;
                return true;
            }
            pumpOpenCloseState = OpenCloseState.UnKnown;
            return false;
        }
        /// <summary>
        /// 获取泵真空能力
        /// </summary>
        /// <param name="standValue">标准值</param>
        /// <param name="currentValue">实际值</param>
        /// <returns></returns>
        public bool GetControlPanelModelParameter(out double standValue, out double currentValue)
        {
            standValue = double.NaN;
            currentValue = double.NaN;
            //1.获取机型,匹配泵真空能力
            double value = double.NaN;
            double value_DP = 0.95;//差压版本
            double value_BP = 0.905;//大气压版本
            double value_LLP = 0.2;//微差压版本
            double value_all = 0.905;

            iResponse<string> result = APC2.GetDevType();
            if (result.IsCorrect)
            {
                string devType = result.Result.Split(',')[0].Trim();
                switch (devType)
                {
                    case "ConST811A-D":
                        value = value_DP;
                        break;
                    case "ADT761A-D":
                        value = value_DP;
                        break;
                    case "ConST811A-LLP":
                        value = value_LLP;
                        break;
                    case "ADT761A-LLP":
                        value = value_LLP;
                        break;
                    case "ConST811A-BP":
                        value = value_BP;
                        break;
                    case "ADT761A-BP":
                        value = value_BP;
                        break;
                    case "ConST811A-UP":
                        if (result.Result.Contains("CP100") || result.Result.Contains("CP250") || result.Result.Contains("CP600"))
                        {
                            value = value_DP;
                        }
                        else
                        {
                            value = value_all;
                        }
                        break;
                    case "ADT761A-UP":
                        if (result.Result.Contains("CP100") || result.Result.Contains("CP250") || result.Result.Contains("CP600"))
                        {
                            value = value_DP;
                        }
                        else
                        {
                            value = value_all;
                        }
                        break;
                    default:
                        value = value_all;
                        break;
                }
                standValue = value;
            }
            else
            {
                return false;
            }
            //iResponse<string> result1 = APC2.GetSerialNumber();
            //if (result1.IsCorrect)
            //{
            //    if(result1.Result.StartsWith("811AG") || result1.Result.StartsWith("811AAM") || result1.Result.StartsWith("811AB"))
            //    {
            //        value = value_all;
            //    }
            //    else if(result1.Result.StartsWith("811AD") || result1.Result.StartsWith("811AAL"))
            //    {
            //        value = value_DP;
            //    }
            //    else if(result1.Result.StartsWith("811AL"))
            //    {
            //        value = value_LLP;
            //    }
            //    else
            //    {
            //        value = value_all;
            //    }

            //    standValue = value;
            //}
            //else
            //{
            //    return false;
            //}
            //2.获取当前泵真空能力
            iResponse<double> result2 = APC2.GetControlPanelModelParameter();
            if (result2.IsCorrect)
            {
                currentValue = result2.Result;
                return true;
            }
            else
            {
                return false;
            }
        }
        public ScriptHelperKVP GetCPSOnlineState(out OnOFFLineState isOnline)
        {
            isOnline = OnOFFLineState.UnKnown;
            var res= APC2.GetCPSOnlineState();
            if (res.IsCorrect)
            {
                isOnline = res.Result;
            }
            return new ScriptHelperKVP("CPS模块在线状态"+isOnline,res.IsCorrect);
        }
        public ScriptHelperKVP GetIsSupportCPS(out bool isSupport)
        {
            isSupport = false;
            var res = APC2.GetIsSupportCPS();
            if (res.IsCorrect)
            {
                isSupport = res.Result;
            }
            return new ScriptHelperKVP("CPS模块支持状态"+isSupport,res.IsCorrect);
        }
        /// <summary>
        /// 设置泵真空能力
        /// </summary>
        /// <returns></returns>
        public bool SetControlPanelModelParameter(out double Value)
        {
            //1.获取机型,匹配泵真空能力
            double value = double.NaN;
            double value_DP = 0.95;
            double value_BP = 0.95;
            double value_LLP = 0.2;
            double value_all = 0.905;
            iResponse<string> result = APC2.GetDevType();
            if (result.IsCorrect)
            {
                string devType = result.Result.Split(',')[0].Trim();
                switch (devType)
                {
                    case "ConST811A-D":
                        value = value_DP;
                        break;
                    case "ADT761A-D":
                        value = value_DP;
                        break;
                    case "ConST811A-LLP":
                        value = value_LLP;
                        break;
                    case "ADT761A-LLP":
                        value = value_LLP;
                        break;
                    case "ConST811A-BP":
                        value = value_BP;
                        break;
                    case "ADT761A-BP":
                        value = value_BP;
                        break;
                    default:
                        value = value_all;
                        break;
                }
            }
            else
            {
                Value = value;
                return false;
            }
            //2.根据机型写泵真空能力
            Value = value;
            return APC2.SetControlPanelModelParameter(value).IsCorrect;
        }
        /// <summary>
        /// 设置泵正压能力
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public bool SetPositiveModelParameter(out double Value, string DevType)
        {
            //1.获取机型匹配正压能力
            double value = double.NaN;
            double value_DP = 850;
            double value_BP = 130;
            double value_LLP = 125.5;
            double value_HP = 10600;
            double value_all = 7610;
            iResponse<string> result = APC2.GetDevType();
            if (result.IsCorrect)
            {
                string devType = result.Result.Split(',')[0].Trim();

                if (DevType.Contains("D"))
                {
                    value = value_DP;
                }
                else if (DevType.Contains("LP"))
                {
                    value = value_LLP;
                }
                else if (DevType.Contains("BP"))
                {
                    value = value_BP;
                }
                else if (DevType.Contains("HP"))
                {
                    value = value_HP;
                }
                else
                {
                    value = value_all;
                }
            }
            else
            {
                Value = value;
                return false;
            }
            //2.根据机型写泵正压能力
            Value = value;
            return APC2.SetControlFeatureConfig("PumpHighAbility", value.ToString()).IsCorrect;
        }

        /// <summary>
        /// 打开支持压力切换
        /// </summary>
        /// <returns></returns>
        public bool SetSupportPressureType(bool isSupport = true)
        {
            return APC2.SetControlFeatureConfig("SupportPTypeChange", Convert.ToInt32(isSupport).ToString()).IsCorrect;
        }

        /// <summary>
        /// 设置是否支持压力切换
        /// </summary>
        /// <returns></returns>
        public bool SetChangePressType()
        {
            //1.获取机型确定是否支持切换压力
            bool isSupport = true;
            iResponse<string> result = APC2.GetDevType();
            if (result.IsCorrect)
            {
                string devType = result.Result.Split(',')[0].Trim();
                switch (devType)
                {
                    case "ConST811A-D":
                        isSupport = false;
                        break;
                    case "ADT761A-D":
                        isSupport = false;
                        break;
                    case "ConST811A-LLP":
                        isSupport = false;
                        break;
                    case "ADT761A-LLP":
                        isSupport = false;
                        break;
                    case "ConST811A-BP":
                        isSupport = false;
                        break;
                    case "ADT761A-BP":
                        isSupport = false;
                        break;
                    default:
                        isSupport = true;
                        break;
                }
            }
            //2.设置配置文件
            return APC2.SetControlFeatureConfig("SupportPTypeChange", Convert.ToInt32(isSupport).ToString()).IsCorrect;
        }

        /// <summary>
        /// 设置Feature配置文件
        /// </summary>
        /// <param name="node"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetControlFeatureConfig(string node, string value)
        {
            return APC2.SetControlFeatureConfig(node, value).IsCorrect;
        }

        /// <summary>
        /// 获得当前电量
        /// </summary>
        /// <param name="batteryValue"></param>
        /// <returns></returns>
        public bool GetBatteryValue(out double batteryValue)
        {
            var result = APC2.GetBatteryValue();
            batteryValue = 0.0;
            if (result.IsCorrect)
            {
                string[] arr = result.Result.Replace("mAh", "").Split('/');

                if (arr.Length == 2 && double.TryParse(arr[0], out double curValue) && double.TryParse(arr[1], out double allValue))
                {
                    batteryValue = Math.Round(curValue / allValue, 2);
                    return true;
                }
            }
            return false;
        }
        public ScriptHelperKVP GetBatteryValue_KVP(out double batteryValue)
        {
            var result = APC2.GetBatteryValue();
            batteryValue = 0.0;

            if (result.IsCorrect)
            {
                string[] arr = result.Result.Replace("mAh", "").Split('/');

                if (arr.Length == 2 && double.TryParse(arr[0], out double curValue) && double.TryParse(arr[1], out double allValue))
                {
                    batteryValue = Math.Round(curValue / allValue, 2);
                    string percentDisplay = (batteryValue * 100).ToString("F0") + "%";
                    return new ScriptHelperKVP($"811A获得当前电量:{percentDisplay}", true);
                }
            }

            return new ScriptHelperKVP("811A获得当前电量:获取失败", false);
        }
        public bool GetBatteryValueAll(out double curvalue, out double allvalue)
        {
            var result = APC2.GetBatteryValue();
            curvalue = 0.0;
            allvalue = 0.0;
            if (result.IsCorrect)
            {
                string[] arr = result.Result.Replace("mAh", "").Split('/');

                if (arr.Length == 2)
                {
                    if (double.TryParse(arr[0], out double cur) && double.TryParse(arr[1], out double all))
                    {
                        allvalue=  all;
                        curvalue = cur;
                    }
                }
            }
            return false;
        }
        #endregion


        #region 自整定
        /// <summary>
        /// 自整定
        /// </summary>
        /// <returns></returns>
        public bool SelfTuning()
        {
            iResponse result = APC2.SelfTuning();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public ScriptHelperKVP SelfTuningSwitch (OpenCloseState ocs)
        {
            return new ScriptHelperKVP("设置811A自整定状态为"+ocs.ToString(), ocs==OpenCloseState.Open?SelfTuning():StopSelfTuning());
        }
        /// <summary>
        /// 停止自整定
        /// </summary>
        /// <returns></returns>
        public bool StopSelfTuning()
        {
            iResponse result = APC2.StopSelfTuning();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 自整定状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetSelfTuningState(out SelfTuningData state)
        {
            iResponse<SelfTuningData> result = APC2.GetSelfTuningState();
            if (result.IsCorrect)
            {
                state = result.Result;
                return true;
            }
            state = new SelfTuningData();
            return false;
        }
        public ScriptHelperKVP GetSelfTuningState_KVP(out SelfTuningData state)
        {
            iResponse<SelfTuningData> result = APC2.GetSelfTuningState();
            bool isCorrect = result.IsCorrect;
            string stateDisplay = "";
            if (isCorrect)
            {
                state = result.Result;
                if (state.ResultType==SelfTuningTestType.InProgress)
                {
                    stateDisplay += string.Format(":自整定状态{0}%  ;\r\n 设定点:{1}  ;\r\n进气阀控制量:{2}  ;\r\n放气阀控制量:{3}\r\n", state.ProcessValue, state.SetPoint, state.IntakeValveControls, state.OuttakeValveControls);
                }
            }
            else
            {
                state = new SelfTuningData();
            }
            stateDisplay = (isCorrect ? state.ToString() : "获取失败") + stateDisplay;
            return new ScriptHelperKVP($"811A自整定状态:{stateDisplay}", isCorrect);
        }        
        #endregion 自整定


        #region 进气传感器校准
        /// <summary>
        /// 进气传感器校准
        /// </summary>
        /// <returns></returns>
        public bool CalibrationSensor()
        {
            iResponse result = APC2.CalibrationSensor();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 停止进气传感器校准
        /// </summary>
        /// <returns></returns>
        public bool StopCalibrationSensor()
        {
            iResponse result = APC2.StopCalibrationSensor();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 进气传感器校准状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetCalibrationSensorState(out IntakeSensorCalibrationData state)
        {
            iResponse<IntakeSensorCalibrationData> result = APC2.GetCalibrationSensorState();
            if (result.IsCorrect)
            {
                state = result.Result;
                return true;
            }
            state = new IntakeSensorCalibrationData();
            return false;
        }
        #endregion 进气传感器校准



        #region 电源
        /// <summary>
        /// 电源测试
        /// </summary>
        /// <param name="powerType"></param>
        /// <returns></returns>
        public bool GetPowerSupplyCheck(out PowerType powerType)
        {
            Xmas11.Comm.Devices.iResponse<PowerType> getPowerSupplyCheck = APC2.GetPowerSupplyCheck();
            if (getPowerSupplyCheck.IsCorrect)
            {
                powerType = getPowerSupplyCheck.Result;
                return true;
            }
            powerType = PowerType.Unknow;
            return false;
        }
        public bool GetEnergyCheckStata(out List<double> energyCheckStata)
        {

            energyCheckStata = new List<double>();
            Xmas11.Comm.Devices.iResponse<List<double>> getEnergyCheckStata = APC2.GetEnergyCheckStata();
            if (getEnergyCheckStata.IsCorrect)
            {
                energyCheckStata = getEnergyCheckStata.Result;
                return true;
            }
            return false;
        }
        #endregion


        #region 语言相关
        /// <summary>
        /// 获取当前语言
        /// </summary>
        /// <param name="languageName"></param>
        /// <returns></returns>
        public bool GetCurrentLanguange(out string languageName)
        {
            Xmas11.Comm.Devices.iResponse<string> getLanguage = APC2.GetLanguage();
            if (getLanguage.IsCorrect)
            {
                languageName = getLanguage.Result;
                return true;
            }
            languageName = string.Empty;
            return false;
        }
        /// <summary>
        /// 设置开机语言
        /// </summary>
        /// <param name="languageName">语言名称</param>
        /// <param name="restart">是否重启</param>
        /// <returns></returns>
        public bool SetCurrentLanguange(string languageName, bool restart)
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.SetLanguage(languageName, restart);
            return testPump.IsCorrect;
        }

        /// <summary>
        /// 获取当前语言列表
        /// </summary>
        /// <param name="languageName"></param>
        /// <returns></returns>
        public bool GetCurrentLanguangeList(out string languageName)
        {
            Xmas11.Comm.Devices.iResponse<string> getLanguage = APC2.GetLanguageList();
            if (getLanguage.IsCorrect)
            {
                languageName = getLanguage.Result;
                return true;
            }
            languageName = string.Empty;
            return false;
        }

        /// <summary>
        /// 设置语言列表
        /// </summary>
        /// <returns></returns>
        public bool SetLanguangeList()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse testPump = APC2.SetLanguageList();
            return testPump.IsCorrect;
        }
        #endregion


        #region 开机LOGO相关
        /// <summary>
        /// 获取LOGO列表
        /// </summary>
        /// <param name="languageName"></param>
        /// <returns></returns>
        public bool GetAllLogoImage(out List<string> languageName)
        {
            Xmas11.Comm.Devices.iResponse<List<string>> getAllLogoImage = APC2.GetAllLogoImage();
            if (getAllLogoImage.IsCorrect)
            {
                languageName = getAllLogoImage.Result;
                return true;
            }
            languageName = null;
            return false;
        }
        /// <summary>
        /// 设置开机LOGO为ConST
        /// </summary>
        /// <returns></returns>
        public bool SetConSTLOGO()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse set = APC2.SetDeviceBootLogo(CompanyName.ConST);
            return set.IsCorrect;
        }
        /// <summary>
        /// 设置开机LOGO为Additel
        /// </summary>
        /// <returns></returns>
        public bool SetAdditelLOGO()
        {
            if (!IsOpen)
                return false;
            Xmas11.Comm.Devices.iResponse set = APC2.SetDeviceBootLogo(CompanyName.Additel);
            return set.IsCorrect;
        }
        #endregion


        #region 高低压模块

        #region 模块编号
        /// <summary>
        /// 获取高压模块编号
        /// </summary>
        /// <param name="SN">高压模块编号</param>
        /// <returns></returns>
        public bool GetHPMCode(out string SN)
        {
            dynamic cdps = null;
            SN = "";
            if (APC2.FW_HPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_HPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.DPSEXBase);
            if (cdps == null)
            {
                return false;
            }
            Xmas11.Comm.Devices.iResponse<string> getCode = cdps.GetCode();
            if (!getCode.IsCorrect)
            {
                SN = string.Empty;
                return false;
            }
            SN = getCode.Result;
            return true;
        }
        /// <summary>
        /// 获取低压模块编号
        /// </summary>
        /// <param name="SN">低压模块编号</param>
        /// <returns></returns>
        public bool GetLPMCode(out string SN)
        {
            dynamic cdps = null;
            SN = "";
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            if (cdps == null)
            {
                return false;
            }
            Xmas11.Comm.Devices.iResponse<string> getCode = cdps.GetCode();
            if (!getCode.IsCorrect)
            {
                SN = string.Empty;
                return false;
            }
            SN = getCode.Result;
            return true;
        }
        #endregion

        #region 模块激励值
        /// <summary>
        /// 获取高压模块传感器激励值
        /// </summary>
        /// <param name="pv"></param>
        /// <returns></returns>
        public bool GetHPMSensorPowerSupplyValue(out double pv)
        {
            pv = double.NaN;
            dynamic cdps = null;
            if (APC2.FW_HPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_HPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_HPM as Xmas11.Comm.Devices.DPSEXBase);
            if (cdps == null)
            {
                return false;
            }
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            pv = result.Result;
            return true;
        }
        /// <summary>
        /// 获取低压模块传感器激励值
        /// </summary>
        /// <param name="pv"></param>
        /// <returns></returns>
        public bool GetLPMSensorPowerSupplyValue(out double pv)
        {
            pv = double.NaN;
            dynamic cdps = null;
            if (APC2.FW_LPM is Xmas11.Comm.Devices.CDP)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.CDP);
            else if (APC2.FW_LPM is Xmas11.Comm.Devices.DPSEXBase)
                cdps = (APC2.FW_LPM as Xmas11.Comm.Devices.DPSEXBase);
            if (cdps == null)
            {
                return false;
            }
            iResponse<double> result = cdps.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            pv = result.Result;
            return true;
        }
        #endregion


        #region 初始化高低压模块校准日期

        public bool SetModelCalibrationDate(PressureSwitchTripType type, string date)
        {
            if (!IsOpen)
                return false;
            PressureModel Ptype = PressureModel.ControllerModule;
            if (type == PressureSwitchTripType.High)
            {
                Ptype = PressureModel.InterHighPressure;
            }
            else if (type == PressureSwitchTripType.Low)
            {
                Ptype = PressureModel.InterLowPressure;
            }

            Xmas11.Comm.Devices.iResponse set = APC2.SetModelCalibrationDate(Ptype, date);
            return set.IsCorrect;
        }
        public bool GetModelCalibrationDate(PressureSwitchTripType type, out string date)
        {
            PressureModel Ptype = PressureModel.ControllerModule;
            if (type == PressureSwitchTripType.High)
            {
                Ptype = PressureModel.InterHighPressure;
            }
            else if (type == PressureSwitchTripType.Low)
            {
                Ptype = PressureModel.InterLowPressure;
            }

            Xmas11.Comm.Devices.iResponse<string> set = APC2.GetModelCalibrationDate(Ptype);

            if (!set.IsCorrect)
            {
                date = string.Empty;
                return false;
            }
            date = set.Result;
            return true;
        }
        #endregion

        #endregion


        #region 私有方法
        /// <summary>
        /// 获取只保留一位小数位的值(不四舍五入)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static double GetSmallerValue(double data)
        {
            if (data > 0)
            {
                return Math.Floor(data * 10) / 10;
            }
            else
            {
                return Math.Ceiling(data * 10) / 10;
            }
        }
        #endregion


        #region 升级
        /// <summary>
        /// 是否可升级
        /// </summary>
        /// <returns></returns>
        public override bool IsUpgradable()
        {
            if (this.UpgradeSetting == null)
            {
                string path = UpgradeFile.LocalCacheRoot + @"/APC2/OS/UpgradeSetting.xml";
                this.LoadUpgradeSetting(path);
            }
            if (this.UpgradeInfo == null)
            {
                this.UpgradeInfo = new UpgradeInfo();
            }

            if (this.UpgradeSetting == null)
            {
                return false;
            }
            if (this.UpgradeInfo == null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 升级检查
        /// </summary>
        public override int UpgradeCheck()
        {
            int result = 0;
            this.UpgradeInfo.ClearUpgradeMsgs();
            if (this.CommConfig is EthernetConfig)
            {
                if (this.IsOpen)
                {
                    result = 0;
                }
                else
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail));
                    result = 2;
                }
            }
            else
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg1));
                result = 1;
            }
            return result;
        }
        /// <summary>
        ///  升级文件检查
        /// </summary>
        /// <returns></returns>
        public override int UpgradeFileCheck()
        {
            int result = UpgradeCheck();
            if (result == 0)
            {
                if (CheckUpgradeFile())
                {
                    UpgradeFile mainUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();
                    if (mainUpgradeFile != null && mainUpgradeFile.IsCached && mainUpgradeFile.IsAnalyzed)
                    {
                        RefreshUpdateVersion();
                        result = 0;
                    }
                    else
                    {
                        result = 4;
                    }
                }
                else
                {
                    result = 3;
                }
            }
            return result;
        }
        /// <summary>
        /// 获取升级信息
        /// </summary>
        /// <returns></returns>
        public override UpgradeInfo GetUpgradeInfo()
        {
            if (!this.IsOpen)
            {
                return UpgradeInfo;
            }
            RefreshMainInformation();
            RefreshCurrentVersion();
            RefreshUpdateVersion();
            this.Close();
            return UpgradeInfo;
        }
        public override UpgradeInfo RefreshMainInformation()
        {
            bool connected = this.IsConnected;
            if (!connected)
            {
                if (!this.IsOpen)
                {
                    return UpgradeInfo;
                }
            }
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;

            string code = "";
            GetSerialNumber(out code);
            this.DeveiceSN = code;
            MainInfo codeInfo = new MainInfo();
            codeInfo.Key = "Code";
            codeInfo.Name = Bots.TestBench.Device.Base.Properties.Resources.SerialNumber;
            codeInfo.Info = code;
            if (UpgradeInfo.MainInfoIsContains(codeInfo))
            {
                UpgradeInfo.MainInfoDic["Code"].Info = codeInfo.Info;
            }
            else
            {
                UpgradeInfo.AddMainInfo(codeInfo);
            }
            string type = "";
            GetDevType(out type);
            MainInfo typeInfo = new MainInfo();
            typeInfo.Key = "Type";
            typeInfo.Name = Bots.TestBench.Device.Base.Properties.Resources.Model;
            typeInfo.Info = type;
            if (UpgradeInfo.MainInfoIsContains(typeInfo))
            {
                UpgradeInfo.MainInfoDic["Type"].Info = typeInfo.Info;
            }
            else
            {
                UpgradeInfo.AddMainInfo(typeInfo);
            }
            this.UpgradeInfo.ProgressIsIndeterminate = false;
            this.UpgradeInfo.IsProgress = false;
            if (!connected)
            {
                this.Close();
            }
            return UpgradeInfo;
        }
        public override UpgradeInfo RefreshCurrentVersion()
        {
            bool connected = this.IsConnected;
            if (!connected)
            {
                if (!this.IsOpen)
                {
                    return UpgradeInfo;
                }
            }
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;

            string mainFirmware;
            if (GetVersion(out mainFirmware))
            {
                Upgrade.VersionInfo info = new Upgrade.VersionInfo();
                info.Key = "MainFirmware";
                info.Name = Bots.TestBench.Device.Base.Properties.Resources.HostVersion;
                info.CurrentVersion = mainFirmware;
                if (UpgradeInfo.VersionInfoIsContains(info))
                {
                    UpgradeInfo.VersionInfoDic["MainFirmware"].CurrentVersion = info.CurrentVersion;
                }
                else
                {
                    UpgradeInfo.AddVersionInfo(info);
                }
            }
            string ctlVersion;
            if (GetVersion_Controller(out ctlVersion))
            {
                Upgrade.VersionInfo info = new Upgrade.VersionInfo();
                info.Key = "ControlBoard";
                info.Name = Bots.TestBench.Device.Base.Properties.Resources.ControlBoard;
                info.CurrentVersion = ctlVersion;
                if (UpgradeInfo.VersionInfoIsContains(info))
                {
                    UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion = info.CurrentVersion;
                }
                else
                {
                    UpgradeInfo.AddVersionInfo(info);
                }
            }
            string eleVersion;
            if (GetVersion_Electricity(out eleVersion))
            {
                Upgrade.VersionInfo info = new Upgrade.VersionInfo();
                info.Key = "ElectricBoard";
                info.Name = Bots.TestBench.Device.Base.Properties.Resources.ElectricBoard;
                info.CurrentVersion = eleVersion;
                if (UpgradeInfo.VersionInfoIsContains(info))
                {
                    UpgradeInfo.VersionInfoDic["ElectricBoard"].CurrentVersion = info.CurrentVersion;
                }
                else
                {
                    UpgradeInfo.AddVersionInfo(info);
                }
            }
            this.UpgradeInfo.ProgressIsIndeterminate = false;
            this.UpgradeInfo.IsProgress = false;
            if (!connected)
            {
                this.Close();
            }
            return UpgradeInfo;
        }
        public override UpgradeInfo RefreshUpdateVersion()
        {
            bool connected = this.IsConnected;
            if (!connected)
            {
                if (!this.IsOpen)
                {
                    return UpgradeInfo;
                }
            }
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;
            if (CheckUpgradeFile())
            {
                UpgradeFile mainUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();
                if (mainUpgradeFile != null)
                {
                    if (UpgradeInfo.VersionInfoIsContains("MainFirmware"))
                    {
                        var host = mainUpgradeFile.Versions.Where(v => v.Key.Contains("HOST")).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(host))
                        {
                            UpgradeInfo.VersionInfoDic["MainFirmware"].UpgradeVersion = host;
                        }
                    }
                    if (UpgradeInfo.VersionInfoIsContains("ControlBoard"))
                    {

                        string VersionController = null;
                        if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("DP"))
                        {
                            VersionController = "DP";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("MP"))
                        {
                            VersionController = "MP";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("LLP"))
                        {
                            VersionController = "LLP";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("HPL"))
                        {
                            VersionController = "HPL";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("HP"))
                        {
                            VersionController = "HP";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("DIF"))
                        {
                            VersionController = "DIF";
                        }
                        else if (UpgradeInfo.VersionInfoDic["ControlBoard"].CurrentVersion.Contains("HIGH"))
                        {
                            VersionController = "HIGH";
                        }
                        else
                        {
                            VersionController = "BP";
                        }
                        var mc = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains(VersionController)).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(mc))
                        {
                            UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion = mc;
                        }
                    }
                    if (UpgradeInfo.VersionInfoIsContains("ElectricBoard"))
                    {
                        var me = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains("E")).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(me))
                        {
                            UpgradeInfo.VersionInfoDic["ElectricBoard"].UpgradeVersion = me;
                        }
                    }
                }
            }
            this.UpgradeInfo.ProgressIsIndeterminate = false;
            this.UpgradeInfo.IsProgress = false;
            if (!connected)
            {
                this.Close();
            }
            return UpgradeInfo;
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public override UpgradeInfo InitializationMainInformation()
        {
            bool connected = this.IsConnected;
            if (!connected)
            {
                if (!this.IsOpen)
                {
                    return UpgradeInfo;
                }
            }
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;

            if (UpgradeInfo.MainInfoIsContains("Code"))
            {
                SetSerialNumber(UpgradeInfo.MainInfoDic["Code"].Info);
            }
            if (UpgradeInfo.MainInfoIsContains("Type"))
            {
                SetPrimaryDevType(UpgradeInfo.MainInfoDic["Type"].Info);
            }
            this.UpgradeInfo.ProgressIsIndeterminate = false;
            this.UpgradeInfo.IsProgress = false;
            if (!connected)
            {
                this.Close();
            }
            return UpgradeInfo;
        }
        /// <summary>
        /// 升级
        /// </summary>
        public override UpgradeInfo Upgrade()
        {
            if (UpgradeFileCheck() > 0)
            {
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.IsProgress = false;
                IsUpgrading = false;
                if (RequestStopUpgrade)
                    RequestStopUpgrade = false;
                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                return UpgradeInfo;
            }
            if (RequestStopUpgrade)
                RequestStopUpgrade = false;
            IsUpgrading = true;
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;
            this.UpgradeInfo.UpgradeResult = UpgradeResult.None;
            DateTime logDateTime = DateTime.Now;
            try
            {
                this.SaveInUpgradingLog(logDateTime);
                EthernetConfig DeviceEthernetConfig = this.CommConfig as EthernetConfig;
                string deviceIP = DeviceEthernetConfig.IP;
                this.UpgradeInfo.ClearUpgradeMsgs();
                DateTime startDT = DateTime.Now;
                DateTime stopDT = DateTime.Now;
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade, startDT.ToString()));

                #region 1.检查启动文件中是否含P21.lnk，升级前需要删除
                string LNKPath = @"\FlashDisk\StartUp\P21.lnk";
                bool IsLNKExist = false;
                UpgradeMsg msg1 = new UpgradeMsg("P21.lnk", Bots.TestBench.Device.Base.Properties.Resources.UpgradeCheck);
                this.UpgradeInfo.AddUpgradeMsgs(msg1);
                if (QueryFileExists(LNKPath, out IsLNKExist))
                {
                    if (IsLNKExist)
                    {
                        msg1.Content = Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileExistent + "," + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileDelete + "," + Bots.TestBench.Device.Base.Properties.Resources.RestartBeginUpgrade;
                        if (!DeleteFile(LNKPath))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("P21.lnk", Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileDeleteFailed));
                            return UpgradeInfo;
                            ;
                        }
                        System.Threading.Thread.Sleep(1000);
                        SetReboot();
                        System.Threading.Thread.Sleep(1000);
                        this.Close();
                        System.Threading.Thread.Sleep(1000);
                        while (true)
                        {
                            if (DeviceEthernetConfig.Ping())
                            {
                                break;
                            }
                            System.Threading.Tasks.Task.Delay(2000).Wait();
                        }
                        while (true)
                        {
                            if (this.Open())
                            {
                                break;
                            }
                            System.Threading.Tasks.Task.Delay(2000).Wait();
                        }
                    }
                    else
                    {
                        msg1.Content = Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileNonExistent + "," + Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade;
                    }
                }
                #endregion
                if (RequestStopUpgrade)
                {
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("升级终止", stopDT.ToString()));
                    return UpgradeInfo;
                }
                #region 2.将升级包拷至设备
                string UpgradeFileName = string.Empty;
                UpgradeFile mainUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();
                if (mainUpgradeFile != null)
                {
                    if (File.Exists(mainUpgradeFile.CachePath))
                    {
                        //获取升级文件，去掉中文
                        System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex(@"[\u4e00-\u9fa5]");
                        UpgradeFileName = System.IO.Path.GetFileName(mainUpgradeFile.CachePath);
                        UpgradeFileName = reg.Replace(UpgradeFileName, "");
                        bool IsUpgradeFileExist = false;
                        if (QueryFileExists(UpgradeFileName, out IsUpgradeFileExist))
                        {
                            if (!IsUpgradeFileExist)
                            {

                                string targetFileURL = Bots.TestBench.Util.FTPHelper.CombineUriToString("ftp://" + deviceIP + "/", UpgradeFileName);
                                long fileByteCount;
                                if (Bots.TestBench.Util.FTPHelper.UploadFile("cst", "cst", targetFileURL, mainUpgradeFile.CachePath, out fileByteCount))
                                {
                                    double filesize2 = Math.Round(fileByteCount / 1024 / 1024.0, 2);
                                    int filesize = Convert.ToInt32(Math.Ceiling(filesize2));
                                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileSize + filesize2 + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileSizeUnitMB + "," + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileUploadComplete));
                                }
                                else
                                {
                                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeError));
                                }
                            }
                        }
                    }
                    else
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeError));
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeEnd, stopDT.ToString()));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                }
                else
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeError));
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeEnd, stopDT.ToString()));
                    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    return UpgradeInfo;
                }
                #endregion
                if (RequestStopUpgrade)
                {
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("升级终止", stopDT.ToString()));
                    return UpgradeInfo;
                }
                #region 3.发送软件升级指令进行升级
                if (!string.IsNullOrEmpty(UpgradeFileName))
                {
                    if (SoftwareUpgrade(UpgradeFileName))
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInProcess));
                    }
                    else
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInProcessError));
                    }
                    System.Threading.Tasks.Task.Delay(2 * 1000).Wait();
                }
                else
                {
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeEnd, stopDT.ToString()));
                    return UpgradeInfo;
                }
                #endregion
                #region 4.升级过程监视
                int i = 0;
                while (true)
                {
                    try
                    {
                        string v;
                        if (!GetVersion(out v))
                        {
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                    System.Threading.Tasks.Task.Delay(2 * 1000).Wait();
                    if (i > 5)
                    {
                        break;
                    }
                    i++;
                    if (RequestStopUpgrade)
                    {
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated2, stopDT.ToString()));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.UnKonw;
                        return UpgradeInfo;
                    }
                }
                i = 0;
                //判断是否已经开始升级
                System.Threading.Tasks.Task.Delay(2 * 1000).Wait();
                while (true)
                {
                    if (DeviceEthernetConfig.Ping())
                    {

                    }
                    else
                    {
                        this.Close();
                        break;
                    }
                    System.Threading.Tasks.Task.Delay(2 * 1000).Wait();
                    if (i > 5)
                    {
                        this.Close();
                        break;
                    }
                    i++;
                    if (RequestStopUpgrade)
                    {
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated2, stopDT.ToString()));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.UnKonw;
                        return UpgradeInfo;
                    }
                }
                i = 0;
                while (true)
                {
                    System.Threading.Tasks.Task.Delay(2 * 1000).Wait();
                    if (i > 15)
                    {
                        break;
                    }
                    i++;
                    if (RequestStopUpgrade)
                    {
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated2, stopDT.ToString()));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.UnKonw;
                        return UpgradeInfo;
                    }
                }
                System.Threading.Tasks.Task.Delay(2000).Wait();
                i = 0;
                while (true)
                {
                    if (DeviceEthernetConfig.Ping())
                    {
                        if (this.Open())
                        {
                            System.Threading.Tasks.Task.Delay(5000).Wait();
                            string v;
                            if (GetVersion(out v))
                            {
                                break;
                            }
                            else
                            {
                                this.Close();
                            }
                        }
                    }
                    System.Threading.Tasks.Task.Delay(2000).Wait();
                    if (i > 90)
                    {
                        break;
                    }
                    i++;
                    if (RequestStopUpgrade)
                    {
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated2, stopDT.ToString()));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.UnKonw;
                        return UpgradeInfo;
                    }
                }
                #endregion   
                if (i <= 90)
                {
                    this.RefreshCurrentVersion();
                    Version cv = VersionHelper.AnalysisVersion(UpgradeInfo.VersionInfoDic["MainFirmware"].CurrentVersion).Version;
                    Version uv = VersionHelper.AnalysisVersion(UpgradeInfo.VersionInfoDic["MainFirmware"].UpgradeVersion).Version;
                    //Version cv = new Version(UpgradeInfo.VersionInfoDic["MainFirmware"].CurrentVersion.Split(' ')[1]);
                    //Version uv = new Version(UpgradeInfo.VersionInfoDic["MainFirmware"].UpgradeVersion.Split('V')[1]);
                    if (cv == uv)
                    {
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Succeed;
                    }
                    else
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessFailMsgResultError));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    }
                }
                else
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessFailMsgTimeout));
                    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                }
                stopDT = DateTime.Now;
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeComplete, stopDT.ToString()));
                return UpgradeInfo;
            }
            catch
            {
                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                return UpgradeInfo;
            }
            finally
            {
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.IsProgress = false;
                this.GetUpgradeInfo();
                this.SaveUpgradedLog(logDateTime);
                this.Close();
                IsUpgrading = false;
                if (RequestStopUpgrade)
                    RequestStopUpgrade = false;
            }
        }

        /// <summary>
        /// 加载升级文件
        /// </summary>
        /// <returns></returns>
        public override void LoadUpgradeFile()
        {
            if (this.UpgradeSetting.UpgradeFiles != null && this.UpgradeSetting.UpgradeFiles.Count > 0)
            {
                this.UpgradeSetting.UpgradeFiles.AsParallel().ForAll(upgrade =>
                {
                    UpgradeFile upgradeFile = upgrade.GetFile();
                    if (upgradeFile != null)
                    {
                        lock (upgradeFile.FileLock)
                        {
                            if (upgradeFile.IsCached)
                            {
                                if (!upgradeFile.IsAnalyzed)
                                {
                                    if (upgradeFile.IsMain)
                                    {
                                        if (File.Exists(upgradeFile.CachePath))
                                        {
                                            upgradeFile.FileContent = System.IO.File.ReadAllBytes(upgradeFile.CachePath);
                                            #region 获取升级包主程序版本
                                            //1.解压升级包
                                            ZipHelper zipHelper = new ZipHelper();
                                            string extractFolderName = System.IO.Path.GetDirectoryName(upgradeFile.CachePath) + "\\apc2UpdatePack" + Guid.NewGuid().ToString();
                                            //如果存在先删除
                                            if (Directory.Exists(extractFolderName))
                                            {
                                                try
                                                {
                                                    Directory.Delete(extractFolderName, true);
                                                }
                                                catch
                                                {
                                                }
                                            }
                                            bool isExtract = zipHelper.Extract(upgradeFile.CachePath, "showmethemoney", extractFolderName);
                                            if (isExtract)
                                            {
                                                //2.查找解压文件中的ATC.exe，这里22年变了，液压气压合并，多了一层层级。1.0是气压，1.1是液压，TODO
                                                string appver = extractFolderName + "\\Application";
                    
                                                string[] files = null;
                                                if (Directory.Exists(appver + "\\1.1") || Directory.Exists(appver + "\\1.1"))
                                                {
                                                    //如果存在，就是新的包，合并后的。
                                                    if (DeveiceSN.StartsWith("811AH"))
                                                    {
                                                        //液压版
                                                        files = System.IO.Directory.GetFiles(appver + "\\1.1", "*.exe");
                                                    }
                                                    else
                                                    {
                                                        //气压版
                                                        files = System.IO.Directory.GetFiles(appver + "\\1.1", "*.exe");
                                                    }

                                                }
                                                else
                                                {
                                                    files = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Application"), "*.exe");
                                                }
                                                string apc_host = files.Where(f => f.Contains("APC.exe")).FirstOrDefault();
                                                if (!string.IsNullOrEmpty(apc_host))
                                                {
                                                    System.IO.FileInfo fileInfo = null;
                                                    try
                                                    {
                                                        fileInfo = new System.IO.FileInfo(apc_host);
                                                    }
                                                    catch { }
                                                    // 如果文件存在
                                                    if (fileInfo != null && fileInfo.Exists)
                                                    {
                                                        System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(apc_host);
                                                        string version = string.Format("APC2-HOST V{0}", info.ProductVersion);
                                                        upgradeFile.AddVersion("APC2-HOST", version);
                                                    }
                                                }
                                                #region 3.查找文件夹中控制板
                                                string[] fileController = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Controller"), "*.bin");
                                                foreach (string file in fileController)
                                                {
                                                    if (file.Contains("DIF"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-DP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-DP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-DP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("HIGH"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-MP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-MP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-MP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("LLP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-LLP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-LLP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-LLP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("BP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-BP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-BP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-BP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("DP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-DP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-DP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-DP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("HPL"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-HPL";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-HPL", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-HPL-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("HP") && !file.Contains("HPL"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-HP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-HP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-HP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("MP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "APC-MP";
                                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-MP", controllerVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-MP-Hardware", controllerHardwareVersion);
                                                    }
                                                }

                                                #endregion

                                                #region 4.查找文件夹中电测板

                                                string[] fileElectricity = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Electricity"), "*.bin");


                                                foreach (string file in fileElectricity)
                                                {
                                                    if (file.Contains("E"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyElectricityVersion = "APC-E";
                                                        int selectfirst = text.LastIndexOf(keyElectricityVersion);
                                                        for (int i = selectfirst + keyElectricityVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var electricityHardwareVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-E-Hardware", electricityHardwareVersion);
                                                        text = text.Substring(0, selectfirst);
                                                        selectfirst = text.LastIndexOf(keyElectricityVersion);
                                                        for (int i = selectfirst + keyElectricityVersion.Length + 2; i < text.Length; i++)
                                                        {
                                                            textsub = text.Substring(i, 1);
                                                            if (!(textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                                            {
                                                                substringlength = i - selectfirst;
                                                                break;
                                                            }
                                                        }
                                                        var electricityVersion = text.Substring(selectfirst, substringlength);
                                                        upgradeFile.AddVersion("APC-E", electricityVersion);
                                                    }
                                                }

                                                #endregion


                                                //5.删除解压的升级包
                                                if (Directory.Exists(extractFolderName))
                                                {
                                                    try
                                                    {
                                                        Directory.Delete(extractFolderName, true);
                                                    }
                                                    catch { }
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                    upgradeFile.IsAnalyzed = true;
                                }
                            }
                        }
                    }
                });
            }
        }
        /// <summary>
        /// 查询夹是否存在
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="isExists"></param>
        /// <returns></returns>
        public bool QueryDirectoryExists(string directory, out bool isExists)
        {
            isExists = false;
            iResponse<bool> result = APC2.QueryDirectoryExists(directory);
            if (!result.IsCorrect)
            {
                return false;
            }
            isExists = result.Result;
            return true;
        }
        /// <summary>
        /// 查询夹是否存在
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isExists"></param>
        /// <returns></returns>
        public bool QueryFileExists(string filePath, out bool isExists)
        {
            isExists = false;
            iResponse<bool> result = APC2.QueryfileExists(filePath);
            if (!result.IsCorrect)
            {
                return false;
            }
            isExists = result.Result;
            return true;
        }
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool DeleteFile(string filePath)
        {
            iResponse result = APC2.Delfile(filePath);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 解压文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="filePath"></param>
        /// <param name="passwrod"></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public bool UnzipfileSpecifiedDirectory(string fileName, string filePath, string passwrod, int fileSize)
        {
            iResponse result = APC2.UnzipfileSpecifiedDirectory(fileName, filePath, passwrod, fileSize);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 设备升级
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool SoftwareUpgrade(string fileName)
        {
            iResponse result = APC2.SoftwareUpgrade(fileName);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        #endregion
        #endregion Methods

        #region 液压指令
        /// <summary>
        /// 获取自整定参数
        /// </summary>
        /// <returns></returns>
        public bool GetConTrolPar(int whichvent, out Y_Control_Para Control_Par)
        {
            Control_Par = new Y_Control_Para();
            Control_Par.C_Vent = new List<float>();
            iResponse<string> result = APC2.GetDeviceControlROMData(whichvent, 72);
            if (result.IsCorrect)
            {
                string HexString = result.Result;

                //将base转为HEX
                //byte[] bytes = Convert.FromBase64String(base64String);
                Control_Par.CVentstr = HexString;

                byte[] byteArray = Enumerable.Range(0, HexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(HexString.Substring(x, 2), 16))
                                .ToArray();

                //将HEX解析为folat数组
                for (int i = 0; i < 18; i += 1)
                {
                    var bytetemp = byteArray.Skip(Control_Par.C_Vent.Count * 4).Take(4).ToArray();
                    float floatValue = BitConverter.ToSingle(bytetemp, 0); // 将byte[]数组转换为float
                    Control_Par.C_Vent.Add(floatValue);
                }

                return true;
            }
            return false;
        }


        /// <summary>
        /// 液源泵控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetBump(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.Bump, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }

        public bool LHSetBump(double value)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.Bump, value);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public ScriptHelperKVP SetPumpState(int state)
        {
            iResponse result = APC2.SetPumpState(state);
            return new ScriptHelperKVP("设置泵状态为"+state,result.IsCorrect);
        }

        /// <summary>
        /// 旋转电机控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetDJ(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.DJ, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }
        public ScriptHelperKVP SetDJ_KVP(double value)
        {
            Result<bool> Sresult = new Result<bool>();
            RealTimeMsg msg = new RealTimeMsg();
            bool success = SetDJ(value, msg, Sresult);
            return new ScriptHelperKVP($"811A旋转电机控制0~1占空比:{value},信息为{msg.Content}", success);
        }
        /// <summary>
        /// VinH阀控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetVinH(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.HVin, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }
        public bool LHSetVinH(double value)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.HVin, value);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool LHSetVinL(double value)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.LVin, value);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// VoutH阀控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetVoutH(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.HVout, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }

        public bool LHSetVoutH(double value)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.HVout, value);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool LHSetVoutL(double value)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.LVout, value);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// VinL阀控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetVinL(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.LVin, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }


        /// <summary>
        /// VoutL阀控制0~1占空比
        /// </summary>
        /// <returns></returns>
        public bool SetVoutL(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.LVout, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }

        /// <summary>
        /// Viso阀控制0,1控制 ，没有占空比
        /// </summary>
        /// <returns></returns>
        public bool SetViso(double value, RealTimeMsg msg, dynamic Sresult)
        {
            iResponse result = APC2.SetPreTestInfo(PreWho.GLF, value);
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }


        /// <summary>
        /// 获取传感器的压力值，Pin/PML/PMH/Pctl
        /// </summary>
        /// <returns></returns>
        public bool GetPSValue(out List<double> value, RealTimeMsg msg, dynamic Sresult)
        {
            value = new List<double>();
            iResponse<string> result = APC2.GetPressureInfo();
            if (result.IsCorrect)
            {
                if (msg != null)
                {
                    msg.Content = " √ ";
                }
                var strspli = result.Result.Split(',');
                value.Add(double.Parse(strspli[2]));
                value.Add(double.Parse(strspli[1]));
                value.Add(double.Parse(strspli[0]));
                value.Add(double.Parse(strspli[3]));
                return true;
            }
            else
            {
                if (msg != null)
                {
                    msg.Content = " X ";
                }
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。");
                }
                return false;
            }
        }

        /// <summary>
        /// 获取泵转速和旋转电机转速
        /// </summary>
        /// <returns></returns>
        public bool GetBumpSpeed(out double[] value, RealTimeMsg msg, dynamic Sresult)
        {
            value = new double[2];
            iResponse<string> result = APC2.GetSpeedInfo();
            if (result.IsCorrect)
            {
                msg.Content = " √ ";
                value[0] = double.Parse(result.Result.Split(',')[0]);
                value[1] = double.Parse(result.Result.Split(',')[1]);
                return true;
            }
            else
            {
                value = new double[2];
                msg.Content = " X ";
                var emsg = new ErrMsg(30002, $"" + result.GetContent(true, true));
                if (string.IsNullOrWhiteSpace(Sresult.Conclusion))
                {
                    Sresult.SetConclusion($"上电重试与正常重新测试都失败的情况下，请联系工装硬件工程师。", emsg);
                }
                return false;
            }
        }

        /// <summary>
        /// 模块清零,2是高压，3是低压
        /// </summary>
        /// <returns></returns>
        public bool ClearPressure(PressureModel pm)
        {
            iResponse result = APC2.ClearPressureModel(pm);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 设置控制模块稳定误差和稳定时间(液压)
        /// </summary>
        /// <param name="stability">稳定误差</param>
        /// <returns></returns>
        public bool SetPStability()
        {
            //波动度初始默认0.005 * 0.01 * FS，其中0.005可以通过518指令更新（0.003~1）、控制状态下波动时间5s

            iResponse result = APC2.SetPressureModelStableParam(1, 0.005, 10);
            return result.IsCorrect;
        }


        /// <summary>
        /// 液压设备，蓄能器充压弹窗提示
        /// </summary>
        /// <returns></returns>
        public bool SetXNQQYTC()
        {
            string Path = "\\FlashDisk\\APC\\userdata\\Profiles\\APC.Configuration.Host";
            string Conunt = "Section=Component;Key=IsAccumulatorChargingEnable;Value=true";
            iResponse result = APC2.WriteDataInConfiguration(Path, Conunt);
            if (result.IsCorrect)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 液压设备，蓄能器充压弹窗提示,结果获取
        /// </summary>
        /// <returns></returns>
        public bool GetXNQQYTC()
        {
            string Path = "\\FlashDisk\\APC\\userdata\\Profiles\\APC.Configuration.Host";
            string Conunt = "Section=Component;Key=IsAccumulatorChargingEnable";
            iResponse<string> result = APC2.GetDataInConfiguration(Path, Conunt);
            if (result.IsCorrect && result.Result.ToLower().Contains("true"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        /// <summary>
        /// /恢复出厂
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool ResetFactoryByManufactor(string password)
        {
            return APC2.ResetFactoryByManufactor(password).IsCorrect;
        }
        /// <summary>
        /// 获取泵的阻转电流
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public bool GetDumpCurrent(out string current)
        {
            iResponse<string> result = APC2.GetDumpCurrent();
            if (result.IsCorrect)
            {
                current=result.Result;
                return true;
            }
            else
            {
                current = null;
                return false;
            }
        }
        /// <summary>
        /// 设置泵的阻转电流
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public bool SetDumpCurrent(int current)
        {
            iResponse result = APC2.SetDumpCurrent(current);
            return result.IsCorrect;
        }

        public bool SetDumpStallingCurrent()
        {
            iResponse result = APC2.SetDumpStallingCurrent();
            return result.IsCorrect;
        }
    }

   
    /// <summary>
    /// 液压整机自整定阀门参数
    /// </summary>
    public class Y_Control_Para
    {
        public string CVentstr;

        public List<float> C_Vent;
    }

}
