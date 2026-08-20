using System;
using System.Collections.Generic;
using Xmas11.Comm.Devices;
using Bots.TestBench.Device.Base;
using Xmas11.Domain.Mechanics;
using Bots.TestBench.Device.Base.Comm;
using Xmas11.Comm.Data.Common;
using System.IO;
using Bots.TestBench.Util.IO.Zip;
using System.Linq;
using Bots.TestBench.DataAccess.DataClass;
using Bots.TestBench.Device.Properties;
using Bots.TestBench.Device.Upgrade;
using System.Net;
using Bots.TestBench.Util.IO;
using Xmas11.Comm.Data.HPC;
using Xmas11.Domain.Electricity;
using System.ComponentModel;
using Bots.TestBench.Model.Scripts;

namespace Bots.TestBench.Device
{
    /// <summary>
    /// ConST810
    /// </summary>
    [Serializable]
    public class ConST810 : UpgradeDevice
    {
        #region Ctors

        /// <summary>
        /// 构造函数 
        /// </summary>
        public ConST810()
        {
            this.DeviceType = DeviceType.DUT;
        }

        #endregion

        #region Properties

        /// <summary>
        /// 获取810
        /// </summary>
        public HPC HPC
        {
            get
            {
                //为空异常怎么处理????
                return this.CommInstance as HPC;
            }
        }

        #endregion

        #region NeededMethods
        /// <summary>
        /// 蓝牙打开
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP OpenBLE()
        {
            var res=HPC.OpenBLE();
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP("蓝牙打开成功", true);
            }
            else
            {
                return new ScriptHelperKVP("蓝牙打开失败", false);
            }
        }
        public ScriptHelperKVP ConsultBLEStatusAndMACAddress(out string result)
        {
            var res = HPC.ConsultBLEStatusAndMACAddress();
            if (res.IsCorrect)
            {
                result = res.Result;
                return new ScriptHelperKVP("获取蓝牙状态和MAC地址成功:"+res.Result, true);
            }
            else
            {
                result = "";
                return new ScriptHelperKVP("获取蓝牙状态和MAC地址失败", false);
            }
        }
        /// <summary>
        /// [未实现]获取蓝牙名称和MAC
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP GetBlueToothNameMAC(out string blenamemac)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// [未实现]蓝牙关闭
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP CloseBLE()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// [未实现]处理BLE
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP HandBle()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// [未实现]读取EEPROM
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP ReadEEPROM(out string str)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// [未实现]写入EEPROM
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP WriteEEPROM(string str)
        {
            throw new NotImplementedException();
        }
        ///// <summary>
        ///// [未实现]切换至固定模式
        ///// </summary>
        ///// <returns></returns>
        ///// <exception cref="NotImplementedException"></exception>
        //public ScriptHelperKVP SwitchToFixedMode()
        //{
        //    throw new NotImplementedException();
        //}

        #endregion

        #region AdditionalMethods
        /// <summary>
        /// 切换至HART测量模式
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool SwitchToHARTMeasureMode()
        {
            return HPC.StartRemoteMode(Xmas11.Comm.HartProtocal.PowerSupplyMode.IPIR);
        }
        /// <summary>
        /// 切换至HART测量模式
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP SwitchToHARTMeasureMode_KVP()
        {
            var strRes = "810切换至HART测量模式";
            if (HPC.StartRemoteMode(Xmas11.Comm.HartProtocal.PowerSupplyMode.IPIR))
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 打开或关闭某序号阀
        /// </summary>
        /// <param name="valveNumber">阀序号</param>
        /// <param name="isOpen">是否为打开</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool SetOneValveOpenCloseState(int valveNumber, bool isOpen)
        {
            return HPC.SetValveStata(isOpen ? (byte)(Convert.ToInt32(Math.Pow(2, valveNumber))) : 0).IsCorrect;
        }
        /// <summary>
        /// 打开或关闭某序号阀
        /// </summary>
        /// <param name="valveNumber">阀序号</param>
        /// <param name="isOpen">是否为打开</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP SetOneValveOpenCloseState_KVP(int valveNumber, bool isOpen)
        {
            var strRes = "810" + (isOpen ? "打开" : "关闭") + "序号" + valveNumber + "阀";
            if (HPC.SetValveStata(isOpen ? (byte)(Convert.ToInt32(Math.Pow(2, valveNumber))) : 0).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 获取电池电量
        /// </summary>
        /// <param name="percentage"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool GetBatteryPercentage(out double percentage)
        {
            var res = HPC.GetBatteryCapacity();
            percentage = res.Result;
            return res.IsCorrect;
        }
        /// <summary>
        /// 获取电池电量
        /// </summary>
        /// <param name="percentage"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ScriptHelperKVP GetBatteryPercentage_KVP(out double percentage)
        {
            var strRes = "810获取电池电量";

            var res = HPC.GetBatteryCapacity();
            res.Result = Math.Round(res.Result / 66, 2);
            percentage = res.Result;
            strRes += res.ToString();
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "%成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "%失败", false);
            }
        }
        /// <summary>
        /// 切换到电流输出
        /// </summary>
        /// <returns></returns>
        public bool ChangeToSource_mA()
        {
            return HPC.ChangeToSource_mA().IsCorrect;
        }
        /// <summary>
        /// 切换到电流输出
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP ChangeToSource(string sourceString)
        {
            var strRes = "810切换到" + sourceString + "输出";
            bool res = false;
            switch (sourceString)
            {
                case "mA":
                    {
                        res = HPC.ChangeToSource_mA().IsCorrect;
                    }
                    break;
                default:
                    return new ScriptHelperKVP(strRes + "失败:无此项", false);
            }
            if (res)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 设定电流输出值
        /// </summary>
        /// <returns></returns>
        public bool SetCurrent(Current current)
        {
            return HPC.SetCurrent(current).IsCorrect;
        }
        /// <summary>
        /// 设定电流输出值
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP SetCurrent_KVP(Current current)
        {
            var strRes = "810设定电流输出值为" + current.ToString();

            if (HPC.SetCurrent(current).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 读取当前测量值
        /// </summary>
        /// <returns></returns>
        public bool GetMeasureValue(out ValueAndUnit vau)
        {
            var res = HPC.GetMeasureValue();
            vau = res.Result;
            return res.IsCorrect;
        }
        /// <summary>
        /// 读取当前测量值
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP GetMeasureValue_KVP(out ValueAndUnit vau)
        {
            var strRes = "810读取当前测量值";
            var res = HPC.GetMeasureValue();
            vau = res.Result;
            strRes += res.ToString();
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 切换到开关测量
        /// </summary>
        /// <returns></returns>
        public bool ChangeToMeasure_Switch()
        {
            return HPC.ChangeToMeasure_Switch().IsCorrect;
        }
        /// <summary>
        /// 切换到开关测量
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP ChangeToMeasure_Switch_KVP()
        {
            var strRes = "810切换到开关测量";
            if (HPC.ChangeToMeasure_Switch().IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 开关通断状态检测
        /// </summary>
        /// <returns></returns>
        public bool GetMeasureValue_Switch(out OpenCloseState ocs)
        {
            var res = HPC.GetMeasureValue_Switch();
            ocs = res.Result;
            return res.IsCorrect;
        }
        /// <summary>
        /// 开关通断状态检测
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP GetMeasureValue_Switch_KVP(out OpenCloseState ocs)
        {
            var strRes = "810开关通断状态检测";
            var res = HPC.GetMeasureValue_Switch();
            ocs = res.Result;
            strRes += res.ToString();
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 切换远程透传模式
        /// </summary>
        /// <param name="supplyMode">变送器电源电阻模式</param>
        /// <returns></returns>
        public bool StartRemoteMode(Xmas11.Comm.HartProtocal.PowerSupplyMode supplyMode)
        {
            return HPC.StartRemoteMode(supplyMode);
        }
        /// <summary>
        /// 切换远程透传模式
        /// </summary>
        /// <param name="supplyMode">变送器电源电阻模式</param>
        /// <returns></returns>
        public ScriptHelperKVP StartRemoteMode_KVP(Xmas11.Comm.HartProtocal.PowerSupplyMode supplyMode)
        {
            var strRes = "810切换远程透传模式为" + supplyMode.ToString();

            if (HPC.StartRemoteMode(supplyMode))
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public enum HARTConnectStatus
        {
            Unknown = 0,
            Connected = 1,
            Disconnected = 2,
        }
        /// <summary>
        /// 切换远程透传模式
        /// </summary>
        /// <param name="supplyMode">变送器电源电阻模式</param>
        /// <returns></returns>
        public bool ReadHARTConnectStatus(out HARTConnectStatus stt)
        {
            var hi = HPC.Polling();
            if (hi != null)
            {
                var info = HPC.GetDeviceInfo(hi);
                if (info != null)
                {
                    stt = HARTConnectStatus.Connected;
                }
                else
                {
                    stt = HARTConnectStatus.Disconnected;
                }
                return true;
            }
            else
            {
                stt = HARTConnectStatus.Unknown;
                return false;
            }
        }
        public  ScriptHelperKVP GetHartDeviceInfo(out string info)
        {
            var strRes = "810获取HART设备信息";
            var res = HPC.GetHartDeviceInfo();
            if (res.IsCorrect)
            {
                info = res.Result;
                return new ScriptHelperKVP(strRes + "成功:"+info, true);
            }
            else
            {
                info = string.Empty;
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP ConnectHartDevice(int address)
        {
            var strRes = "810开始连接HART设备"+address;
            if (HPC.ConnectHartDevice(address).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP GetHartDevices(out List<int> devices)
        {
            var strRes = "810开始获取HART设备";
            var res = HPC.GetHartDevices();
            if (res.IsCorrect)
            {
                devices = res.Result;
                return new ScriptHelperKVP(strRes + "成功,共" + devices.Count + "个", true);
            }
            else
            {
                devices = new List<int>();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP GetHartDevicesDetails(out Dictionary<int,string> devices)
        {
            var strRes = "810开始获取HART设备详情";
            var res = HPC.GetHartDevicesDetails();
            if (res.IsCorrect)
            {
                devices = res.Result;
                return new ScriptHelperKVP(strRes + "成功,共" + devices.Count + "个", true);
            }
            else
            {
                devices = new Dictionary<int, string>();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP SearchHartFunction(SearchState targetSearchState)
        {
            var strRes = "810开始进行HART搜索";
            if (HPC.SearchHart(targetSearchState).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP SearchHartAddress(int begin,int end)
        {
            var strRes = "810开始对地址"+begin+"到"+end+"进行HART搜索";
            if (HPC.SearchHartAddresses(begin,end).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 切换远程透传模式
        /// </summary>
        /// <param name="supplyMode">变送器电源电阻模式</param>
        /// <returns></returns>
        public ScriptHelperKVP ReadHARTConnectStatus_KVP(out HARTConnectStatus stt)
        {
            var strRes = "810切换远程透传模式";

            var hi = HPC.Polling();
            if (hi != null)
            {
                var info = HPC.GetDeviceInfo(hi);
                if (info != null)
                {
                    stt = HARTConnectStatus.Connected;
                }
                else
                {
                    stt = HARTConnectStatus.Disconnected;
                }
                strRes += stt.ToString();
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                stt = HARTConnectStatus.Unknown;
                strRes += stt.ToString();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 关闭24V
        /// </summary>
        /// <returns></returns>
        public bool Close24V()
        {
            return HPC.Set24VState(Power24VState.Close).IsCorrect;
        }
        /// <summary>
        /// 关闭WIFI
        /// </summary>
        /// <returns></returns>
        public bool CloseWifi()
        {
            return HPC.SetWifiState(OpenCloseState.Close).IsCorrect;
        }
        /// <summary>
        /// 获取外接模块A的压力值
        /// </summary>
        /// <returns></returns>
        public bool GetPressure_EPM_A(out Pressure val)
        {
            var res = HPC.GetPressure_EPM_A();
            val = res.Result;
            return res.IsCorrect;
        }
        /// <summary>
        /// 获取外接模块的压力值
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP GetPressure_EPM(string moduleName, out Pressure val)
        {
            var strRes = "810获取外接模块" + moduleName + "的压力值";
            iResponse<Pressure> ress = null;
            switch (moduleName)
            {
                case "A":
                    ress = HPC.GetPressure_EPM_A();
                    break;
                case "B":
                    ress = HPC.GetPressure_EPM_B();
                    break;
                default:
                    break;
            }
            val = ress.Result;
            strRes += ress.ToString();
            if (ress.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 获取外接模块B的压力值
        /// </summary>
        /// <returns></returns>
        public bool GetPressure_EPM_B(out Pressure val)
        {
            var res = HPC.GetPressure_EPM_B();
            val = res.Result;
            return res.IsCorrect;
        }
        /// <summary>
        /// 获取CDP模块是否上线
        /// </summary>
        /// <param name="isOnline"></param>
        /// <returns></returns>
        public bool GetStatusCDPOnline(out bool isOnline)
        {
            var res = HPC.FW_IPM.GetSerialNumber();
            isOnline = res.IsCorrect;
            return res.IsCorrect;
        }
        /// <summary>
        /// 获取CDP模块是否上线
        /// </summary>
        /// <param name="isOnline"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetStatusCDPOnline_KVP(out bool isOnline)
        {
            var strRes = "810获取CDP模块是否上线";
            var res = HPC.FW_IPM.GetSerialNumber();
            isOnline = res.IsCorrect;
            strRes += isOnline.ToString();
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public ScriptHelperKVP SetInOutValveOpenCloseState_KVP(bool isInOpen, bool isOutOpen)
        {
            var strRes = "810" + (isInOpen ? "开启" : "关闭") + "进气阀," + (isOutOpen ? "开启" : "关闭") + "排气阀";
            if (SetInOutValveOpenCloseState(isInOpen, isOutOpen))
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 设置进气阀和排气阀状态
        /// </summary>
        /// <param name="isInOpen">进气阀是否打开</param>
        /// <param name="isOutOpen">排气阀是否打开</param>
        /// <returns>是否成功完成</returns>
        public bool SetInOutValveOpenCloseState(bool isInOpen, bool isOutOpen)
        {
            if (isInOpen)
            {
                if (isOutOpen)
                {
                    return false;
                }
                else
                {
                    return HPC.SetReleaseStata(1).IsCorrect;
                }
            }
            else
            {
                if (isOutOpen)
                {
                    return HPC.SetReleaseStata(-1).IsCorrect;
                }
                else
                {
                    return HPC.SetReleaseStata(0).IsCorrect;
                }
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
                this.CommInstance = factory.BeginCreate<HPC>(this.CommConfig);
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
                string moduleSN = string.Empty;
                if (GetModuleSerialNumber(out moduleSN))
                {
                    this.DUT.AddInfo("MSN", moduleSN);
                }
                //模块激励值
                double oriv = double.NaN;
                if (GetModuleSensorPowerSupplyValue(out oriv))
                {
                    this.DUT.AddInfo("MORIV", oriv.ToString());
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
        /// 获取序列号
        /// </summary>
        /// <returns></returns>
        public bool GetSerialNumber(out string code)
        {
            code = string.Empty;
            iResponse<string> result = HPC.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            code = result.Result;
            return true;
        }
        /// <summary>
        /// 获取序列号
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP GetSerialNumber_KVP(out string code)
        {
            var strRes = "810获取序列号";
            code = string.Empty;
            iResponse<string> result = HPC.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            code = result.Result;
            strRes += result.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }

        /// <summary>
        /// 设备序列号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetSerialNumber(string code)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse result = HPC.SetSerialNumber(code);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetDevType(out string type)
        {
            type = "";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            type = string.Empty;
            iResponse<string> result = HPC.GetDevType();
            if (!result.IsCorrect)
            {
                return false;
            }
            type = result.Result;
            return true;
        }
        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetDevType_KVP(out string type)
        {
            var strRes = "810获取设备类型";
            type = "";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            type = string.Empty;
            iResponse<string> result = HPC.GetDevType();
            if (!result.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            type = result.Result;
            strRes += type;
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        ///设置设备类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetDevType(string type)
        {
            iResponse result = HPC.SetDevType(type);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取设备机型
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool GetDeviceModel(out string model)
        {
            model = string.Empty;
            iResponse<Xmas11.Comm.Data.HPC.DeviceModel> result = HPC.GetDeviceModel();
            if (!result.IsCorrect)
            {
                return false;
            }
            model = result.Result.ToString();
            return true;
        }
        #endregion

        #region Methods


        /// <summary>
        /// 设置电测开关状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool SetElectricityState(OpenCloseState state)
        {
            iResponse setState = HPC.SetElectricityState(state);
            //正常应该进行回读验证，设置需要重启生效，故把验证放到出厂设置。
            return setState.IsCorrect;
        }
        /// <summary>
        /// 读取电测开关状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetElectricityState(out OpenCloseState state)
        {
            state = OpenCloseState.Close;
            iResponse<OpenCloseState> getState = HPC.GetElectricityState();
            if (!getState.IsCorrect)
            {
                return false;
            }
            state = getState.Result;
            return true;
        }


        #region 文件操作

        /// <summary>
        /// 查找是否存在文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool SearchFile(string path)
        {
            iResponse<bool> response = HPC.SearchFile(path);
            if (response.IsCorrect)
            {
                return response.Result;
            }
            return false;
        }

        /// <summary>
        /// 查找是否存在文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool SearchDirectory(string path)
        {
            iResponse<bool> response = HPC.SearchDirectory(path);
            if (response.IsCorrect)
            {
                return response.Result;
            }
            return false;
        }

        /// <summary>
        /// 写入文件数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public bool WriteFileData(string path, string data, FileMode mode)
        {
            iResponse response = HPC.WriteFileData(path, data, mode);
            return response.IsCorrect;
        }

        /// <summary>
        /// 读取文件数据
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool ReadFileData(string path, out string data)
        {
            data = string.Empty;
            iResponse<string> response = HPC.ReadFileData(path);
            if (response.IsCorrect)
            {
                data = response.Result;
            }
            return response.IsCorrect;
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool DeleteFile(string path)
        {
            iResponse response = HPC.DeleteFile(path);
            return response.IsCorrect;
        }


        /// <summary>
        /// 删除SD卡文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool DelSDCardfile(string path)
        {
            iResponse response = HPC.DeleteStorageCardFile(path);
            return response.IsCorrect;
        }
        #endregion

        #region USB

        /// <summary>
        /// 获取USB接口状态
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetUSBType(out Xmas11.Comm.Data.HPC.USBType type)
        {
            type = Xmas11.Comm.Data.HPC.USBType.Unknown;
            iResponse<Xmas11.Comm.Data.HPC.USBType> response = HPC.GetUSBType();
            if (response.IsCorrect)
            {
                type = response.Result;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置USB主模式
        /// </summary>
        /// <returns></returns>
        public bool ChangeUSBhostType()
        {
            iResponse response = HPC.SetUSBType(Xmas11.Comm.Data.HPC.USBType.Host);
            return response.IsCorrect;
        }
        /// <summary>
        /// 设置USB从模式
        /// </summary>
        /// <returns></returns>
        public bool ChangeUSBslaveType()
        {
            iResponse response = HPC.SetUSBType(Xmas11.Comm.Data.HPC.USBType.Slave);
            return response.IsCorrect;
        }

        #endregion

        /// <summary>
        /// 获取设备图片
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Bitmap GetDeviceMainImage()
        {
            return Resources.main;
        }

        /// <summary>
        /// 设置设备生产日期
        /// </summary>
        /// <param name="computerTime"></param>
        /// <returns></returns>
        public bool SetManufactureDate(DateTime manufactureDate)
        {
            iResponse result = HPC.SetManufactureDate(manufactureDate);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取设备生产日期
        /// </summary>
        /// <param name="computerTime"></param>
        /// <returns></returns>
        public bool GetManufactureDate(out DateTime manufactureDate)
        {
            manufactureDate = DateTime.MinValue;
            iResponse<DateTime> result = HPC.GetManufactureDate();
            if (!result.IsCorrect)
            {
                return false;
            }
            manufactureDate = result.Result;
            return true;
        }

        /// <summary>
        /// 获取压力类型切换功能状态
        /// </summary>
        /// <returns></returns>
        public bool GetDiagnosticABSolutep(out OpenCloseState state)
        {
            var response = HPC.GetDiagnosticABSolutep();
            state = response.Result;
            return response.IsCorrect;
        }

        /// <summary>
        /// 获取内部模块生产日期
        /// </summary>
        /// <param name="modulefactureDate"></param>
        /// <returns></returns>
        public bool GetModulefactureDate(out DateTime modulefactureDate)
        {
            modulefactureDate = DateTime.MinValue;
            iResponse<DateTime> result = HPC.FW_IPM.GetManufactureDate();
            if (!result.IsCorrect)
            {
                return false;
            }
            modulefactureDate = result.Result;
            return true;
        }
        public ScriptHelperKVP GetSensorSerialNumber(int moduleNumber,out string sn)
        {
            var strRes = "获取传感器模块" + moduleNumber + "的编号";
            sn = string.Empty;
            if (!this.IsOpen)
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse<string> getSN = HPC.GetSensorSerialNumber((HPC.SensorEnum)moduleNumber);
            if (!getSN.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            else
            {
                sn = getSN.Result;
                return new ScriptHelperKVP(strRes + "成功:"+sn, true);
            }
        }
        public ScriptHelperKVP GetModuleSerialNumber(HPC.SensorEnum sensor, out string sn)
        {
            var strRes = "获取" + sensor.ToString() + "传感器模块的编号";
            sn = string.Empty;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse<string> getSN = HPC.GetSensorSerialNumber(sensor);
            if (!getSN.IsCorrect)
            {
                FileHelper.SaveTxtFile(getSN.GetContent(true, true));
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            else
            {
                sn = getSN.Result;
                return new ScriptHelperKVP(strRes + "成功: "+sn, true);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sensorID">1-inner,2-outerA,3-outB</param>
        /// <param name="sn"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetModuleSerialNumber(int sensorID, out string sn)
        {
            var sensor = (HPC.SensorEnum)sensorID;
            var strRes = "获取" + sensor.ToString()+ "传感器模块的编号";
            sn = string.Empty;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse<string> getSN = HPC.GetSensorSerialNumber(sensor);
            if (!getSN.IsCorrect)
            {
                FileHelper.SaveTxtFile(getSN.GetContent(true, true));
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            else
            {
                sn = getSN.Result;
                return new ScriptHelperKVP(strRes + "成功", true);
            }
        }
        /// <summary>
        /// 获取内部模块编号
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetModuleSerialNumber(out string SN)
        {
            SN = string.Empty;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse<string> result = HPC.FW_IPM.GetSerialNumber();
            if (!result.IsCorrect)
            {
                FileHelper.SaveTxtFile(result.GetContent(true, true));
                return false;
            }
            SN = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部模块传感器激励值
        /// </summary>
        /// <param name="pv"></param>
        /// <returns></returns>
        public bool GetModuleSensorPowerSupplyValue(out double pv)
        {
            pv = double.NaN;
            iResponse<double> result = HPC.FW_IPM.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            pv = result.Result;
            return true;
        }
        /// <summary>
        /// 设置设备当前日期
        /// </summary>
        /// <param name="computerTime"></param>
        /// <returns></returns>
        public bool SetSystemDateTime(DateTime computerTime)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse result = HPC.SetSystemDateTime(computerTime);
            if (!result.IsCorrect)
            {
                FileHelper.SaveTxtFile(result.GetContent(true, true));
                return false;
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 校验RTC时间
        /// </summary>
        /// <param name="computerTime"></param>
        /// <returns></returns>
        public bool RTCCheck(DateTime computerTime)
        {
            return HPC.RTCCheck(computerTime);
        }
        /// <summary>
        /// 获取软件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion(out string version)
        {
            iResponse<string> response = HPC.GetVersion();
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
        public bool GetVersion_Core(out string version)
        {
            version = "";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse<string> response = HPC.GetVersion_Core();

            if (response.IsCorrect)
            {
                version = response.Result;
                return true;
            }
            else
            {
                FileHelper.SaveTxtFile(response.GetContent(true, true));
                version = string.Empty;
                return false;
            }
        }
        /// <summary>
        /// 获取控制板固件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetHardVersion_Controller(out string version)
        {
            iResponse<string> response = HPC.GetHardVersion_Controller();
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
        /// 获取控制板固件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion_Controller(out string version)
        {
            iResponse<string> response = HPC.GetVersion_Controller();
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
        /// 获取电测板固件
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetHardVersion_Electricity(out string version)
        {
            iResponse<string> response = HPC.GetHardVersion_Electricity();
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
        public ScriptHelperKVP GetHardVersion_Electricity_KVP(out string version)
        {
            bool success = GetHardVersion_Electricity(out version);
            string versionDisplay = success ? version : "获取失败";
            return new ScriptHelperKVP($"ConST810A获取电测板固件硬件版本:{versionDisplay}", success);
        }
        /// <summary>
        /// 获取电测板固件
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion_Electricity(out string version)
        {
            iResponse<string> response = HPC.GetVersion_Electricity();
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
            return new ScriptHelperKVP($"ConST810A获取电测板固件:{versionDisplay}", success);
        }
        /// <summary>
        /// 主板电源状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetMainBoardCheckStata(out Xmas11.Comm.Data.HPC.CheckStata state)
        {
            state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getMainBoardCheckStata = HPC.GetMainBoardCheckStata();
            if (!getMainBoardCheckStata.IsCorrect)
            {
                FileHelper.SaveTxtFile(getMainBoardCheckStata.GetContent(true, true));
                state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return false;
            }
            state = getMainBoardCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 查询电池电流
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetBatteryCurrentStata(out Current state)
        {
            var getBatteryCheckStata = HPC.GetBatteryCurrent();
            if (!getBatteryCheckStata.IsCorrect)
            {
                state = new Current();
                return false;
            }
            state = getBatteryCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 查询电池电流
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetBatteryCurrentStata_KVP(out Current state)
        {
            var strRes = "810查询电池电流";

            var getBatteryCheckStata = HPC.GetBatteryCurrent();
            if (!getBatteryCheckStata.IsCorrect)
            {
                state = new Current();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            state = getBatteryCheckStata.Result;
            strRes += getBatteryCheckStata.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 电池电源状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetBatteryCheckStata(out Xmas11.Comm.Data.HPC.CheckStata state)
        {
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getBatteryCheckStata = HPC.GetBatteryCheckStata();
            if (!getBatteryCheckStata.IsCorrect)
            {
                state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return false;
            }
            state = getBatteryCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 电池电源状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetBatteryCheckStata_KVP(out Xmas11.Comm.Data.HPC.CheckStata state)
        {
            var strRes = "810电池电源状态";

            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getBatteryCheckStata = HPC.GetBatteryCheckStata();
            if (!getBatteryCheckStata.IsCorrect)
            {
                state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                strRes += state.ToString();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            state = getBatteryCheckStata.Result;
            strRes += state.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 启用24V电源
        /// </summary>
        /// <returns></returns>
        public bool Set24VStateOpen()
        {
            return HPC.Set24VState(Power24VState.Open).IsCorrect;
        }
        /// <summary>
        /// 启用24V电源
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP Set24VStateOpen_KVP()
        {
            var strRes = "启用24V电源";

            if (HPC.Set24VState(Power24VState.Open).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);

            }
        }
        public ScriptHelperKVP Set24VStateClose()
        {
            var strRes = "关闭24V电源";

            if (HPC.Set24VState(Power24VState.Close).IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);

            }
        }
        /// <summary>
        /// 关闭气柱头校正
        /// </summary>
        /// <returns></returns>
        public bool SetAirStigmaClose()
        {
            return HPC.SetAirStigma(OpenCloseState.Close).IsCorrect;
        }
        /// <summary>
        /// 打开按键音
        /// </summary>
        /// <returns></returns>
        public bool SetTouchSoundStateOpen()
        {
            return HPC.SetTouchSoundState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 打开提示音
        /// </summary>
        /// <returns></returns>
        public bool SetTipsSoundStateOpen()
        {
            return HPC.SetTipsSoundState(OpenCloseState.Open).IsCorrect;
        }
        /// <summary>
        /// 设置控制器控制模式
        /// </summary>
        /// <returns></returns>
        public bool ChangeToPressureControl()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse setVentMode = HPC.ChangeToPressureControl();
            if (!setVentMode.IsCorrect)
            {
                FileHelper.SaveTxtFile(setVentMode.GetContent(true, true));
            }
            return setVentMode.IsCorrect;
        }
        /// <summary>
        /// 设置控制器测量模式
        /// </summary>
        /// <returns></returns>
        public bool ChangeToPressureMeasure()
        {
            iResponse setVentMode = HPC.ChangeToPressureMeasure();
            return setVentMode.IsCorrect;
        }
        /// <summary>
        /// 设置排空状态
        /// </summary>
        /// <returns></returns>
        public bool SetVentMode()
        {
            iResponse setVentMode = HPC.SetVentMode();
            return setVentMode.IsCorrect;
        }
        /// <summary>
        /// 设置测试状态
        /// </summary>
        /// <returns></returns>
        public bool SetTestMode()
        {
            iResponse setTestMode = HPC.SetTestMode();
            return setTestMode.IsCorrect;
        }
        /// <summary>
        /// 功耗测试
        /// </summary>
        /// <param name="EnergyCheckStata"></param>
        /// <returns></returns>
        public bool GetEnergyCheckStata(out List<double> EnergyCheckStata)
        {
            Xmas11.Comm.Devices.iResponse<List<double>> getEnergyCheckStata = HPC.GetEnergyCheckStata();
            if (!getEnergyCheckStata.IsCorrect)
            {
                EnergyCheckStata = null;
                return false;
            }
            EnergyCheckStata = getEnergyCheckStata.Result;
            return true;
        }
        public enum EnergyCheckItemEnum
        {
            Current_mA,
            Voltage_mV,
            Consumption_mW
        }
        /// <summary>
        /// 功耗测试
        /// </summary>
        /// <param name="EnergyCheckStata"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetEnergyCheckStata_KVP(out Dictionary<EnergyCheckItemEnum,double> EnergyCheckStata)
        {
            var str = "获取功耗值";
            Xmas11.Comm.Devices.iResponse<List<double>> getEnergyCheckStata = HPC.GetEnergyCheckStata();
            if (!getEnergyCheckStata.IsCorrect)
            {
                EnergyCheckStata = null;
                str += "失败";
                return new ScriptHelperKVP(str,false);
            }
            EnergyCheckStata = new Dictionary<EnergyCheckItemEnum, double>
            {
                {
                    EnergyCheckItemEnum.Current_mA,
                    getEnergyCheckStata.Result[1]
                },
                {
                    EnergyCheckItemEnum.Voltage_mV,
                    getEnergyCheckStata.Result[0]
                },
                {
                    EnergyCheckItemEnum.Consumption_mW,
                    getEnergyCheckStata.Result[2]
                }
            };
            return new ScriptHelperKVP(str+
                ":电流"+getEnergyCheckStata.Result[1]+
                "mA,电压"+getEnergyCheckStata.Result[0]+
                "mV,功耗"+getEnergyCheckStata.Result[2]+"mW",true);
        }
        /// <summary>
        /// 适配器供电检测
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetPowerAdapterCheck(out Xmas11.Comm.Data.HPC.CheckStata state)
        {
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getPowerAdapterCheck = HPC.GetPowerAdapterCheck();
            if (!getPowerAdapterCheck.IsCorrect)
            {
                state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return false;
            }
            state = getPowerAdapterCheck.Result;
            return true;
        }
        /// <summary>
        /// 适配器供电检测
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetPowerAdapterCheck_KVP(out Xmas11.Comm.Data.HPC.CheckStata state)
        {
            var strRes = "810适配器供电检测";
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getPowerAdapterCheck = HPC.GetPowerAdapterCheck();
            if (!getPowerAdapterCheck.IsCorrect)
            {
                state = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                strRes += state.ToString();
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            state = getPowerAdapterCheck.Result;
            strRes += state.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 设置显示屏亮度最高
        /// </summary>
        /// <returns></returns>
        public bool SetDisplayLevelHigh()
        {
            iResponse setDisplayLevel_8 = HPC.SetDisplayLevel(8);
            return setDisplayLevel_8.IsCorrect;

        }
        /// <summary>
        /// 设置显示屏亮度最低
        /// </summary>
        /// <returns></returns>
        public bool SetDisplayLevelLow()
        {
            iResponse setDisplayLevel_1 = HPC.SetDisplayLevel(1);
            return setDisplayLevel_1.IsCorrect;

        }
        /// <summary>
        /// 设置显示屏亮度150
        /// </summary>
        /// <returns></returns>
        public bool SetDisplayLevel150()
        {
            iResponse setDisplayLevel_5 = HPC.SetDisplayLevel(5);
            return setDisplayLevel_5.IsCorrect;
        }
        /// <summary>
        /// 设置自动关背光
        /// </summary>
        /// <returns></returns>
        public bool SetBackLight()
        {
            return HPC.SetBackLight(0).IsCorrect;
        }
        /// <summary>
        /// 设置关背光后自动关机
        /// </summary>
        /// <returns></returns>
        public bool SetBackShutDown()
        {
            return HPC.SetBackShutDown(0).IsCorrect;
        }

        /// <summary>
        /// 打开触摸屏幕校准测试
        /// </summary>
        /// <returns></returns>
        public bool OpenDisplayTouchCal()
        {
            iResponse openDisplayTouchCal = HPC.OpenDisplayTouchCal();
            return openDisplayTouchCal.IsCorrect;
        }
        /// <summary>
        /// 打开显示屏坏点测试
        /// </summary>
        /// <returns></returns>
        public bool OpenDisplayDeadPixelTest()
        {
            iResponse openDisplayDeadPixelTest = HPC.OpenDisplayTest(Xmas11.Comm.Data.HPC.DisplayTest.DeadPixelTest);
            return openDisplayDeadPixelTest.IsCorrect;
        }
        /// <summary>
        /// 关闭显示屏坏点测试
        /// </summary>
        /// <returns></returns>
        public bool CloseDisplayDeadPixelTest()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse closeDisplayDeadPixelTest = HPC.CloseDisplayTest(Xmas11.Comm.Data.HPC.DisplayTest.DeadPixelTest);
            if (!closeDisplayDeadPixelTest.IsCorrect)
            {
                FileHelper.SaveTxtFile(closeDisplayDeadPixelTest.GetContent(true, true));
            }
            return closeDisplayDeadPixelTest.IsCorrect;
        }
        /// <summary>
        /// 打开显示屏触摸测试
        /// </summary>
        /// <returns></returns>
        public bool OpenDisplayTouchTest()
        {
            iResponse openDisplayDeadPixelTest = HPC.OpenDisplayTest(Xmas11.Comm.Data.HPC.DisplayTest.TouchTest);

            return openDisplayDeadPixelTest.IsCorrect;
        }
        /// <summary>
        /// 关闭显示屏触摸测试
        /// </summary>
        /// <returns></returns>
        public bool CloseDisplayTouchTest()
        {
            iResponse closeDisplayTouchTest = HPC.CloseDisplayTest(Xmas11.Comm.Data.HPC.DisplayTest.TouchTest);
            return closeDisplayTouchTest.IsCorrect;
        }

        /// <summary>
        /// 获取屏幕测试结果
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool GetDisplayTouch(out string Result)
        {
            Result = string.Empty;
            iResponse<string> result = HPC.GetDisplayTest();
            if (!result.IsCorrect)
            {
                return false;
            }
            Result = result.Result.ToString();
            return true;
        }

        /// <summary>
        /// 开启蜂鸣器
        /// </summary>
        /// <returns></returns>
        public bool OpenBuzzer()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse openBuzzer = HPC.OpenBuzzer();
            if (!openBuzzer.IsCorrect)
            {
                FileHelper.SaveTxtFile(openBuzzer.GetContent(true, true));
            }
            return openBuzzer.IsCorrect;
        }
        /// <summary>
        /// 开启蜂鸣器
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP OpenBuzzer_KVP()
        {
            var strRes = "810开启蜂鸣器";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse openBuzzer = HPC.OpenBuzzer();
            if (!openBuzzer.IsCorrect)
            {
                FileHelper.SaveTxtFile(openBuzzer.GetContent(true, true));
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
        }
        /// <summary>
        /// 关闭蜂鸣器
        /// </summary>
        /// <returns></returns>
        public bool CloseBuzzer()
        {
            var strRes = "810关闭蜂鸣器";
            iResponse closeBuzzer = HPC.CloseBuzzer();
            return closeBuzzer.IsCorrect;
        }
        /// <summary>
        /// 关闭蜂鸣器
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP CloseBuzzer_KVP()
        {
            var strRes = "810关闭蜂鸣器";
            iResponse closeBuzzer = HPC.CloseBuzzer();
            if (closeBuzzer.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);

            }
        }
        /// <summary>
        /// 打开呼吸灯
        /// </summary>
        /// <returns></returns>
        public bool SetBreathingLightOpen()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse setBreathingLightOpen = HPC.SetBreathingLight(Xmas11.Comm.Data.Common.OpenCloseState.Open);
            if (!setBreathingLightOpen.IsCorrect)
            {
                FileHelper.SaveTxtFile(setBreathingLightOpen.GetContent(true, true));
            }
            return setBreathingLightOpen.IsCorrect;
        }
        /// <summary>
        /// 打开呼吸灯
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP SetBreathingLightOpen_KVP()
        {
            var strRes = "810打开呼吸灯";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse setBreathingLightOpen = HPC.SetBreathingLight(Xmas11.Comm.Data.Common.OpenCloseState.Open);
            if (!setBreathingLightOpen.IsCorrect)
            {
                FileHelper.SaveTxtFile(setBreathingLightOpen.GetContent(true, true));
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
        }
        /// <summary>
        /// 关闭呼吸灯
        /// </summary>
        /// <returns></returns>
        public bool SetBreathingLightClose()
        {
            iResponse setBreathingLightCloseTurn = HPC.SetBreathingLight(Xmas11.Comm.Data.Common.OpenCloseState.Close);
            return setBreathingLightCloseTurn.IsCorrect;
        }
        /// <summary>
        /// 关闭呼吸灯
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP SetBreathingLightClose_KVP()
        {
            var strRes = "810关闭呼吸灯";
            iResponse setBreathingLightCloseTurn = HPC.SetBreathingLight(Xmas11.Comm.Data.Common.OpenCloseState.Close);
            if (setBreathingLightCloseTurn.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 获取内部压力模块通讯状态
        /// </summary>
        /// <param name="moduleConnectState">模块状态</param>
        /// <returns></returns>
        public bool GetIPMConnectState(out Xmas11.Comm.Data.Common.OnOFFLineState moduleConnectState)
        {
            moduleConnectState = Xmas11.Comm.Data.Common.OnOFFLineState.UnKnown;
            iResponse<Xmas11.Comm.Data.Common.OnOFFLineState> getModuleConnectState_A = HPC.GetModuleConnectState(1);
            if (!getModuleConnectState_A.IsCorrect)
            {
                return false;
            }
            moduleConnectState = getModuleConnectState_A.Result;
            return true;
        }
        /// <summary>
        /// 获取外接压力模块A通讯状态
        /// </summary>
        /// <param name="moduleConnectState">模块状态</param>
        /// <returns></returns>
        public bool GetAModuleConnectState(out Xmas11.Comm.Data.Common.OnOFFLineState moduleConnectState)
        {
            moduleConnectState = Xmas11.Comm.Data.Common.OnOFFLineState.UnKnown;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse<Xmas11.Comm.Data.Common.OnOFFLineState> getModuleConnectState_A = HPC.GetModuleConnectState(2);
            if (!getModuleConnectState_A.IsCorrect)
            {
                FileHelper.SaveTxtFile(getModuleConnectState_A.GetContent(true, true));
                return false;
            }
            moduleConnectState = getModuleConnectState_A.Result;
            return true;
        }
        /// <summary>
        /// 获取外接压力模块A或B通讯状态
        /// </summary>
        /// <param name="moduleConnectState">模块状态</param>
        /// <returns></returns>
        public ScriptHelperKVP GetModuleConnectState(string ModuleLetter, out Xmas11.Comm.Data.Common.OnOFFLineState moduleConnectState)
        {
            var strRes = "810获取外接压力模块" + ModuleLetter + "通讯状态";
            moduleConnectState = Xmas11.Comm.Data.Common.OnOFFLineState.UnKnown;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            iResponse<Xmas11.Comm.Data.Common.OnOFFLineState> res = null;
            switch (ModuleLetter)
            {
                case "A":
                    res = HPC.GetModuleConnectState(2);
                    break;
                case "B":
                    res = HPC.GetModuleConnectState(3);
                    break;
                default:
                    return new ScriptHelperKVP(strRes + ":无此模块", false);
            }
            if (!res.IsCorrect)
            {
                FileHelper.SaveTxtFile(res.GetContent(true, true));
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            moduleConnectState = res.Result;
            strRes += res.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 获取外接压力模块B通讯状态
        /// </summary>
        /// <param name="moduleConnectState">模块状态</param>
        /// <returns></returns>
        public bool GetBModuleConnectState(out Xmas11.Comm.Data.Common.OnOFFLineState moduleConnectState)
        {
            iResponse<Xmas11.Comm.Data.Common.OnOFFLineState> getModuleConnectState_B = HPC.GetModuleConnectState(3);
            if (!getModuleConnectState_B.IsCorrect)
            {
                moduleConnectState = Xmas11.Comm.Data.Common.OnOFFLineState.UnKnown;
                return false;
            }
            moduleConnectState = getModuleConnectState_B.Result;
            return true;
        }

        /// <summary>
        /// 获取内部模块序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetIPMSerialNumber(out string serialNumber)
        {
            serialNumber = string.Empty;
            iResponse<string> result = HPC.FW_IPM.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            serialNumber = result.Result;
            return true;
        }
        /// <summary>
        /// 获取内部模传感器激励值
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool GetIPMSensorPowerSupplyValue(out double supplyValue)
        {
            supplyValue = 0;
            iResponse<double> result = HPC.FW_IPM.GetSensorPowerSupplyValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            supplyValue = result.Result;
            return true;
        }
        /// <summary>
        /// 设置电压测量项
        /// </summary>
        /// <returns></returns>
        public bool SetMeasureV()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse setMeasureItem = HPC.SetMeasureItem(Xmas11.Comm.Data.HPC.MeasureItem.V);
            if (!setMeasureItem.IsCorrect)
            {
                FileHelper.SaveTxtFile(setMeasureItem.GetContent(true, true));
            }
            return setMeasureItem.IsCorrect;
        }
        /// <summary>
        /// 设置电流测量项
        /// </summary>
        /// <returns></returns>
        public bool SetMeasure_mA()
        {
            iResponse setMeasureItem = HPC.SetMeasureItem(Xmas11.Comm.Data.HPC.MeasureItem.mA);
            return setMeasureItem.IsCorrect;
        }
        /// <summary>
        /// 设置电流测量项
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP SetMeasure(string measureUnit)
        {
            var strRes = "810设置测量单位为" + measureUnit;
            iResponse res = null;
            switch (measureUnit)
            {
                case "mA":
                    res = HPC.SetMeasureItem(Xmas11.Comm.Data.HPC.MeasureItem.mA);
                    break;
                case "V":
                    res = HPC.SetMeasureItem(Xmas11.Comm.Data.HPC.MeasureItem.V);
                    break;
                default:
                    return new ScriptHelperKVP(strRes + "未找到相应单位", false);
            }
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 关闭电测测量项
        /// </summary>
        /// <returns></returns>
        public bool CloseMeasureItem()
        {
            return HPC.CloseMeasureItem();
        }
        /// <summary>
        /// 获取电测板自检状态
        /// </summary>
        /// <param name="powerCheckStata"></param>
        /// <returns></returns>
        public bool GetPowerCheckStata(out Xmas11.Comm.Data.HPC.CheckStata powerCheckStata)
        {
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getPowerCheckStata = HPC.GetPowerCheckStata();
            if (!getPowerCheckStata.IsCorrect)
            {
                powerCheckStata = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return false;
            }
            powerCheckStata = getPowerCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 获取电测板自检状态
        /// </summary>
        /// <param name="powerCheckStata"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetPowerCheckStata_KVP(out Xmas11.Comm.Data.HPC.CheckStata powerCheckStata)
        {
            var strRes = "810获取电测板自检状态";
            iResponse<Xmas11.Comm.Data.HPC.CheckStata> getPowerCheckStata = HPC.GetPowerCheckStata();
            if (!getPowerCheckStata.IsCorrect)
            {
                powerCheckStata = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            powerCheckStata = getPowerCheckStata.Result;
            strRes += getPowerCheckStata.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 获取主板电源状态
        /// </summary>
        /// <param name="powerCheckStata"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetMainboardPowerState_KVP(out Xmas11.Comm.Data.HPC.PowerState powerCheckStata)
        {
            var strRes = "810获取主板电源状态";
            iResponse<Xmas11.Comm.Data.HPC.PowerState> getPowerCheckStata = HPC.GetMainboardPowerState();
            if (!getPowerCheckStata.IsCorrect)
            {
                powerCheckStata = Xmas11.Comm.Data.HPC.PowerState.UnKnown;
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            powerCheckStata = getPowerCheckStata.Result;
            strRes += getPowerCheckStata.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 获取电压测试值
        /// </summary>
        /// <param name="voltageCheckStata"></param>
        /// <returns></returns>
        public bool GetVoltageCheckStata(out List<double> voltageCheckStata)
        {
            iResponse<List<double>> getVoltageCheckStata = HPC.GetVoltageCheckStata();
            if (!getVoltageCheckStata.IsCorrect)
            {
                voltageCheckStata = null;
                return false;
            }
            voltageCheckStata = getVoltageCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 获取电流测试值
        /// </summary>
        /// <param name="currentCheckStata"></param>
        /// <returns></returns>
        public bool GetCurrentCheckStata(out List<double> currentCheckStata)
        {
            HPC.Policy.RequestTimeOut = 6000;
            iResponse<List<double>> getCurrentCheckStata = HPC.GetCurrentCheckStata();
            if (!getCurrentCheckStata.IsCorrect)
            {
                currentCheckStata = null;
                return false;
            }
            currentCheckStata = getCurrentCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 获取电流输出值
        /// </summary>
        /// <param name="currentCheckStata"></param>
        /// <returns></returns>
        public bool GetSourceCurrentCheckStata(out List<double> currentCheckStata)
        {
            HPC.Policy.RequestTimeOut = 6000;
            iResponse<List<double>> getSourceCurrentCheckStata = HPC.GetSourceCurrentCheckStata();
            if (!getSourceCurrentCheckStata.IsCorrect)
            {
                currentCheckStata = null;
                return false;
            }
            currentCheckStata = getSourceCurrentCheckStata.Result;
            return true;
        }

        /// <summary>
        /// 获取开关自检状态
        /// </summary>
        /// <param name="voltageCheckStata"></param>
        /// <returns></returns>
        public bool GetSwitchCheckStata(out List<int> switchCheckStata)
        {
            HPC.Policy.RequestTimeOut = 6000;
            Xmas11.Comm.Devices.iResponse<List<int>> getSwitchCheckStata = HPC.GetSwitchCheckStata();
            if (!getSwitchCheckStata.IsCorrect)
            {
                switchCheckStata = null;
                return false;
            }
            switchCheckStata = getSwitchCheckStata.Result;
            return true;
        }
        #region Wifi
        /// <summary>
        /// 断开WiFi
        /// </summary>
        /// <returns></returns>
        public bool DisconnectWifi()
        {
            iResponse disconnectWifi = HPC.DisconnectWifi();
            return disconnectWifi.IsCorrect;
        }
        public ScriptHelperKVP WifiOperation(string operationName, string ssid = "", string encryptionMode = "", string password = "")
        {
            var strRes = "810" + operationName + "Wifi";
            bool complete = false;
            switch (operationName)
            {
                case "打开":
                    {
                        complete = OpenWifi();
                    }
                    break;
                case "连接":
                    {
                        complete = ConnectWIFI(ssid, encryptionMode, password);
                    }
                    break;
                case "断开":
                    {
                        complete = DisconnectWifi();
                    }
                    break;
                case "关闭":
                    {
                        complete = CloseWifi();
                    }
                    break;
                default:
                    break;
            }
            if (complete)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        public bool OpenWifi()
        {
            iResponse openWifi = HPC.OpenWifi();
            System.Threading.Thread.Sleep(5000);
            iResponse<OpenCloseState> response = HPC.GetWifiState();
            if (openWifi.IsCorrect && response.IsCorrect && response.Result == OpenCloseState.Open)
                return true;
            return false;
        }

        /// <summary>
        /// 获取WIFI开启状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetWifiState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> response = HPC.GetWifiState();
            if (response.IsCorrect)
            {
                state = response.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 连接指定WiFi
        /// </summary>
        /// <param name="ssid">热点名称</param>
        /// <param name="encryptionMode">加密方式OPEN|WPA|WPA2</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        public bool ConnectWIFI(string ssid, string encryptionMode, string password)
        {
            Xmas11.Comm.Devices.iResponse setWIfiConnect = HPC.SetWifiConnect(ssid, encryptionMode, password);
            return setWIfiConnect.IsCorrect;
        }

        /// <summary>
        /// 获取WIFI连接状态
        /// </summary>
        /// <param name="lineState"></param>
        /// <returns></returns>
        public bool GetWifiConnectState(out OnOFFLineState lineState)
        {
            lineState = OnOFFLineState.UnKnown;

            iResponse<OnOFFLineState> result = HPC.GetWifiConnectState();
            if (!result.IsCorrect) return false;

            lineState = result.Result;
            return true;
        }
        /// <summary>
        /// 获取WIFI连接状态
        /// </summary>
        /// <param name="lineState"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetWifiConnectState_KVP(out OnOFFLineState lineState)
        {
            var strRes = "810获取WIFI连接状态";

            lineState = OnOFFLineState.UnKnown;

            iResponse<OnOFFLineState> result = HPC.GetWifiConnectState();
            if (!result.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            lineState = result.Result;
            strRes += result.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }

        /// <summary>
        /// 获取wifi的IP地址
        /// </summary>
        /// <param name="IP"></param>
        /// <returns></returns>
        public bool GetWifiIPAddress(out string IP)
        {
            IP = string.Empty;
            int count = 0;
            while (true)
            {
                System.Threading.Thread.Sleep(2000);
                iResponse<IPAddress> result = HPC.GetWifiIPAddress();
                if (result.IsCorrect)
                {
                    IP = result.Result.ToString();
                    return true;
                }
                else
                {
                    if (count > 5)
                    {
                        return false;
                    }
                    System.Threading.Thread.Sleep(2000);
                    count++;
                    continue;
                }
            }
        }

        /// <summary>
        /// 获取wifi的MAC地址
        /// </summary>
        /// <param name="MAC"></param>
        /// <returns></returns>
        public bool GetWifiMACAddress(out string MAC)
        {
            MAC = string.Empty;
            int count = 0;
            while (true)
            {
                System.Threading.Thread.Sleep(2000);
                iResponse<string> result = HPC.GetWifiMACAddress();
                if (result.IsCorrect)
                {
                    MAC = result.Result;
                    return true;
                }
                else
                {
                    if (count > 5)
                    {
                        return false;
                    }
                    System.Threading.Thread.Sleep(2000);
                    count++;
                    continue;
                }
            }
        }

        /// <summary>
        /// 获取wifi睡眠模式-810A不可用
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetWifiSleepState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> response = HPC.GetWifiSleepState();
            if (response.IsCorrect)
            {
                state = response.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置wifi睡眠模式-810A不可用
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool SetWifiSleepState(OpenCloseState state)
        {
            iResponse response = HPC.SetWifiSleepState(state);
            return response.IsCorrect;
        }

        public ScriptHelperKVP GetControlPanelPowerState_KVP(out Xmas11.Comm.Data.HPC.PowerState controlPanelPowerState)
        {
            var res = "获取控制板电源状态";
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.GetSourceState> getControlPanelPowerState = HPC.GetControlPanelPowerState();
            if (getControlPanelPowerState.IsCorrect)
            {
                controlPanelPowerState = getControlPanelPowerState.Result.SourcePowerResult;
                return new ScriptHelperKVP(res + "成功: " + controlPanelPowerState.ToString(), true);
            }
            controlPanelPowerState = Xmas11.Comm.Data.HPC.PowerState.UnKnown;
            return new ScriptHelperKVP(res + "失败" , false);
        }

        #endregion
        /// <summary>
        /// 获取控制板电源状态
        /// </summary>
        /// <param name="controlPanelPowerState"></param>
        /// <returns></returns>
        public bool GetControlPanelPowerState(out Xmas11.Comm.Data.HPC.PowerState controlPanelPowerState)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                controlPanelPowerState = Xmas11.Comm.Data.HPC.PowerState.UnKnown;
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.GetSourceState> getControlPanelPowerState = HPC.GetControlPanelPowerState();
            if (!getControlPanelPowerState.IsCorrect)
            {

                FileHelper.SaveTxtFile(getControlPanelPowerState.GetContent(true, true));

                controlPanelPowerState = Xmas11.Comm.Data.HPC.PowerState.UnKnown;
                return false;
            }
            controlPanelPowerState = getControlPanelPowerState.Result.SourcePowerResult;
            return true;
        }
        /// <summary>
        /// 获取SD卡插入状态
        /// </summary>
        /// <param name="controlPanelPowerState"></param>
        /// <returns></returns>
        public bool GetSDCardCheckStata(out Xmas11.Comm.Data.HPC.CheckStata SDCardCheckStata)
        {
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.CheckStata> getSDCardCheckStata = HPC.GetSDCardCheckStata();

            if (!getSDCardCheckStata.IsCorrect)
            {
                SDCardCheckStata = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return false;
            }
            SDCardCheckStata = getSDCardCheckStata.Result;
            return true;
        }
        /// <summary>
        /// 获取SD卡插入状态
        /// </summary>
        /// <param name="controlPanelPowerState"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetSDCardCheckStata_KVP(out Xmas11.Comm.Data.HPC.CheckStata SDCardCheckStata)
        {
            string res = "获取SD卡插入状态";
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.CheckStata> getSDCardCheckStata = HPC.GetSDCardCheckStata();

            if (!getSDCardCheckStata.IsCorrect)
            {
                SDCardCheckStata = Xmas11.Comm.Data.HPC.CheckStata.UnKnown;
                return new ScriptHelperKVP(res+"失败",false);
            }
            SDCardCheckStata = getSDCardCheckStata.Result;
            return new ScriptHelperKVP(res + "成功: "+SDCardCheckStata, false);
        }

        /// <summary>
        /// 获取SD卡磁盘大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool GetSDSize(out List<string> size)
        {
            iResponse<List<string>> response = HPC.GetSDSize();
            size = new List<string>();
            if (!response.IsCorrect)
                return false;
            size = response.Result;
            return true;
        }


        /// <summary>
        /// 获取SD卡磁盘大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public bool GetSDSizeNew(out List<string> size)
        {
            iResponse<List<string>> response = HPC.GetSDSizeNew();
            size = new List<string>();
            if (!response.IsCorrect)
                return false;
            size = response.Result;
            return true;
        }
        /// <summary>
        /// 获取SD卡磁盘大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetSDSizeNew_KVP(out List<string> size)
        {
            string res = "获取SD卡磁盘大小";
            iResponse<List<string>> response = HPC.GetSDSizeNew();
            size = new List<string>();
            if (!response.IsCorrect)
                return new ScriptHelperKVP(res+"失败",false);
            size = response.Result;
            return new ScriptHelperKVP(res+"成功: "+size+"byte",true);
        }

        /// <summary>
        /// 向SD添加文件
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool AddDataToSD(string file, string value)
        {
            iResponse result = HPC.DataAddtoStorageCard(file, value, FileWriteType.TRUNcate);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 向SD添加文件
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScriptHelperKVP AddDataToSD_KVP(string file, string value)
        {
            var res= "向SD添加文件:"+file+",内容:" + value;
            iResponse result = HPC.DataAddtoStorageCard(file, value, FileWriteType.TRUNcate);
            if (!result.IsCorrect)
            {
                return new ScriptHelperKVP(res+"失败",false);
            }
            return new ScriptHelperKVP(res+"成功",true);
        }
        /// <summary>
        /// 读取SD指定文件信息
        /// </summary>
        /// <param name="file"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public ScriptHelperKVP ReadDataFromSD_KVP(string file, out string value)
        {
            var res = "读取SD文件"+file+"信息";
            value = string.Empty;
            iResponse<string> result = HPC.DataReadtoStorageCard(file);
            if (!result.IsCorrect)
            {
                return new ScriptHelperKVP(res+"失败",false);
            }
            value = result.Result;
            return new ScriptHelperKVP(res + "成功: " + value, true);
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
            iResponse<string> result = HPC.DataReadtoStorageCard(file);
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 获取大气压传感器压力值
        /// </summary>
        /// <param name="AtmosSensor"></param>
        /// <returns></returns>
        public bool GetAtmosSensor(out Xmas11.Domain.Mechanics.Pressure AtmosSensor)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                AtmosSensor = new Pressure(0, PressureUnit.kPa);
                return false;
            }
            iResponse<Xmas11.Domain.Mechanics.Pressure> getAtmosSensor = HPC.GetAtmosSensor();
            if (!getAtmosSensor.IsCorrect)
            {
                FileHelper.SaveTxtFile(getAtmosSensor.GetContent(true, true));
                AtmosSensor = new Pressure(0, PressureUnit.kPa);
                return false;
            }
            AtmosSensor = getAtmosSensor.Result;
            return true;
        }
        /// <summary>
        /// 获取大气压传感器压力值
        /// </summary>
        /// <param name="AtmosSensor"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetAtmosSensor_KVP(out Xmas11.Domain.Mechanics.Pressure AtmosSensor)
        {
            var strRes = "810获取大气压传感器压力值";
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                AtmosSensor = new Pressure(0, PressureUnit.kPa);
                return new ScriptHelperKVP(strRes + "连接失败", false);
            }
            iResponse<Xmas11.Domain.Mechanics.Pressure> getAtmosSensor = HPC.GetAtmosSensor();
            if (!getAtmosSensor.IsCorrect)
            {
                FileHelper.SaveTxtFile(getAtmosSensor.GetContent(true, true));
                AtmosSensor = new Pressure(0, PressureUnit.kPa);
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            AtmosSensor = getAtmosSensor.Result;
            strRes += getAtmosSensor.ToString();
            return new ScriptHelperKVP(strRes + "成功", true);
        }
        /// <summary>
        /// 获取大气压模块SN号
        /// </summary>
        /// <param name="AtmosphericSensorSN"></param>
        /// <returns></returns>
        public bool GetAtmosSensorSN(out string AtmosphericSensorSN)
        {
            Xmas11.Comm.Devices.iResponse<string> getAtmosphericSensorSN = HPC.GetAtmosSensorSN();
            if (!getAtmosphericSensorSN.IsCorrect)
            {
                AtmosphericSensorSN = string.Empty;
                return false;
            }
            AtmosphericSensorSN = getAtmosphericSensorSN.Result;
            return true;
        }

        /// <summary>
        /// 设置大气压传感器序列号
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        public bool SetAtmosSerialNumber(string serialNumber)
        {
            return HPC.SetAtmosSerialNumber(serialNumber).IsCorrect;
        }

        /// <summary>
        /// 设定内部模块压力单位
        /// </summary>
        /// <returns></returns>
        public bool SetPressureUnit_IPM()
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            iResponse setInnerModulePressureUnit = HPC.SetPressureUnit_IPM(Xmas11.Domain.Mechanics.PressureUnit.kPa);
            if (!setInnerModulePressureUnit.IsCorrect)
            {
                FileHelper.SaveTxtFile(setInnerModulePressureUnit.GetContent(true, true));
            }
            return setInnerModulePressureUnit.IsCorrect;
        }
        /// <summary>
        /// 设置内部模块压力单位
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public bool SetPressureUnit_IPM(string unit)
        {

            Xmas11.Domain.Unit pressureUnit = Xmas11.Domain.Mechanics.PressureUnit.Parse(unit);
            iResponse setInnerModulePressureUnit = HPC.SetPressureUnit_IPM(pressureUnit);
            return setInnerModulePressureUnit.IsCorrect;
        }

        /// <summary>
        /// 获取正压传感器校准状态
        /// </summary>
        /// <param name="message"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetSupplySensorCalibrationState(out Xmas11.Comm.Data.HPC.SensorCaliMessage message, out Xmas11.Comm.Data.HPC.TestState state)
        {
            message = new Xmas11.Comm.Data.HPC.SensorCaliMessage();
            state = new Xmas11.Comm.Data.HPC.TestState();
            iResponse<Xmas11.Comm.Data.HPC.TestState> getSensorCalibrationState = HPC.GetSensorCalibrationState(Xmas11.Comm.Data.HPC.SensorCalibrationItem.SupplySensorCali, out message);
            if (getSensorCalibrationState.IsCorrect)
                state = getSensorCalibrationState.Result;
            return getSensorCalibrationState.IsCorrect;
        }
        /// <summary>
        /// 校准正气压传感器
        /// </summary>
        /// <returns></returns>
        public bool SensorSupplyCalibration()
        {
            Xmas11.Comm.Devices.iResponse sensorCalibration = HPC.SensorCalibration(Xmas11.Comm.Data.HPC.SensorCalibrationItem.SupplySensorCali);
            return sensorCalibration.IsCorrect;
        }
        /// <summary>
        /// 获取正压气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetSupplyPressure(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getSupplyPressure = HPC.GetSupplyPressure();
            if (getSupplyPressure.IsCorrect)
            {
                pressure = getSupplyPressure.Result;
            }
            return getSupplyPressure.IsCorrect;
        }
        /// <summary>
        /// 获取正压气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetSupplyPressure_KVP(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            var strres = "获取正压气源压力";
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getSupplyPressure = HPC.GetSupplyPressure();
            if (getSupplyPressure.IsCorrect)
            {
                pressure = getSupplyPressure.Result;
            }
            return new ScriptHelperKVP(strres+(getSupplyPressure.IsCorrect?pressure.ToString():""), getSupplyPressure.IsCorrect);
        }
        /// <summary>
        /// 设定正压传感器校准时间
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetSupplySensorTime(DateTime time)
        {
            iResponse setSensorTime = HPC.SetSensorTime(time, Xmas11.Comm.Data.HPC.SensorCalibrationItem.SupplySensorCali);
            return setSensorTime.IsCorrect;
        }
        /// <summary>
        /// 获取真空传感器校准状态
        /// </summary>
        /// <param name="message"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetVacuumSensorCalibrationState(out Xmas11.Comm.Data.HPC.SensorCaliMessage message, out Xmas11.Comm.Data.HPC.TestState state)
        {
            message = new Xmas11.Comm.Data.HPC.SensorCaliMessage();
            state = new Xmas11.Comm.Data.HPC.TestState();
            iResponse<Xmas11.Comm.Data.HPC.TestState> getSensorCalibrationState = HPC.GetSensorCalibrationState(Xmas11.Comm.Data.HPC.SensorCalibrationItem.VacuumSensorCali, out message);
            if (getSensorCalibrationState.IsCorrect)
                state = getSensorCalibrationState.Result;
            return getSensorCalibrationState.IsCorrect;
        }
        /// <summary>
        /// 校准真空气源传感器
        /// </summary>
        /// <returns></returns>
        public bool SensorVacuumCalibration()
        {
            Xmas11.Comm.Devices.iResponse sensorCalibration = HPC.SensorCalibration(Xmas11.Comm.Data.HPC.SensorCalibrationItem.VacuumSensorCali);
            return sensorCalibration.IsCorrect;
        }
        /// <summary>
        /// 获取真空气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetVacuumPressure_KVP(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            var strres = "获取真空气源压力";
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return new ScriptHelperKVP(strres,false);
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getVacuumPressure = HPC.GetVacuumPressure();
            if (getVacuumPressure.IsCorrect)
            {
                pressure = getVacuumPressure.Result;
            }
            if (!getVacuumPressure.IsCorrect)
            {
                FileHelper.SaveTxtFile(getVacuumPressure.GetContent(true, true));
            }
            return new ScriptHelperKVP(strres+(getVacuumPressure.IsCorrect?pressure.ToString():""), getVacuumPressure.IsCorrect);
        }

        /// <summary>
        /// 获取真空气源压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetVacuumPressure(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getVacuumPressure = HPC.GetVacuumPressure();
            if (getVacuumPressure.IsCorrect)
            {
                pressure = getVacuumPressure.Result;
            }
            if (!getVacuumPressure.IsCorrect)
            {
                FileHelper.SaveTxtFile(getVacuumPressure.GetContent(true, true));
            }
            return getVacuumPressure.IsCorrect;
        }
        /// <summary>
        /// 设定真空传感器校准时间
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetVacuumSensorTime(DateTime time)
        {
            iResponse setSensorTime = HPC.SetSensorTime(time, Xmas11.Comm.Data.HPC.SensorCalibrationItem.VacuumSensorCali);
            return setSensorTime.IsCorrect;
        }
        /// <summary>
        /// 读取电机温度
        /// </summary>
        /// <param name="temperature"></param>
        /// <returns></returns>
        public bool GetMotor_Temperature(out double temperature)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                temperature = 0.0;
                return false;
            }
            Xmas11.Comm.Devices.iResponse<double> getMotor_Temperature = HPC.GetMotor_Temperature();
            if (getMotor_Temperature.IsCorrect)
            {
                temperature = getMotor_Temperature.Result;
                return true;
            }
            if (!getMotor_Temperature.IsCorrect)
            {
                FileHelper.SaveTxtFile(getMotor_Temperature.GetContent(true, true));
            }
            temperature = double.NaN;
            return false;
        }
        /// <summary>
        /// 读取电机温度
        /// </summary>
        /// <param name="temperature"></param>
        /// <returns></returns>
        public ScriptHelperKVP GetMotor_Temperature_KVP(out double temperature)
        {
            var strRes = "810读取电机温度";

            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                temperature = 0.0;
                return new ScriptHelperKVP(strRes + "失败", false);
            }
            Xmas11.Comm.Devices.iResponse<double> getMotor_Temperature = HPC.GetMotor_Temperature();
            if (getMotor_Temperature.IsCorrect)
            {
                temperature = getMotor_Temperature.Result;
                strRes += getMotor_Temperature.ToString();
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            if (!getMotor_Temperature.IsCorrect)
            {
                FileHelper.SaveTxtFile(getMotor_Temperature.GetContent(true, true));
            }
            temperature = double.NaN;
            return new ScriptHelperKVP(strRes + "失败", false);
        }
        /// <summary>
        /// 读取输出压力的设定点上限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureControlRange_UpperLimit(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure(0, PressureUnit.kPa);
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getOutPressureSetPointToplimit = HPC.GetPressureControlRange_UpperLimit();
            if (getOutPressureSetPointToplimit.IsCorrect)
            {
                pressure = getOutPressureSetPointToplimit.Result;
            }
            if (!getOutPressureSetPointToplimit.IsCorrect)
            {
                FileHelper.SaveTxtFile(getOutPressureSetPointToplimit.GetContent(true, true));
            }
            return getOutPressureSetPointToplimit.IsCorrect;
        }
        /// <summary>
        /// 读取输出压力的设定点下限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureControlRange_LowerLimit(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure(0, PressureUnit.kPa);
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getOutPressureSetPointLowerlimit = HPC.GetPressureControlRange_LowerLimit();
            if (getOutPressureSetPointLowerlimit.IsCorrect)
            {
                pressure = getOutPressureSetPointLowerlimit.Result;
            }
            return getOutPressureSetPointLowerlimit.IsCorrect;
        }
        /// <summary>
        /// 读取压力控制量程范围
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureControlRange(out Xmas11.Domain.Mechanics.PressureRange pressure)
        {
            pressure = new PressureRange();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.PressureRange> getPressureControlRange = HPC.GetPressureControlRange();
            if (getPressureControlRange.IsCorrect)
            {
                pressure = getPressureControlRange.Result;
            }
            return getPressureControlRange.IsCorrect;
        }
        /// <summary>
        /// 正压泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestPositivePump()
        {
            Xmas11.Comm.Devices.iResponse testPump = HPC.TestPump(Xmas11.Comm.Data.HPC.PumpTestItem.Positive);
            return testPump.IsCorrect;
        }
        public ScriptHelperKVP SetPumpOpenCloseState(OpenCloseState state)
        {
            var strRes = "设置泵打开关闭状态" + state;
            var res = HPC.SetPumpOpenCloseState(state);
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 正压泵测试
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP TestPositivePump_KVP()
        {
            var strRes = "810正压泵测试";

            Xmas11.Comm.Devices.iResponse testPump = HPC.TestPump(Xmas11.Comm.Data.HPC.PumpTestItem.Positive);
            if (testPump.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 负压泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestNegativePump()
        {
            Xmas11.Comm.Devices.iResponse testPump = HPC.TestPump(Xmas11.Comm.Data.HPC.PumpTestItem.Negative);
            return testPump.IsCorrect;
        }
        /// <summary>
        /// 终止泵测试
        /// </summary>
        /// <returns></returns>
        public bool TestPumpStop()
        {
            Xmas11.Comm.Devices.iResponse testPumpStop = HPC.TestPump(Xmas11.Comm.Data.HPC.PumpTestItem.Stop);
            return testPumpStop.IsCorrect;
        }
        /// <summary>
        /// 终止泵测试
        /// </summary>
        /// <returns></returns>
        public ScriptHelperKVP TestPumpStop_KVP()
        {
            var strRes = "810终止泵测试";

            Xmas11.Comm.Devices.iResponse testPumpStop = HPC.TestPump(Xmas11.Comm.Data.HPC.PumpTestItem.Stop);
            if (testPumpStop.IsCorrect)
            {
                return new ScriptHelperKVP(strRes + "成功", true);
            }
            else
            {
                return new ScriptHelperKVP(strRes + "失败", false);
            }
        }
        /// <summary>
        /// 气泵测试状态
        /// </summary>
        /// <param name="testState"></param>
        /// <returns></returns>
        public bool GetPumpTestState(out Xmas11.Comm.Data.HPC.TestState testState)
        {
            testState = new Xmas11.Comm.Data.HPC.TestState();
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.TestState> getPumpTestState = HPC.GetPumpTestState();
            if (getPumpTestState.IsCorrect)
            {
                testState = getPumpTestState.Result;
            }
            if (!getPumpTestState.IsCorrect)
            {
                FileHelper.SaveTxtFile(getPumpTestState.GetContent(true, true));
            }
            return getPumpTestState.IsCorrect;
        }
        /// <summary>
        /// 获取内部模块压力
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressure_IPM(out Xmas11.Domain.Mechanics.Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure();
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getInternalModulePressure = HPC.GetPressure_IPM();
            if (getInternalModulePressure.IsCorrect)
            {
                pressure = getInternalModulePressure.Result;
            }
            if (!getInternalModulePressure.IsCorrect)
            {
                FileHelper.SaveTxtFile(getInternalModulePressure.GetContent(true, true));
            }
            return getInternalModulePressure.IsCorrect;
        }
        /// <summary>
        /// 获取泵的电流值
        /// </summary>
        /// <param name="pumpCurrent"></param>
        /// <returns></returns>
        public bool GetPumpCurrent(out double pumpCurrent)
        {
            Xmas11.Comm.Devices.iResponse<double> getPumpCurrent = HPC.GetPumpCurrent();
            if (getPumpCurrent.IsCorrect)
            {
                pumpCurrent = getPumpCurrent.Result;
                return true;
            }
            pumpCurrent = double.NaN;
            return false;
        }
        /// <summary>
        /// 启动自整定
        /// </summary>
        /// <returns></returns>
        public bool StartSelfTuning()
        {
            Xmas11.Comm.Devices.iResponse startSelfTuning = HPC.StartSelfTuning();
            return startSelfTuning.IsCorrect;
        }
        /// <summary>
        /// 停止自整定
        /// </summary>
        /// <returns></returns>
        public bool StopSelfTuning()
        {
            Xmas11.Comm.Devices.iResponse stopSelfTuning = HPC.StopSelfTuning();
            return stopSelfTuning.IsCorrect;
        }
        /// <summary>
        /// 读取自整定状态
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool GetSelfTuningState(out Xmas11.Comm.Data.HPC.SelfTuningMessage message, out Xmas11.Comm.Data.HPC.TestState state)
        {
            message = new Xmas11.Comm.Data.HPC.SelfTuningMessage();
            state = new Xmas11.Comm.Data.HPC.TestState();
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.TestState> getSelfTuningState = HPC.GetSelfTuningState(out message);

            if (getSelfTuningState.IsCorrect)
            {
                state = getSelfTuningState.Result;
                return true;
            }
            if (!getSelfTuningState.IsCorrect)
            {
                FileHelper.SaveTxtFile(getSelfTuningState.GetContent(true, true));
            }
            return getSelfTuningState.IsCorrect;
        }
        /// <summary>
        /// 设定自整定时间
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool SetSelfTuningTime(DateTime time)
        {
            Xmas11.Comm.Devices.iResponse setSelfTuningTime = HPC.SetSelfTuningTime(time);
            return setSelfTuningTime.IsCorrect;
        }
        /// <summary>
        /// 设置控压速率
        /// </summary>
        /// <param name="pressureRate"></param>
        /// <returns></returns>
        public bool SetPressureRate(Xmas11.Domain.Mechanics.Pressure pressureRate)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse setPressureRate = HPC.SetPressureRate(pressureRate);
            if (!setPressureRate.IsCorrect)
            {
                FileHelper.SaveTxtFile(setPressureRate.GetContent(true, true));
            }
            return setPressureRate.IsCorrect;
        }
        /// <summary>
        /// 获取控压速率上限
        /// </summary>
        /// <param name="pressureRateUpper"></param>
        /// <returns></returns>
        public bool GetPressureRateUpper(out Xmas11.Domain.Mechanics.Pressure pressureRateUpper)
        {
            pressureRateUpper = new Pressure(10, PressureUnit.kPa);
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> result = HPC.GetPressureRateUpper();
            if (result.IsCorrect)
            {
                pressureRateUpper = result.Result;
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置控压速率最大
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRateToMax()
        {
            Xmas11.Domain.Mechanics.Pressure pressureRateUpper = new Pressure(10, PressureUnit.kPa);
            ;
            GetPressureRateUpper(out pressureRateUpper);
            return SetPressureRate(pressureRateUpper);
        }
        /// <summary>
        /// 设置控压速率类型-810A不可用
        /// </summary>
        /// <param name="pressureRateType"></param>
        /// <returns></returns>
        public bool SetPressureRateType(Xmas11.Comm.Data.HPC.PressureRateType pressureRateType)
        {
            Xmas11.Comm.Devices.iResponse setPressureRate = HPC.SetPressureRateType(pressureRateType);
            return setPressureRate.IsCorrect;
        }
        /// <summary>
        /// 设置控压速率类型最大
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRateTypeMax()
        {
            return SetPressureRateType(Xmas11.Comm.Data.HPC.PressureRateType.Max);
        }
        /// <summary>
        /// 设置压力稳定度
        /// </summary>
        /// <param name="pressureStabilityValue"></param>
        /// <returns></returns>
        public bool SetPressureStability(double pressureStabilityValue)
        {
            Xmas11.Comm.Devices.iResponse setPressureStability = HPC.SetPressureStability(pressureStabilityValue);
            return setPressureStability.IsCorrect;
        }
        /// <summary>
        /// 设定目标压力并输出
        /// </summary>
        /// <param name="setInnerPressure"></param>
        /// <returns></returns>
        public bool SetTargetPressure(Xmas11.Domain.Mechanics.Pressure setInnerPressure)
        {
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse setPressure = HPC.SetTargetPressure(setInnerPressure);
            if (!setPressure.IsCorrect)
            {
                FileHelper.SaveTxtFile(setPressure.GetContent(true, true));
            }
            return setPressure.IsCorrect;
        }
        /// <summary>
        /// 获取设定目标压力
        /// </summary>
        /// <param name="targetPressure"></param>
        /// <returns></returns>
        public bool GetTargetPressure(out Xmas11.Domain.Mechanics.Pressure targetPressure)
        {
            targetPressure = Pressure.Empty;
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> result = HPC.GetTargetPressure();
            if (result.IsCorrect)
            {
                targetPressure = result.Result;
            }
            return result.IsCorrect;
        }
        /// <summary>
        /// 读取压力稳定状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetPressureStableState(out Xmas11.Comm.Data.Common.PressureStableState state)
        {
            state = Xmas11.Comm.Data.Common.PressureStableState.UnKnown;
            if (!this.IsOpen)
            {
                FileHelper.SaveTxtFile("连接失败");
                return false;
            }
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.Common.PressureStableState> getPressureStableState = HPC.GetPressureStableState();
            if (getPressureStableState.IsCorrect)
            {
                state = getPressureStableState.Result;
                return true;
            }
            if (!getPressureStableState.IsCorrect)
            {
                FileHelper.SaveTxtFile(getPressureStableState.GetContent(true, true));
            }
            return false;
        }
        public bool GetPumpState(out Xmas11.Comm.Data.HPC.PumpState state)
        {
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.HPC.PumpState> getPumpState = HPC.GetPumpState();
            if (getPumpState.IsCorrect)
            {
                state = getPumpState.Result;
                return true;
            }
            state = Xmas11.Comm.Data.HPC.PumpState.Unknown;
            return false;
        }
        /// <summary>
        /// 读取内部模块的量程上限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureUpper_IPM(out Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure(0, PressureUnit.kPa);
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getInnerModulePressureUpper = HPC.GetPressureUpper_IPM();
            if (getInnerModulePressureUpper.IsCorrect)
            {
                pressure = getInnerModulePressureUpper.Result;
            }
            return getInnerModulePressureUpper.IsCorrect;
        }

        /// <summary>
        /// 读取内部模块的量程
        /// </summary>
        /// <param name="pressureRange"></param>
        /// <returns></returns>
        public bool GetPressureRange_IPM(out PressureRange pressureRange)
        {
            pressureRange = new PressureRange();
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.PressureRange> result = HPC.GetPressureRange_IPM();
            if (result.IsCorrect)
            {
                pressureRange = result.Result;
            }
            return result.IsCorrect;
        }


        /// <summary>
        /// 设定排空压力并输出
        /// </summary>
        /// <param name="setInnerPressure"></param>
        /// <returns></returns>
        public bool SetVentPressure(Xmas11.Domain.Mechanics.Pressure ventPressure, out Xmas11.Domain.Mechanics.Pressure setVent)
        {
            if (ventPressure > new Pressure(2000, PressureUnit.kPa))
            {
                ventPressure = new Pressure(2000, PressureUnit.kPa);
            }
            Xmas11.Comm.Devices.iResponse setVentPressure = HPC.SetVentPressure(ventPressure);
            if (setVentPressure.IsCorrect)
            {
                setVent = ventPressure;
                return true;
            }

            if (ventPressure > new Pressure(250, PressureUnit.kPa))
            {
                ventPressure = new Pressure(250, PressureUnit.kPa);
            }
            setVentPressure = HPC.SetVentPressure(ventPressure);
            if (setVentPressure.IsCorrect)
            {
                setVent = ventPressure;
                return true;
            }

            if (ventPressure > new Pressure(25, PressureUnit.kPa))
            {
                ventPressure = new Pressure(25, PressureUnit.kPa);
            }
            setVentPressure = HPC.SetVentPressure(ventPressure);
            setVent = ventPressure;
            return setVentPressure.IsCorrect;

        }
        /// <summary>
        /// 设置排空压力
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetVentPressureValue(double value)
        {
            if (value > 2000)
            {
                value = 2000;
            }
            Xmas11.Comm.Devices.iResponse setVentPressure = HPC.SetVentPressure(value);
            if (setVentPressure.IsCorrect)
            {
                return true;
            }

            if (value > 250)
            {
                value = 250;
            }
            setVentPressure = HPC.SetVentPressure(value);
            if (setVentPressure.IsCorrect)
            {
                return true;
            }

            if (value > 25)
            {
                value = 25;
            }
            setVentPressure = HPC.SetVentPressure(value);

            return setVentPressure.IsCorrect;
        }
        /// <summary>
        /// 读取当前控制状态
        /// </summary>
        /// <param name="controlMode"></param>
        /// <returns></returns>
        public bool GetPressureControlModeEX(out Xmas11.Comm.Data.Common.PressureControlMode controlMode)
        {
            Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.Common.PressureControlMode> getPressureControlMode = HPC.GetPressureControlModeEX();
            if (getPressureControlMode.IsCorrect)
            {
                controlMode = getPressureControlMode.Result;
                return true;
            }
            controlMode = Xmas11.Comm.Data.Common.PressureControlMode.UnKnown;
            return false;
        }
        //public bool GetPressureStableState(out Xmas11.Comm.Data.Common.PressureStableState stableState)
        //{
        //    stableState = Xmas11.Comm.Data.Common.PressureStableState.UnKnown;
        //    Xmas11.Comm.Devices.iResponse<Xmas11.Comm.Data.Common.PressureStableState> getPressureStableState = HPC.GetPressureStableState();
        //    if (getPressureStableState.IsCorrect)
        //    {
        //        stableState = getPressureStableState.Result;
        //        return true;
        //    }
        //    return false;
        //}


        /// <summary>
        /// 设定阀状态
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetValveStata(int value)
        {
            Xmas11.Comm.Devices.iResponse setValveStata = HPC.SetValveStata(value);
            return setValveStata.IsCorrect;
        }
        /// <summary>
        /// 获取阀状态
        /// </summary>
        /// <param name="stateValue"></param>
        /// <returns></returns>
        public bool GetValveStateValue(out int stateValue)
        {
            Xmas11.Comm.Devices.iResponse<int> getValveStateValue = HPC.GetValveStateValue();
            if (getValveStateValue.IsCorrect)
            {
                stateValue = getValveStateValue.Result;
                return true;
            }
            stateValue = 0;
            return false;
        }
        /// <summary>
        /// 获取内部模块压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetPressureType_IPM(out string pressureType)
        {
            iResponse<PressureType> result = HPC.GetPressureType_IPM();
            if (!result.IsCorrect)
            {
                pressureType = string.Empty;
                return false;
            }
            pressureType = result.Result.ToString();
            return true;
        }
        /// <summary>
        /// 设置内部模块压力类型(只支持表压)
        /// </summary>
        /// <returns></returns>
        public bool SetPressureType_IPM()
        {
            return HPC.SetPressureType_IPM(PressureType.G).IsCorrect;
        }
        /// <summary>
        /// 设置显示位宽
        /// </summary>
        /// <returns></returns>
        public bool SetDisplayWidth_IPM(int num)
        {
            return HPC.SetDisplayWidth_IPM(num).IsCorrect;
        }
        /// <summary>
        /// 获取机器当前语言
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public bool GetLanguageSource(out string language)
        {
            iResponse<string> result = HPC.GetLanguageSource();
            if (!result.IsCorrect)
            {
                language = string.Empty;
                return false;
            }
            language = result.Result;
            return true;
        }

        /// <summary>
        /// 获取机器所有可用语言
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public bool GetLanguages(out string languages)
        {
            iResponse<string> result = HPC.GetLanguages();
            if (!result.IsCorrect)
            {
                languages = string.Empty;
                return false;
            }
            languages = result.Result;
            return true;
        }

        /// <summary>
        /// 获取功能开启状态
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public bool GetFunctionState(out List<bool> states)
        {
            iResponse<List<bool>> result = HPC.GetFunctionState(Xmas11.Comm.Data.HPC.FunctionType.ALL);
            if (!result.IsCorrect)
            {
                states = new List<bool>();
                return false;
            }
            states = result.Result;
            return true;
        }
        /// <summary>
        /// 获取量程限制开启状态
        /// </summary>
        /// <param name="isOpen"></param>
        /// <returns></returns>
        public bool GetRangeLimitState(out bool isOpen)
        {
            Xmas11.Comm.Devices.iResponse<OpenCloseState> result = HPC.GetRangeLimitsState();
            if (result.IsCorrect)
            {
                if (result.Result == OpenCloseState.Open)
                {
                    isOpen = true;
                    return true;
                }
                else if (result.Result == OpenCloseState.Close)
                {
                    isOpen = false;
                    return true;
                }
            }
            isOpen = false;
            return false;
        }
        /// <summary>
        /// 设置启动量程限制
        /// </summary>
        /// <returns></returns>
        public bool SetOpenRangeLimitsState()
        {
            Xmas11.Comm.Devices.iResponse setRangeLimitsState = HPC.SetRangeLimitsState(OpenCloseState.Open);
            if (setRangeLimitsState.IsCorrect)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取当前设备量程限制
        /// </summary>
        /// <param name="deviceType"></param>
        /// <param name="lowLimit"></param>
        /// <param name="highLimit"></param>
        /// <returns></returns>
        public bool GetModuleRangeLimeits(out string deviceType, out float lowLimit, out float highLimit)
        {
            Xmas11.Comm.Devices.iResponse<List<Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew>> result = HPC.GetModuleRangeLimitsNew(Xmas11.Comm.Data.HPC.DeviceModel.CURRENT);
            if (result.IsCorrect)
            {
                deviceType = result.Result[0].DeviceType;
                lowLimit = result.Result[0].LowLimit;
                highLimit = result.Result[0].HighLimit;
                return true;
            }
            deviceType = string.Empty;
            lowLimit = float.NaN;
            highLimit = float.NaN;
            return false;
        }
        /// <summary>
        /// 重启设备
        /// </summary>
        /// <returns></returns>
        public bool Reset()
        {
            var result = HPC.Reset();
            return result.IsCorrect;
        }
        /// <summary>
        /// 恢复出厂设置
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool ResetFactoryByManufactor()
        {
            //return HPC.FactoryReset().IsCorrect;
            var result = HPC.FactoryReset();
            return result.IsCorrect;
        }

        #region 升级
        /// <summary>
        /// 是否可升级
        /// </summary>
        /// <returns></returns>
        public override bool IsUpgradable()
        {
            this.GetDevType(out string devType);
            string path = string.Empty;
            if (this.DUT.DetailType.Contains("810A") || devType.Contains("810A"))
            {
                path = UpgradeFile.LocalCacheRoot + @"/HPC/OS/UpgradeSetting810A.xml";
            }
            else
            {
                path = UpgradeFile.LocalCacheRoot + @"/HPC/OS/UpgradeSetting.xml";
            }

            this.LoadUpgradeSetting(path);
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
            if (this.CommConfig is USBConfig)
            {
                if (this.IsOpen)
                {
                    FileHelper.SaveTxtFile("连接失败");
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
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg2));
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

            code = code.Replace("?", "").Replace(" ", "");
            if (string.IsNullOrEmpty(code))
            {
                if (DUT != null)
                {
                    string dutSN = this.DUT.DeviceCode.Trim();
                    if (!string.IsNullOrEmpty(dutSN))
                    {
                        var info = MeterERP_HPCBaseInfoDAO.GetHPCBaseInfo(dutSN);
                        if (info != null)
                        {
                            SetSerialNumber(dutSN);
                        }
                    }
                }

            }
            GetSerialNumber(out code);

            MainInfo codeInfo2 = new MainInfo();
            codeInfo2.Key = "Code";
            codeInfo2.Name = Bots.TestBench.Device.Base.Properties.Resources.SerialNumber;
            codeInfo2.Info = code;
            if (UpgradeInfo.MainInfoIsContains(codeInfo2))
            {
                UpgradeInfo.MainInfoDic["Code"].Info = codeInfo2.Info;
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
                VersionInfo info = new VersionInfo();
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
                VersionInfo info = new VersionInfo();
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
                VersionInfo info = new VersionInfo();
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
                        string controllerClassCode = string.Empty;
                        string code = "";
                        GetSerialNumber(out code);
                        code = code.Replace("?", "").Replace(" ", "");
                        if (!string.IsNullOrEmpty(code))
                        {
                            var deviceInfo = MeterERP_HPCBaseInfoDAO.GetHPCBaseInfo(code);
                            if (deviceInfo != null)
                            {
                                if (deviceInfo.Unit.ToLower().Contains("mpa"))
                                    controllerClassCode = "ConST810MP";
                                else if (deviceInfo.URV > 50)
                                    controllerClassCode = "ConST810DP";
                                else
                                    controllerClassCode = "ConST810LLP";
                            }
                        }
                        else
                        {
                            //如果没有编号，需要用到控压范围
                            GetPressureRange_IPM(out Xmas11.Domain.Mechanics.PressureRange pressure);
                            if (pressure.Upper.ToUnit(PressureUnit.kPa).Value == 10)
                                controllerClassCode = "ConST810LLP";
                            else if (pressure.Upper.ToUnit(PressureUnit.kPa).Value > 2000)
                                controllerClassCode = "ConST810MP";
                            else
                                controllerClassCode = "ConST810DP";
                        }

                        string VersionController = null;
                        if (string.IsNullOrEmpty(controllerClassCode))
                        {
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
                        }
                        else
                        {
                            if (controllerClassCode.Contains("DP"))
                            {
                                VersionController = "DP";
                            }
                            else if (controllerClassCode.Contains("LLP"))
                            {
                                VersionController = "LLP";
                            }
                            else if (controllerClassCode.Contains("MP"))
                            {
                                VersionController = "MP";
                            }
                            else
                            {
                                VersionController = "MP";
                            }
                        }
                        var mc = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains(VersionController)).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(mc))
                        {
                            UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion = mc;
                        }
                    }
                    if (UpgradeInfo.VersionInfoIsContains("ElectricBoard"))
                    {
                        var me = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains("ME")).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(me))
                        {
                            UpgradeInfo.VersionInfoDic["ElectricBoard"].UpgradeVersion = me;
                        }
                    }

                }
            }
            else
            {
                //如果没有升级文件，则清空升级版本信息
                if (UpgradeInfo.VersionInfoDic != null)
                {
                    foreach (var versionInfo in UpgradeInfo.VersionInfoDic.Values)
                    {
                        versionInfo.UpgradeVersion = string.Empty;
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
                SetDevType(UpgradeInfo.MainInfoDic["Type"].Info);
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
                this.UpgradeInfo.ClearUpgradeMsgs();
                DateTime startDT = DateTime.Now;
                DateTime stopDT = DateTime.Now;
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade, startDT.ToString()));
                int retryCount = 0;
                #region 1.将启动升级程序推送到设备中
                //将启动升级程序推送到设备中
                string UpdaterExE = @"FlashDisk\HPC\Tool.USBUpdater.exe";
                //获取设备类型，如果是810A，则使用新路径
                this.GetDevType(out string devType);
                if (this.DUT.DetailType.Contains("810A") || devType.Contains("810A"))
                {
                    UpdaterExE = @"FlashDisk\HPC\BOOT_A\Tool.USBUpdater.exe";
                }
                //升级逻辑修改，由原来如果升级文件存在，删除，从新上传，修改为，如果升级文件存在，则直接使用，不存在则上传 2025-10-30

                bool IsUpdaterExEExist = false;
                if (QueryFileExists(UpdaterExE, out IsUpdaterExEExist))
                {
                    if (IsUpdaterExEExist)
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("升级程序", "当前设备已存在升级程序,将直接进行升级"));
                        //DeleteFile(UpdaterExE);
                    }
                    else
                    {
                        UpgradeFile UpdateToolUpgradeFile = this.UpgradeSetting.GetUpgradeFile("UpdateTool");
                        if (UpdateToolUpgradeFile != null)
                        {
                            string targetFilePath = UpdateToolUpgradeFile.CachePath;
                            if (File.Exists(targetFilePath))
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInDownload));
                                this.UpgradeInfo.ProgressIsIndeterminate = false;
                                this.UpgradeInfo.ProgressMaximum = 100;
                                this.UpgradeInfo.ProgressMinimum = 0;
                                this.UpgradeInfo.ProgressValue = 0;
                                System.IO.FileStream fs = new System.IO.FileStream(targetFilePath, System.IO.FileMode.Open);
                                byte[] buffer = new byte[fs.Length];
                                fs.Read(buffer, 0, buffer.Length);
                                fs.Close();

                                int pLength = 1024 * 4;
                                int pTotalCount = buffer.Length / pLength + 1;

                                //for (int i = 0; i < pTotalCount; i++)
                                //{
                                //    byte[] data = null;
                                //    if ((i + 1) * pLength <= buffer.Length)
                                //    {
                                //        data = new byte[pLength];
                                //    }
                                //    else if (i * pLength == buffer.Length)
                                //    {
                                //        break;
                                //    }
                                //    else
                                //    {
                                //        data = new byte[buffer.Length - pLength * i];
                                //    }
                                //    Array.Copy(buffer, i * pLength, data, 0, data.Length);
                                //    if (SendDataToDevice(UpdaterExE, data))
                                //    {
                                //        this.UpgradeInfo.ProgressValue = (i * 1.0 / pTotalCount) * 100;
                                //    }
                                //    else
                                //    {
                                //        DeleteFile(UpdaterExE);
                                //        this.UpgradeInfo.ProgressValue = 0;
                                //    }
                                //}
                                int i = 0;
                                while (true)
                                {
                                    byte[] data = null;
                                    if ((i + 1) * pLength <= buffer.Length)
                                    {
                                        data = new byte[pLength];
                                    }
                                    else
                                    {
                                        data = new byte[buffer.Length - pLength * i];
                                    }
                                    Array.Copy(buffer, i * pLength, data, 0, data.Length);
                                    if (SendDataToDevice(UpdaterExE, data))
                                    {
                                        this.UpgradeInfo.ProgressValue = (i * 1.0 / pTotalCount) * 100;
                                        if (pLength * i + data.Length >= buffer.Length)
                                        {
                                            break;

                                        }
                                        else
                                        {
                                            i++;
                                        }
                                    }
                                    else
                                    {
                                        DeleteFile(UpdaterExE);
                                        this.UpgradeInfo.ProgressValue = 0;
                                        i = 0;
                                    }
                                }



                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeDownloadComplete));
                                this.UpgradeInfo.ProgressIsIndeterminate = true;
                            }
                            else
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileNonExistent));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }
                        }
                        else
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileConfigError));
                            this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                            return UpgradeInfo;
                        }
                    }

                }
                else
                {
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated3, stopDT.ToString()));
                    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    return UpgradeInfo;
                }
                if (RequestStopUpgrade)
                {
                    QuitUpdateProgram();
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated1, stopDT.ToString()));
                    return UpgradeInfo;
                }
                //启动升级程序
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgramInStart));
                retryCount = 0;
                while (true)
                {
                    if (RunProgram(UpdaterExE))
                    {
                        break;
                    }
                    retryCount++;
                    if (retryCount > 3)
                    {
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgramStartFailed, stopDT.ToString()));
                        return UpgradeInfo;
                    }
                }
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgramStartOk));

                #endregion
                if (RequestStopUpgrade)
                {
                    ReStart();
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated1, stopDT.ToString()));
                    return UpgradeInfo;
                }
                System.Threading.Tasks.Task.Delay(2000).Wait();

                #region 2.将升级包下载到设备中
                string UpgradeFileName = string.Empty;
                UpgradeFile mainUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();
                if (mainUpgradeFile != null)
                {
                    UpgradeFileName = System.IO.Path.GetFileName(mainUpgradeFile.CachePath);
                    string VersionController = null;
                    if (UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion.Contains("DP"))
                    {
                        VersionController = "DP";
                    }
                    else if (UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion.Contains("MP"))
                    {
                        VersionController = "MP";
                    }
                    else if (UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion.Contains("LLP"))
                    {
                        VersionController = "LLP";
                    }
                    else
                    {
                        VersionController = "MP";
                    }
                    string currentControlleHardVersion = mainUpgradeFile.Versions.Where(v => v.Key.Contains("Hardware") && v.Key.Contains(VersionController)).Select(v => v.Value).FirstOrDefault();

                    string currentControlleVersion = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains(VersionController)).Select(v => v.Value).FirstOrDefault();

                    string currentElectricityHardVersion = mainUpgradeFile.Versions.Where(v => v.Key.Contains("Hardware") && v.Key.Contains("ME")).Select(v => v.Value).FirstOrDefault();

                    string currentElectricityVersion = mainUpgradeFile.Versions.Where(v => !v.Key.Contains("Hardware") && v.Key.Contains("ME")).Select(v => v.Value).FirstOrDefault();

                    if (string.IsNullOrEmpty(UpgradeFileName))
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileGetFailed));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return this.UpgradeInfo;
                    }
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, UpgradeFileName + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileUpload));
                    retryCount = 0;
                    while (true)
                    {
                        if (IsReady())
                        {
                            break;
                        }
                        retryCount++;
                        if (retryCount > 10)
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInProcessError));
                            return this.UpgradeInfo;
                        }
                    }
                    retryCount = 0;
                    while (true)
                    {
                        if (SetParams(currentControlleHardVersion, currentControlleVersion, currentElectricityHardVersion, currentElectricityVersion))
                        {
                            break;
                        }
                        retryCount++;
                        if (retryCount > 3)
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInProcessError));
                            return this.UpgradeInfo;
                        }
                    }

                }
                else
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileGetFailed));
                    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    return UpgradeInfo;
                }
                System.Threading.Tasks.Task.Delay(5000).Wait();
                byte[] bufferFile = mainUpgradeFile.FileContent;

                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileUpload));
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;

                int packageLength = 1024 * 30;
                int packageTotalCount = bufferFile.Length / packageLength + 1;

                for (int i = 0; i < packageTotalCount; i++)
                {
                    byte[] data = null;
                    if ((i + 1) * packageLength <= bufferFile.Length)
                    {
                        data = new byte[packageLength];
                    }
                    else
                    {
                        data = new byte[bufferFile.Length - packageLength * i];
                    }

                    Array.Copy(bufferFile, i * packageLength, data, 0, data.Length);

                    UpdateTelegram telegram = new UpdateTelegram(0x65, data);
                    UpdateTelegram response = this.SendUpdateTelegram(telegram, 2000);
                    if (response.Status != UpdateTelegramStatus.OK)
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, Bots.TestBench.Device.Base.Properties.Resources.UpgradeError));
                        return this.UpgradeInfo;
                    }
                    this.UpgradeInfo.ProgressValue = (i * 1.0 / packageTotalCount) * 100;
                }
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileUploadComplete));
                this.UpgradeInfo.ProgressIsIndeterminate = true;

                #endregion

                if (RequestStopUpgrade)
                {
                    ReStart();
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated1, stopDT.ToString()));
                    return UpgradeInfo;
                }

                #region   3.开始升级
                retryCount = 0;
                while (true)
                {
                    if (UpdateStart())
                    {
                        break;
                    }
                    retryCount++;
                    if (retryCount > 3)
                    {
                        ReStart();
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProgram, Bots.TestBench.Device.Base.Properties.Resources.UpgradeInProcessError));
                        return UpgradeInfo;
                    }
                }
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeTakes4Minute));
                #endregion

                #region  4.等待开机
                System.Threading.Tasks.Task.Delay(2000).Wait();
                this.Close();
                int c = 0;
                while (true)
                {
                    if (this.Open())
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleRestart + "," + Bots.TestBench.Device.Base.Properties.Resources.UpgradeComplete));
                        break;
                    }
                    if (c > 180)
                    {
                        break;
                    }
                    c++;
                    System.Threading.Tasks.Task.Delay(1000).Wait();
                    if (RequestStopUpgrade)
                    {
                        ReStart();
                        stopDT = DateTime.Now;
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessTerminated1, stopDT.ToString()));
                        return UpgradeInfo;
                    }
                }
                if (!this.IsOpen)
                {
                    stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeProcessFailMsgTimeout, stopDT.ToString()));
                    return UpgradeInfo;
                }
                #endregion

                #region 5.设置语言
                retryCount = 0;
                while (true)
                {
                    //如果是810A，则按照810A的语言列表设置语言
                    if (devType.Contains("810A"))
                    {
                        if (SetMachineLanguages_810A())
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (SetMachineLanguage())
                        {
                            break;
                        }
                    }
                    retryCount++;
                    if (retryCount > 3)
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeSetLanguageFailed));
                        return UpgradeInfo;
                    }
                }
                #endregion

                #region 6.设置量程限制
                if (UpgradeInfo.VersionInfoDic["ControlBoard"].UpgradeVersion.Contains("HPC-LLP"))
                {
                    //this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, "重置量程限制"));
                    Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew rangeLimit = new Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew();
                    if (GetModuleRangeLimeitsNew(Xmas11.Comm.Data.HPC.DeviceModel.LLP, out rangeLimit))
                    {
                        if (rangeLimit.HighLimit != 10 || rangeLimit.LowLimit != 0.06)
                        {
                            SetRangeLimitsValue(Xmas11.Comm.Data.HPC.DeviceModel.LLP, 0.06, 10);
                        }
                    }

                }
                if (IsOnLine_IPM())
                {
                    //this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, "关闭量程限制"));
                    SetCloseRangeLimitsState();
                }
                #endregion

                stopDT = DateTime.Now;
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeComplete, stopDT.ToString()));
                RefreshCurrentVersion();
                this.UpgradeInfo.UpgradeResult = UpgradeResult.Succeed;
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
                if (this.UpgradeInfo.UpgradeResult == UpgradeResult.Succeed)
                {
                    this.GetUpgradeInfo();
                }
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
                                            byte[] buffer = new byte[0];
                                            using (System.IO.FileStream fs = new FileStream(upgradeFile.CachePath, System.IO.FileMode.Open))
                                            {
                                                buffer = new byte[fs.Length];
                                                fs.Read(buffer, 0, buffer.Length);
                                                fs.Close();
                                            }
                                            upgradeFile.FileContent = buffer;
                                            #region 获取升级包主程序版本
                                            //1.解压升级包
                                            ZipHelper zipHelper = new ZipHelper();
                                            string extractFolderName = System.IO.Path.GetDirectoryName(upgradeFile.CachePath) + "\\hpcUpdatePack";
                                            bool isExtract = zipHelper.Extract(upgradeFile.CachePath, "showmethemoney", extractFolderName);
                                            if (isExtract)
                                            {
                                                //2.查找解压文件中的ATC.exe
                                                string[] files = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Application"), "*.exe");
                                                string hpc_host = files.Where(f => f.Contains("HPC.exe")).FirstOrDefault();
                                                if (!string.IsNullOrEmpty(hpc_host))
                                                {
                                                    System.IO.FileInfo fileInfo = null;
                                                    try
                                                    {
                                                        fileInfo = new System.IO.FileInfo(hpc_host);
                                                    }
                                                    catch { }
                                                    // 如果文件存在
                                                    if (fileInfo != null && fileInfo.Exists)
                                                    {
                                                        System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(hpc_host);
                                                        string version = string.Format("HTC-HOST V{0}", info.ProductVersion);
                                                        upgradeFile.AddVersion("HTC-HOST", version);
                                                    }



                                                }
                                                #region 3.查找文件夹中控制板
                                                string[] fileController = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Controller"), "*.bin");
                                                foreach (string file in fileController)
                                                {
                                                    if (file.Contains("DP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "HPC-DP";
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
                                                        upgradeFile.AddVersion("HPC-DP", controllerVersion);
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
                                                        upgradeFile.AddVersion("HPC-DP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("MP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "HPC-MP";
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
                                                        upgradeFile.AddVersion("HPC-MP", controllerVersion);
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
                                                        upgradeFile.AddVersion("HPC-MP-Hardware", controllerHardwareVersion);
                                                    }
                                                    else if (file.Contains("LLP"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyControllerVersion = "HPC-LLP";
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
                                                        upgradeFile.AddVersion("HPC-LLP", controllerVersion);
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
                                                        upgradeFile.AddVersion("HPC-LLP-Hardware", controllerHardwareVersion);
                                                    }
                                                }

                                                #endregion

                                                #region 4.查找文件夹中电测板

                                                string[] fileElectricity = System.IO.Directory.GetFiles(string.Format(extractFolderName + "\\Electricity"), "*.bin");


                                                foreach (string file in fileElectricity)
                                                {
                                                    if (file.Contains("ME"))
                                                    {
                                                        int substringlength = 0;
                                                        string text = System.IO.File.ReadAllText(file);
                                                        string textsub = null;
                                                        string keyElectricityVersion = "HPC-ME";
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
                                                        upgradeFile.AddVersion("HPC-ME-Hardware", electricityHardwareVersion);
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
                                                        upgradeFile.AddVersion("HPC-ME", electricityVersion);
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
        /// 查询文件是否存在
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isExists"></param>
        /// <returns></returns>
        public bool QueryFileExists(string filePath, out bool isExists)
        {
            isExists = false;
            iResponse<bool> result = HPC.SearchFile(filePath);
            if (!result.IsCorrect)
            {
                isExists = result.Result;
            }
            return true;
        }

        /// <summary>
        /// 查询SD卡文件是否存在
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isExists"></param>
        /// <returns></returns>
        public bool QuerySDCardfileExists(string filePath, out bool isExists)
        {
            isExists = false;
            iResponse<bool> result = HPC.SearchStorageCardFile(filePath);
            if (!result.IsCorrect)
            {
                isExists = result.Result;
            }
            return true;
        }

        /// <summary>
        /// 设置机器语言
        /// </summary>
        /// <returns></returns>
        public bool SetMachineLanguage()
        {
            iResponse result = HPC.SetMachineLanguage();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        public bool SetMachineLanguages_810A()
        {
            iResponse respones = HPC.SetMachineLanguage_810A();
            if (!respones.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 返回量程范围
        /// </summary>
        /// <param name="model"></param>
        /// <param name="rangeLimit"></param>
        /// <returns></returns>
        public bool GetModuleRangeLimeitsNew(Xmas11.Comm.Data.HPC.DeviceModel model, out Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew rangeLimit)
        {
            rangeLimit = new Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew();
            iResponse<List<Xmas11.Comm.Data.HPC.ModuleRangeLimitsNew>> getModuleRangeLimitsNew = HPC.GetModuleRangeLimitsNew(model);
            if (getModuleRangeLimitsNew.IsCorrect)
            {
                rangeLimit = getModuleRangeLimitsNew.Result[0];
                return true;
            }
            return false;
        }
        /// <summary>
        /// 设置量程范围
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="low"></param>
        /// <param name="up"></param>
        /// <returns></returns>
        public bool SetRangeLimitsValue(Xmas11.Comm.Data.HPC.DeviceModel mode, double low, double up)
        {
            iResponse result = HPC.SetRangeLimitsValue(mode, low, up);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 内部模块是否在线
        /// </summary>
        /// <returns></returns>
        public bool IsOnLine_IPM()
        {
            iResponse result = HPC.IsOnLine_IPM();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        public bool SetCloseRangeLimitsState()
        {
            iResponse result = HPC.SetRangeLimitsState(OpenCloseState.Close);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 发送数据到设备
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="Data"></param>
        /// <returns></returns>
        public bool SendDataToDevice(string filePath, byte[] Data)
        {
            iResponse result = HPC.SendDataToDevice(filePath, Data);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 运行程序
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool RunProgram(string filePath)
        {

            iResponse result = HPC.RunProgram(filePath);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 组合控制板电测板版本 发送指令到设备 为了在升级的时候根据此区分设备类型等等
        /// </summary>
        /// <returns></returns>
        private bool SetParams(string hardVersion_Controller, string version_Controller, string hardVersion_Electricity, string version_Electricity)
        {
            //先获取版本号参数
            string packagePath = "\\Temp\\HPC.hpcupdate";
            string controllerFareWareVersion = string.Empty;
            if (version_Controller.Contains("LLP"))
            {
                controllerFareWareVersion = "HPC-LLP V00.00";
            }
            else if (version_Controller.Contains("DP"))
            {
                controllerFareWareVersion = "HPC-DP V00.00";
            }
            else if (version_Controller.Contains("MP"))
            {
                controllerFareWareVersion = "HPC-MP V00.00";
            }
            string arg = string.Format("IndependentUpdate=True;ServicePackagePath=\\Flashdisk\\HPC.update;ControllerHWVersion={0};ControllerFWVersion={1};ElectricityHWVersion={2};ElectricityFWVersion={3};LocalAppFilePath=\\Flashdisk\\HPC\\HPC.exe;ShowResult=false;NeedComfirm=false;", hardVersion_Controller, controllerFareWareVersion, hardVersion_Electricity, version_Electricity);
            if (this.SendUpdateTelegram(new UpdateTelegram(0x064, System.Text.ASCIIEncoding.ASCII.GetBytes(packagePath)), 2000).Status == UpdateTelegramStatus.OK
                && this.SendUpdateTelegram(new UpdateTelegram(0x063, System.Text.ASCIIEncoding.ASCII.GetBytes(arg)), 2000).Status == UpdateTelegramStatus.OK)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 退出升级程序
        /// </summary>
        /// <returns></returns>
        private bool QuitUpdateProgram()
        {
            try
            {
                UpdateTelegram response = SendUpdateTelegram(new UpdateTelegram(0x50, null), 5000);
                if (response.Status == UpdateTelegramStatus.OK)
                {
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 重启设备
        /// </summary>
        /// <returns></returns>
        private bool ReStart()
        {
            try
            {

                UpdateTelegram response = SendUpdateTelegram(new UpdateTelegram(0x51, null), 5000);
                if (response.Status == UpdateTelegramStatus.OK)
                {
                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                this.Close();
            }
            return false;

        }
        /// <summary>
        /// 发送升级指令
        /// </summary>
        /// <returns></returns>
        private bool UpdateStart()
        {
            if (SendUpdateTelegram(new UpdateTelegram(0x66, null), 2000).Status == UpdateTelegramStatus.OK)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 判断是否准备完毕
        /// </summary>
        /// <returns></returns>
        private bool IsReady()
        {
            try
            {
                UpdateTelegram response = SendUpdateTelegram(new UpdateTelegram(0x01, null), 5000);
                if (response.Status == UpdateTelegramStatus.OK)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
        /// <summary>
        /// 发送报文方法
        /// </summary>
        /// <param name="telegram">指令报文</param>
        /// <param name="timeOut">等待时间</param>
        /// <returns></returns>
        private UpdateTelegram SendUpdateTelegram(UpdateTelegram telegram, int timeOut)
        {
            HPC.CommInstance.ClearInBuffer();
            HPC.CommInstance.ClearOutBuffer();
            List<byte> buffer = new List<byte>();
            HPC.CommInstance.Write(telegram.Telegram, 0, telegram.Telegram.Length);
            int startTick = Environment.TickCount;
            while (true)
            {
                System.Threading.Thread.Sleep(10);
                if (HPC.CommInstance.Available > 0)
                {
                    byte[] data = null;
                    HPC.CommInstance.Read(out data);
                    buffer.AddRange(data);

                }

                if (buffer.Count >= 13 && buffer[0] == 0xAA && buffer[1] == 0xBB && buffer[2] == 0xCC && buffer[3] == 0xDD && buffer.Count == XmasCE.Conversion.IBitConverter.ToUInt16(buffer.ToArray(), 7, false) + 11)
                {
                    break;
                }

                if (Environment.TickCount - startTick > timeOut)
                    break;

            }
            return new UpdateTelegram(buffer.ToArray());
        }
        /// <summary>
        /// 获取DD库-810A(读取文件的方式)
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion_DD(out string version)
        {
            version = string.Empty;
            iResponse<string> response = HPC.GetVersion_DD_ByReadFile();
            if (!response.IsCorrect)
            {
                return false;
            }
            var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(response.Result);
            version = jsonObj["Version"]?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }
            return true;
        }
        #endregion

        #region 保存通讯日志

        #endregion

        #endregion Methods
        #region 新增810老化方法
        /// <summary>
        /// 读取内部模块的量程下限
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressureLowerer(out Pressure pressure)
        {
            pressure = new Xmas11.Domain.Mechanics.Pressure(0, PressureUnit.kPa);
            Xmas11.Comm.Devices.iResponse<Xmas11.Domain.Mechanics.Pressure> getInnerModulePressureLowerer = HPC.GetPressureLowerer_IPM();
            if (getInnerModulePressureLowerer.IsCorrect)
            {
                pressure = getInnerModulePressureLowerer.Result;
            }
            return getInnerModulePressureLowerer.IsCorrect;
        }
        /// <summary>
        /// 810A模块清零
        /// </summary>
        /// <returns></returns>
        public bool SetPressureZero_IPM()
        {
            iResponse result = HPC.SetPressureZero_IPM();
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }
        #endregion 新增810老化方法
    }
    public class UpdateTelegram
    {
        public static readonly byte[] HEADER = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };


        private byte[] _telegram = new byte[0];

        public byte[] Telegram
        {
            get
            {
                return _telegram;
            }
            private set
            {
                _telegram = value;
                this.status = UpdateTelegramStatus.None;
            }
        }


        public byte Control
        {
            get
            {
                return this.Telegram[4];
            }
        }

        public ushort Code
        {
            get
            {
                return (ushort)((this.Telegram[5] << 8 | this.Telegram[6]));
            }
        }


        public ushort Length
        {
            get
            {
                return (ushort)(this.Telegram.Length - 11);//包含错误码
                                                           // return (ushort)((this.Telegram[7] << 8 | this.Telegram[8]));
            }
        }

        public ushort CRC
        {
            get
            {
                return (ushort)((this.Telegram[this.Telegram.Length - 2] << 8) | this.Telegram[this.Telegram.Length - 1]);
            }
        }

        public byte[] Block
        {
            get
            {
                byte[] block = new byte[this.Length - 2];
                Array.Copy(this.Telegram, 11, block, 0, block.Length);
                return block;
            }
        }



        public int ErrorCode
        {
            get
            {
                if (this.Length >= 2)
                {
                    return XmasCE.Conversion.IBitConverter.ToUInt16(this.Telegram, 9, false);
                    ;
                }
                else
                {
                    return -1;
                }
            }
        }

        UpdateTelegramStatus status = UpdateTelegramStatus.None;

        public UpdateTelegramStatus Status
        {
            get
            {
                if (this.status == UpdateTelegramStatus.None)
                {
                    if (this.Telegram.Length >= 13 && ArrayCompare(this.Telegram, 0, HEADER, 0, HEADER.Length))
                    {
                        if ((this.Control & 0x02) >> 1 == 1)
                        {
                            byte[] crc_buf = new byte[this.Telegram.Length - 6];
                            Array.Copy(this.Telegram, 4, crc_buf, 0, crc_buf.Length);
                            ushort crc_sum = (ushort)(new Xmas11.Comm.Commander.CRCEntity(Xmas11.Comm.Commander.CRCCoding.CRC16CCITT).Sum(crc_buf)); //Calculate CRC-sum.
                            if (crc_sum != ((this.Telegram[this.Telegram.Length - 2] << 8) | this.Telegram[this.Telegram.Length - 1]))
                            {
                                status = UpdateTelegramStatus.CRCError;
                                return status;
                            }
                        }

                        if (ErrorCode == 0)  //Code
                        {
                            status = UpdateTelegramStatus.OK;//指令高位=0，指令正确，否则错误，数据区为错误码.
                        }
                        else
                        {
                            status = UpdateTelegramStatus.CError;//从设备正确影响后返回的错误，非异常.
                        }
                        //status = UpdateTelegramStatus.OK;
                    }
                    else
                    {
                        status = UpdateTelegramStatus.InvalidTelegram;
                    }
                }

                return status;
            }
        }

        public UpdateTelegram()
        {
        }



        public UpdateTelegram(ushort code, byte[] data)
            : this(0x02, code, data)
        {
        }

        public UpdateTelegram(byte control, ushort code, byte[] data)
            : this(control, code, 0, data)
        {
        }

        public UpdateTelegram(ushort code, ushort cError)
            : this(0, code, cError, null)
        {
        }

        public UpdateTelegram(byte control, ushort code, ushort cError, byte[] data)
        {
            List<byte> items = new List<byte>();
            //Header
            items.AddRange(HEADER);
            //Control
            items.Add(control);
            //Code(High + Low)
            items.Add((byte)(code >> 8));
            items.Add((byte)code);
            //DataLength
            int dataLength = data == null ? 0 + 2 : data.Length + 2;
            items.Add((byte)(dataLength >> 8));
            items.Add((byte)dataLength);
            //cError
            items.Add((byte)(cError >> 8));
            items.Add((byte)cError);
            //Data
            if (data != null)
            {
                items.AddRange(data);
            }
            //CRC
            byte[] array = new byte[dataLength + 5];
            Array.Copy(items.ToArray(), 4, array, 0, array.Length);
            ushort crc = (ushort)(new Xmas11.Comm.Commander.CRCEntity(Xmas11.Comm.Commander.CRCCoding.CRC16CCITT).Sum(array));
            items.Add((byte)(crc >> 8));
            items.Add((byte)crc);
            //
            this.Telegram = items.ToArray();
        }

        public UpdateTelegram(byte[] buffer)
        {
            if (buffer != null && buffer.Length >= 11)
            {
                this.Telegram = new byte[buffer.Length];
                Array.Copy(buffer, this.Telegram, buffer.Length);
            }
            else
            {
                this.Telegram = new byte[16];   //创建一个全0的无效报文.
            }

        }


        private static bool ArrayCompare(byte[] buffer1, int buffer1StartIndex, byte[] buffer2, int buffer2StartIndex, int size)
        {
            for (int i = 0; i < size; i++)
            {
                if (buffer1[buffer1StartIndex + i] != buffer2[buffer2StartIndex + i])
                    return false;
            }
            return true;
        }


        public static bool IsStatusOk(UpdateTelegram gram)
        {
            if (gram != null && gram.Status == UpdateTelegramStatus.OK)
                return true;
            else
                return false;
        }

        public static bool IsStatusCError(UpdateTelegram gram)
        {
            if (gram != null && gram.Status == UpdateTelegramStatus.CError)
                return true;
            else
                return false;
        }




    }
    public enum UpdateTelegramStatus
    {
        None = -1,
        OK = 0,                 //无错误
        CError = 1,             //正常返回错误+错误ID.
        InvalidTelegram = 2,    //报告错误(长度不够或无正确报头、报尾).
        CRCError = 3,           //CRC校验错误.
    }

}
