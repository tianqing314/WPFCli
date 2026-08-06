using Bots.TestBench.Device.Base.Comm;
using Bots.TestBench.Device.Properties;
using Bots.TestBench.Device.Upgrade;
using Bots.TestBench.Model.Scripts;
using Bots.TestBench.Util;
using Bots.TestBench.Util.CRC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.DPG2;
using Xmas11.Comm.Devices.DPG2.Data.Structs;
using Xmas11.Domain;
using Xmas11.Domain.Mechanics;
using Xmas11.Domain.Thermology;

namespace Bots.TestBench.Device
{
    public class ConST221_2 : UpgradeDevice
    {
        #region Ctors
        public ConST221_2()
        {
            this.DeviceType = Base.DeviceType.STD;
        }
        #endregion

        #region Properties
        public DPG2SCPI DPG2
        {
            get
            {
                return this.CommInstance as DPG2SCPI;
            }
        }

        /// <summary>
        /// 获取设备图片
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Bitmap GetDeviceMainImage()
        {
            return Resources.main;
        }

        /// <summary>
        /// 获取被检SN
        /// </summary>
        /// <returns></returns>
        public override string GetDUTSN()
        {
            string result = this.DUT.DeviceCode;
            if (this.DeviceKey.Contains("出厂"))
            {
                GetSerialNumber(out result);
            }
            else
            {
                GetComponentSN(out result);
            }
            return result;
        }
        #endregion

        #region Methods

        #region 通讯
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
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                AddressChanged();

                bool openResult = false;

                var cominfo = CommenHelper.GetComInfo().Where(o => (!o.name.ToUpper().Equals("COM1")) && !o.name.ToUpper().Equals("COM5")).ToList();
                var zqwl = cominfo.Where(w => !(w.name.Contains("Serial Port") || w.name.Contains("UPort") /*|| w.name.Contains("CP21")*/));
                if (zqwl == null)
                {
                    MessageBox.Show($"没有找到USB端口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    ConnectStatus = ConnectStatus.DisConnected;
                    return false;
                }
                //逐个打开
                foreach (var item in zqwl)
                {
                    SerialPortConfig S1Config = new SerialPortConfig();
                    S1Config.SPName = item.value;
                    if (this.CommConfig.Name == "Board")
                    {
                        S1Config.SPName = (this.CommConfig as SerialPortConfig).SPName;
                        S1Config.Bauds = 19200;
                        S1Config.StopBits = "Two";
                    }
                    this.CommInstance = new DPG2SCPI((S1Config).GetCommSettings());
                    try
                    {
                        openResult = this.CommInstance.Open();
                        //打开成功，判定是否是221
                        if (openResult)
                        {
                            if (this.CommConfig.Name != "Board")
                            {
                                var vtemp = DPG2.GetiVersion().Result.ToString();
                                if (this.CommInstance.IsExist())
                                {
                                    ConnectStatus = ConnectStatus.Connected;
                                    return true;
                                }

                            }
                            else
                            {
                                iResponse result = DPG2.ExecuteAnyCommand_NoResponse("255:W:PCDP:0");
                                if (result.IsCorrect)
                                {
                                    ConnectStatus = ConnectStatus.Connected;
                                    return true;
                                }
                            }
                            if (this.CommInstance != null)
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                            }
                        }
                        else
                        {
                            if (this.CommInstance != null)
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                            }
                        }
                    }
                    catch
                    {
                        if (this.CommInstance != null)
                        {
                            this.CommInstance.Close();
                            this.CommInstance = null;
                        }
                        openResult = false;
                    }
                }

                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            catch (Exception eX)
            {
                MessageBox.Show($"异常{eX.Message}{eX.StackTrace}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            //return FirstManualAfterAutoConnectSpDevice((st) => new DPG2SCPI(st), () => IsExist);
        }



        /// <summary>
        /// 更改串口重新打开
        /// </summary>
        /// <returns></returns>
        public bool ChangeComOpen()
        {
            ConnectStatus = ConnectStatus.Connectting;
            try
            {
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                AddressChanged();

                bool openResult = false;

                var cominfo = CommenHelper.GetComInfo();
                var zqwl = cominfo.Where(w => w.name.Contains("Serial Port") || w.name.Contains("UPort") || w.name.Contains("CP21"));
                if (zqwl == null)
                {
                    MessageBox.Show($"没有找到Serial Port端口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    ConnectStatus = ConnectStatus.DisConnected;
                    return false;
                }
                //逐个打开
                foreach (var item in zqwl)
                {
                    SerialPortConfig S1Config = new SerialPortConfig();
                    S1Config.SPName = item.value;

                    this.CommInstance = new DPG2SCPI((S1Config).GetCommSettings());
                    try
                    {
                        openResult = this.CommInstance.Open();
                        //打开成功，判定是否是221
                        if (openResult)
                        {
                            var vtemp = DPG2.GetiVersion().Result.ToString();
                            if (this.CommInstance.IsExist())
                            {
                                ConnectStatus = ConnectStatus.Connected;
                                return true;
                            }
                            if (this.CommInstance != null)
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                            }
                        }
                        else
                        {
                            if (this.CommInstance != null)
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                            }
                        }
                    }
                    catch
                    {
                        if (this.CommInstance != null)
                        {
                            this.CommInstance.Close();
                            this.CommInstance = null;
                        }
                        openResult = false;
                    }
                }

                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            catch (Exception eX)
            {
                MessageBox.Show($"异常{eX.Message}{eX.StackTrace}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
        }


        /// <summary>
        /// 获取信息(上一次通讯时保留的)
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return base.GetInfo();
        }
        #endregion

        #region 版本
        /// <summary>
        /// 获取版本号
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion(out string version)
        {
            version = string.Empty;
            iResponse<string> result = DPG2.GetVersion();
            if (!result.IsCorrect)
            {
                return false;
            }
            version = result.Result;
            return true;
        }

        /// <summary>
        /// 读取DPSEX硬件版本编号
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetDPSEXVersion(out string version)
        {
            version = string.Empty;
            iResponse<string> result = DPG2.GetDPSEXVersion();
            if (!result.IsCorrect)
            {
                return false;
            }
            version = result.Result;
            return true;
        }

        /// <summary>
        /// 读取蓝牙版本编号
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetBLEVersion(out string version)
        {
            version = string.Empty;
            iResponse<string> result = DPG2.GetVersion(VersionType.BT);
            if (!result.IsCorrect)
            {
                return false;
            }
            version = result.Result;
            return true;
        }
        #endregion


        #region 内置模块
        /// <summary>
        /// 获取SN
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        public bool GetPressuresn(out string sn)
        {
            //1:F:OCODE:DPSE021E00009
            sn = "";
            iResponse<string> response = DPG2.GetPressureSN();
            if (response.IsCorrect)
            {
                sn = response.Result;
                if (!string.IsNullOrWhiteSpace(sn.Trim()))
                {
                    var splicount = sn.Split(':');
                    if (splicount.Count() >= 4)
                    {
                        sn = splicount[3].Trim();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 获取量程与精度
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        public bool GetPressureLUV(out List<string> Range)
        {
            Range = new List<string>();
            try
            {
                //1:F:ORAN:     0.0000MPA:     1.0000MPA:A: 0
                //1:F:ORAN:     0.0000MPA:    25.0000MPA:A: 3
                iResponse<string> response = DPG2.GetPressureCAL();
                if (response.IsCorrect)
                {
                    string vtemp = response.Result;
                    if (!string.IsNullOrWhiteSpace(vtemp.Trim()))
                    {
                        var splicount = vtemp.Split(':');
                        if (splicount.Count() >= 7)
                        {
                            string Lv = splicount[3].Trim();
                            string Uv = splicount[4].Trim();
                            string PT = splicount[5].Trim();
                            string Vv = splicount[6].Trim();
                            string RegexStr = @"-?\d+.\d+";
                            string LV1 = Regex.Match(Lv, RegexStr).Value;
                            string UV1 = Regex.Match(Uv, RegexStr).Value;
                            double Vv1 = 0.0;

                            if (Vv == "1")
                            {
                                Vv1 = 0.002;
                            }
                            else if (Vv == "2")
                            {
                                Vv1 = 0.001;
                            }
                            else if (Vv == "3")
                            {
                                Vv1 = 0.0005;
                            }
                            else if (Vv == "4")
                            {
                                Vv1 = 0.0002;
                            }
                            else if (Vv == "5")
                            {
                                Vv1 = 0.00025;
                            }
                            else if (Vv == "101")
                            {
                                Vv1 = 0.0001;
                            }
                            else
                            {
                                Vv1 = 0.0;
                            }
                            Range.Add(LV1);
                            Range.Add(UV1);
                            Range.Add((Vv1 * 100).ToString());
                            Range.Add(Regex.Match(Uv, "[a-zA-Z]+").Value);
                            Range.Add(PT.ToUpper().Contains("A") ? "绝压" : "表压");

                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }


        /// <summary>
        /// 获取量程
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public bool GetPressureRange(out PressureRange range)
        {
            range = new PressureRange(0, 0, PressureUnit.kPa);
            iResponse<PressureRange> response = DPG2.GetPressureRange();
            if (response.IsCorrect)
            {
                range = response.Result;
                return true;
            }
            return false;
        }
        #endregion

        #region 主板动态测试指令
        public ScriptHelperKVP ExecuteAnyCommand_NoResponse(SimpleCommandEnum sce)
        {
            iResponse res = DPG2.ExecuteAnyCommand_NoResponse(SimpleCommands[sce]);
            if (res.IsCorrect)
            {
                return new ScriptHelperKVP("执行" + sce + "成功", true);
            }
            return new ScriptHelperKVP("执行" + sce + "失败", false);
        }
        public enum SimpleCommandEnum
        {
            CDP电源打开,
            CDP电源关闭,
            液晶屏电源打开,
            液晶屏电源关闭,
            触摸屏电源打开,
            触摸屏电源关闭,
            FRAM电源打开,
            FRAM电源关闭,
            FLASH电源打开,
            FLASH电源关闭,
            能够写入读取时间即通过,
            铁电正常写入擦除或其它操作,
            FLASH正常写入擦除或其它操作,
        }
        public Dictionary<SimpleCommandEnum, string> SimpleCommands = new Dictionary<SimpleCommandEnum, string>
        {
            {SimpleCommandEnum.CDP电源打开,"255:W:PCDP:1"},
            {SimpleCommandEnum.CDP电源关闭,"255:W:PCDP:0"},
            {SimpleCommandEnum.液晶屏电源打开,"255:W:PLCD:1"},
            {SimpleCommandEnum.液晶屏电源关闭,"255:W:PLCD:0"},
            {SimpleCommandEnum.触摸屏电源打开,"255:W:PTSP:1"},
            {SimpleCommandEnum.触摸屏电源关闭,"255:W:PTSP:0"},
            {SimpleCommandEnum.FRAM电源打开,"255:W:PFRAM:1"},
            {SimpleCommandEnum.FRAM电源关闭,"255:W:PFRAM:0"},
            {SimpleCommandEnum.FLASH电源打开,"255:W:PFLASH:1"},
            {SimpleCommandEnum.FLASH电源关闭,"255:W:PFLASH:0"},
            {SimpleCommandEnum.能够写入读取时间即通过,"255:W:TRTC"},
            {SimpleCommandEnum.铁电正常写入擦除或其它操作,"255:W:TFRAM"},
            {SimpleCommandEnum.FLASH正常写入擦除或其它操作,"255:W:TFLASH"},
        };

        public bool OpenMainboard()
        {
            return false;
        }

        #endregion
        #region 序列号
        /// <summary>
        /// 获取SN
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetSerialNumber(out string SN)
        {
            SN = string.Empty;
            iResponse<string> result = DPG2.GetSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            SN = result.Result;
            return true;
        }
        /// <summary>
        /// 设置序列号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetSerialNumber(string code)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetSerialNumber(code);
            return result.IsCorrect;
        }


        /// <summary>
        /// 获取表头SN
        /// </summary>
        /// <param name="SN"></param>
        /// <returns></returns>
        public bool GetComponentSN(out string SN)
        {
            SN = string.Empty;
            iResponse<string> result = DPG2.GetComponentSN();
            if (!result.IsCorrect)
            {
                return false;
            }
            SN = result.Result;
            return true;
        }

        /// <summary>
        /// 设置表头序列号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetComponentSN(string code)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetComponentSN(code);
            return result.IsCorrect;
        }
        #endregion

        #region 设备类型
        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetDevType(out string type)
        {
            type = string.Empty;
            iResponse<string> result = DPG2.GetDevType();
            if (!result.IsCorrect)
            {
                return false;
            }
            type = result.Result;
            return true;
        }
        /// <summary>
        /// 设置类型
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetDevType(string type)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetDevType(type);
            return result.IsCorrect;
        }
        #endregion


        /// <summary>
        /// 读取大气压校准数据
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetAutomDUALdata(out string Value)
        {
            Value = "";
            iResponse<string> result = DPG2.GetAutomDUALdata();
            if (!result.IsCorrect)
            {
                return false;
            }
            Value = result.Result.ToString();
            return true;
        }

        /// <summary>
        /// 读取产线工装大气压标定数据
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetLineAutomDUALdata(out string Value)
        {
            Value = "";
            iResponse<string> result = DPG2.GetLineAutomDUALdata();
            if (!result.IsCorrect)
            {
                return false;
            }
            Value = result.Result.ToString();
            return true;
        }


        /// <summary>
        /// 获取系统日期和时间
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool GetSystemDateTime(out DateTime dateTime)
        {
            dateTime = DateTime.MinValue;
            iResponse<DateTime> result = DPG2.GetSystemDate();
            if (!result.IsCorrect)
            {
                return false;
            }
            dateTime = result.Result;

            iResponse<DateTime> result2 = DPG2.GetSystemTime();
            if (!result2.IsCorrect)
            {
                return false;
            }
            dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, result2.Result.Hour, result2.Result.Minute, result2.Result.Second);
            return true;
        }
        /// <summary>
        /// 设置系统日期和时间
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public bool SetSystemDateTime(DateTime dateTime)
        {
            iResponse returnValue1 = DPG2.SetSystemTime(dateTime);
            if (!returnValue1.IsCorrect)
            {
                return false;
            }
            System.Threading.Thread.Sleep(1000);
            iResponse returnValue2 = DPG2.SetSystemDate(dateTime);
            if (!returnValue2.IsCorrect)
            {

                return false;
            }
            return true;
        }


        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetBatteryVoltage(out BatteryStruct Voltage)
        {
            Voltage = new BatteryStruct();
            iResponse<BatteryStruct> result = DPG2.GetBatteryVoltage();
            if (!result.IsCorrect)
            {
                return false;
            }
            Voltage = result.Result;
            return true;
        }



        /// <summary>
        /// 读取当前压力类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetPressureType(out string Type)
        {
            Type = "";
            iResponse<PressureType> result = DPG2.GetPressureType();
            if (!result.IsCorrect)
            {
                return false;
            }
            Type = result.Result.ToString();
            return true;
        }

        /// <summary>
        /// 设置当前压力类型
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetPressureType(PressureType type)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetPressureType(type);
            return result.IsCorrect;
        }


        /// <summary>
        /// 查询压力模块是否在线
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetPressureModelIsOnline(out bool isonline)
        {
            isonline = false;
            iResponse<bool> result = DPG2.GetPressureModelIsOnline();
            if (!result.IsCorrect)
            {
                return false;
            }
            isonline = result.Result;
            return true;
        }



        /// <summary>
        /// 获取测量值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetPressure(PressureStructType structType, out Pressure value)
        {
            value = new Pressure();
            iResponse<PressureStruct> result = DPG2.GetPressureStruct(structType);
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result.Pressure;
            return true;
        }

        /// <summary>
        /// 设置当前压力类型
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetPressureUnit(string unit)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetPressureUnit(unit);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取表绝压切换功能状态
        /// </summary>
        /// <returns></returns>
        public bool GetPTypeSwitch(out OpenCloseState state)
        {
            var response = DPG2.GetPTypeSwitch();
            state = response.Result;
            return response.IsCorrect;
        }

        /// <summary>
        /// 获取当前压力类型
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool GetPressureUnit(out Unit unit)
        {
            unit = new Unit();
            if (!IsOpen)
                return false;
            iResponse<Unit> result = DPG2.GetPressureUnit();
            if (!result.IsCorrect)
            {
                return false;
            }
            unit = result.Result;
            return true;
        }

        /// <summary>
        /// 获取大气压测量值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetTemperature(out Temperature value)
        {
            value = new Temperature();
            iResponse<Temperature> result = DPG2.GetTemperature();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 获取大气压测量值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetPressure(out Pressure value)
        {
            value = new Pressure();
            iResponse<Pressure> result = DPG2.GetPressure();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }


        /// <summary>
        /// 获取大气压和温度测量值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetTPValue(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetTPValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 开启背光
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool OpenBackLighting()
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.OpenBackLighting();
            return result.IsCorrect;
        }

        /// <summary>
        /// 关闭背光
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool CloseBackLighting()
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.CloseBackLighting();
            return result.IsCorrect;
        }

        /// <summary>
        /// 是否支持蓝牙
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetHaveBLE(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetDeviceHaveBLE();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 开启蓝牙
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool OpenBLE()
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.OpenBLE();
            return result.IsCorrect;
        }

        /// <summary>
        /// 关闭蓝牙
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool CloseBLE()
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.CloseBLE();
            return result.IsCorrect;
        }

        /// <summary>
        /// 软重启
        /// </summary>
        /// <returns></returns>
        public bool SoftReboot(int second)
        {
            try
            {
                iResponse result = DPG2.DiagnosticSystemReboot(second);
                return result.IsCorrect;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 重启
        /// </summary>
        /// <returns></returns>
        public bool Reboot()
        {
            iResponse result = DPG2.Reset();
            return result.IsCorrect;
        }

        /// <summary>
        /// 关机
        /// </summary>
        /// <returns></returns>
        public bool ShutDown()
        {
            iResponse result = DPG2.ShutDown();
            return result.IsCorrect;
        }

        /// <summary>
        /// 主板NOR-FLASH测试结果
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetNORFLASHValue(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetNORFLASHValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }



        /// <summary>
        /// 获取大气压板存储器自检结果
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetBARoEepromValue(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetBARoEepromValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 打开LED屏幕测试界面，全显示
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetLEDFullOpen()
        {
            iResponse result = DPG2.SetLEDFullOpen();
            return result.IsCorrect;
        }

        /// <summary>
        /// 返回主界面
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetLEDHome()
        {
            iResponse result = DPG2.SetLEDHome();
            return result.IsCorrect;
        }

        /// <summary>
        /// 打开屏幕触摸测试界面
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetTouchOpen()
        {
            iResponse result = DPG2.SetTouchOpen();
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取屏幕触摸的坐标值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetTouchValue(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetTouchValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }


        /// <summary>
        /// 获取实体按键按下的值，1，2，3
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetKeyDownValue(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetKeyDownValue();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 获取蓝牙名称+MAC地址
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetBlueToothNameMAC(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetBlueToothNameMAC();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 打开扬声器
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetBuzzerOpen()
        {
            iResponse result = DPG2.SetBuzzerOpen();
            return result.IsCorrect;
        }


        /// <summary>
        /// 关闭扬声器
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetBuzzerClose()
        {
            iResponse result = DPG2.SetBuzzerClose();
            return result.IsCorrect;
        }


        /// <summary>
        /// 设置设备生产日期
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetDiagnosticLFRDate(DateTime date)
        {
            iResponse result = DPG2.SetLFD(date);
            return result.IsCorrect;
        }


        /// <summary>
        /// 获取设备生产日期
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetDiagnosticLFRDate(out DateTime value)
        {
            value = DateTime.MinValue;
            iResponse<DateTime> result = DPG2.GetLFD();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }


        /// <summary>
        /// 设置大气压传感器SN
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool SetAutomSN(string Sn)
        {
            iResponse result = DPG2.SetAutomSN(Sn);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取大气压传感器SN
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetAutomSN(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetAutomSN();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 读取是否支持大气压
        /// </summary>
        /// <returns></returns>
        public bool GetATM(out OpenCloseState atm)
        {
            atm = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = DPG2.GetATM();
            if (!result.IsCorrect)
            {
                return false;
            }
            atm = result.Result;
            return true;
        }

        /// <summary>
        /// 设置是否支持大气压
        /// </summary>
        /// <param name="atm"></param>
        /// <returns></returns>
        public bool SetATM(OpenCloseState atm)
        {
            iResponse result = DPG2.SetATM(atm);
            var result1 = DPG2.GetATM();
            if (result.IsCorrect && result1.IsCorrect && atm == result1.Result)
                return true;
            return false;
        }

        /// <summary>
        /// 设置SW开关信号输出
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool SetSWState(int Swnum, int Value)
        {
            iResponse result = DPG2.SetSWState(Swnum, Value);
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置外接模块类型
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool SetExtendModuleEnableType(ExternalModuleType value)
        {
            iResponse result = DPG2.SetExtendModuleEnableType(value);
            return result.IsCorrect;
        }


        /// <summary>
        /// 读取外接模块类型
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool GetExtendModuleEnableType()
        {
            iResponse<ExternalModuleType> result = DPG2.GetExtendModuleEnableType();
            if (result.IsCorrect)
            {
                if (result.Result == ExternalModuleType.SW_2)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 读取外接模块类型
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool GetExtendModuleEnableType(out ExternalModuleType value)
        {
            value = ExternalModuleType.NULL;
            iResponse<ExternalModuleType> result = DPG2.GetExtendModuleEnableType();
            if (result.IsCorrect)
            {
                value = result.Result;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 格式化表头存储区域
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool SetFlashFormat()
        {
            iResponse result = DPG2.SetFlashFormat();
            return result.IsCorrect;
        }

        /// <summary>
        /// 恢复出厂设置,出厂必须做
        /// </summary>
        /// <param name="Sn"></param>
        /// <returns></returns>
        public bool DDiagnosticSystemRestore()
        {
            iResponse result = DPG2.DDiagnosticSystemRestore(Xmas11.Comm.Devices.DPG2.PasswordType.FACTORY, false);
            return result.IsCorrect;
        }


        /// <summary>
        /// 获取压力模块过压记录
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetDPSExGY(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetPressureClearRecoder();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 清除压力模块过压记录
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetDPSExGYClear(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.SetPressureClearRecoder();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }



        /// <summary>
        /// 获取串口通讯波特率
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetComInfo(out string value)
        {
            value = "";
            iResponse<string> result = DPG2.GetComInfo();
            if (!result.IsCorrect)
            {
                return false;
            }
            value = result.Result;
            return true;
        }

        /// <summary>
        /// 设置串口通讯波特率
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetComInfo(string address, string bate, string date, string stop, string check)
        {
            iResponse result = DPG2.SetComInfo(address, bate, date, stop, check);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 设置当前音量
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetVoiceCircle()
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetVoiceCircle();
            return result.IsCorrect;
        }

        /// <summary>
        /// 设置表头的屏幕配置信息
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetScreenConfig(string type)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetScreenConfig(type);
            return result.IsCorrect;
        }


        /// <summary>
        /// 查询表头的屏幕配置信息
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool GetScreenConfig(out string type)
        {
            type = "";
            iResponse<string> result = DPG2.GetScreenConfig();
            if (!result.IsCorrect)
            {
                return false;
            }
            type = result.Result;
            return true;
        }

        /// <summary>
        /// 设置压力测量速率
        /// </summary>
        /// <param name="workMode"></param>
        /// <param name="second"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public bool SetPRESsureRATE(RateMode workMode, int second, int count)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetPRESsureRATE(workMode, second, count);
            if (result.IsCorrect)
            {
                return GetPRESsureRATE(out PressureRate pressureRate) && workMode == pressureRate.RateMode && second == pressureRate.RateSeconds && count == pressureRate.RateCounts;
            }
            return false;
        }

        /// <summary>
        /// 读取压力测量速率
        /// </summary>
        /// <returns></returns>
        public bool GetPRESsureRATE(out PressureRate pressureRate)
        {
            pressureRate = new PressureRate();
            iResponse<PressureRate> result = DPG2.GetPRESsureRATE();
            if (!result.IsCorrect)
            {
                return false;
            }
            pressureRate = result.Result;
            return true;
        }

        /// <summary>
        /// 设置触屏打开关闭
        /// </summary>
        /// <param name="state">这里0是打开，1是关闭</param>
        /// <returns></returns>
        public bool SetSystemMode(OpenCloseState state)
        {
            if (!IsOpen)
                return false;
            iResponse result = DPG2.SetSystemMode(state);
            if (result.IsCorrect)
            {
                return GetSystemMode(out OpenCloseState returnState) && returnState == state;
            }
            return false;
        }

        /// <summary>
        /// 查询触屏打开关闭状态
        /// </summary>
        /// <returns></returns>
        public bool GetSystemMode(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = DPG2.GetSystemMode();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }

        #region 大订单相关

        /// <summary>
        /// 获取自动关机状态
        /// </summary>
        /// <returns></returns>
        public bool GetAUTOpoweroff(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            var result = DPG2.GetAUTOpoweroff();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }

        /// <summary>
        /// 获取tag
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool GetDiagnosticTag(out string tag)
        {
            tag = string.Empty;
            var result = DPG2.GetDiagnosticTag();
            if (!result.IsCorrect)
            {
                return false;
            }
            tag = result.Result;
            return true;
        }

        /// <summary>
        /// 获取滤波功能状态
        /// </summary>
        /// <returns></returns>
        public bool GetFilterInfo(out FilterStruct filterStruct)
        {
            filterStruct = new FilterStruct();
            var result = DPG2.GetFilterInfo();
            if (!result.IsCorrect)
            {
                return false;
            }
            filterStruct = result.Result;
            return true;
        }

        /// <summary>
        /// 获取泄漏测试结果计算方式
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public bool GetDIAGnosticFEATureLEACal(out int mode)
        {
            mode = 0;
            var result = DPG2.GetDIAGnosticFEATureLEACal();
            if (!result.IsCorrect)
            {
                return false;
            }
            mode = result.Result;
            return true;
        }

        /// <summary>
        /// 获取压力测量速率
        /// </summary>
        /// <param name="rate"></param>
        /// <returns></returns>
        public bool GetDIAGnosticFEATureLEACal(out PressureRate rate)
        {
            rate = new PressureRate();
            var result = DPG2.GetPRESsureRATE();
            if (!result.IsCorrect)
            {
                return false;
            }
            rate = result.Result;
            return true;
        }

        /// <summary>
        /// 获取分辨率
        /// </summary>
        /// <param name="digit"></param>
        /// <returns></returns>
        public bool GetDisplayDigit(out DisplayDigit digit)
        {
            digit = DisplayDigit.Unknown;
            var result = DPG2.GetDisplayDigit();
            if (!result.IsCorrect)
            {
                return false;
            }
            digit = result.Result;
            return true;
        }

        /// <summary>
        /// 获取Tare值
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetDIAGnosticFEATureTARE(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            var result = DPG2.GetDIAGnosticFEATureTARE();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }

        /// <summary>
        /// 获取温度单位
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public bool GetTemperatureUnit(out string unit)
        {
            unit = string.Empty;
            var result = DPG2.GetTemperatureUnit();
            if (!result.IsCorrect)
            {
                return false;
            }
            unit = result.Result.ToString();
            return true;
        }

        /// <summary>
        /// 获取系统日期格式
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public bool GetSystemDateFormat(out int mode)
        {
            mode = 0;
            var result = DPG2.GetSystemDateFormat();
            if (!result.IsCorrect)
            {
                return false;
            }
            mode = result.Result;
            return true;
        }

        /// <summary>
        /// 获取校准提醒信息
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public bool GetCalReminderInfo(out CalReminderInfo reminderInfo)
        {
            reminderInfo = new CalReminderInfo();
            var result = DPG2.GetCalReminderInfo();
            if (!result.IsCorrect)
            {
                return false;
            }
            reminderInfo = result.Result;
            return true;
        }

        /// <summary>
        /// 获取压力单位列表配置模式
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public bool GetDIAGnosticPUNItListType(out int mode)
        {
            mode = 0;
            var result = DPG2.GetDIAGnosticPUNItListType();
            if (!result.IsCorrect)
            {
                return false;
            }
            mode = result.Result;
            return true;
        }

        /// <summary>
        /// 获取压力单位列表
        /// </summary>
        /// <param name="units"></param>
        /// <returns></returns>
        public bool GetPRESsureUNITList(out string units)
        {
            units = string.Empty;
            var result = DPG2.GetPRESsureUNITList();
            if (!result.IsCorrect)
            {
                return false;
            }
            units = result.Result.ToString();
            return true;
        }

        /// <summary>
        /// 获取单位中是否显示温度名称
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetDIAGnosticFEATureUNITname(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            var result = DPG2.GetDIAGnosticFEATureUNITname();
            if (!result.IsCorrect)
            {
                return false;
            }
            state = result.Result;
            return true;
        }
        #endregion
        #endregion

        #region 升级

        /// <summary>
        /// 进入loader
        /// </summary>
        /// <returns></returns>
        public bool SoftReboot()
        {
            try
            {
                iResponse result = DPG2.DiagnosticSystemReboot(60);
                return result.IsCorrect;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 是否可升级
        /// </summary>
        /// <returns></returns>
        public override bool IsUpgradable()
        {
            if (this.UpgradeSetting == null)
            {
                string path = UpgradeFile.LocalCacheRoot + @"/DPG2/OS/UpgradeSetting.xml";
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
        /// 升级检查，通讯正常返回0，通讯失败返回2，通讯类型不对返回1
        /// </summary>
        public override int UpgradeCheck()
        {
            int result = 0;
            this.UpgradeInfo.ClearUpgradeMsgs();
            if (this.CommConfig is SerialPortConfig)
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
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg3));
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
        /// 检查升级文件
        /// </summary>
        /// <returns></returns>
        public override bool CheckUpgradeFile()
        {
            bool result = false;
            bool connected = this.IsConnected;
            if (!connected)
            {
                if (!this.IsOpen)
                {
                    return result;
                }
            }
            try
            {
                if (this.UpgradeSetting.UpgradeFiles != null && this.UpgradeSetting.UpgradeFiles.Count > 0)
                {
                    result = this.UpgradeSetting.UpgradeFiles.AsParallel().All(f =>
                    {
                        if (f.IsMain)
                        {
                            try
                            {
                                if (f.IsLocal)
                                {
                                    if (f.IsManualSelected)
                                    {
                                        if (!File.Exists(f.LocalFilePath))
                                        {
                                            DirectoryInfo dir = new DirectoryInfo(Path.Combine(UpgradeFile.LocalCacheRoot, f.LocalURL));
                                            if (Directory.Exists(dir.FullName))
                                            {
                                                string currentVersion;
                                                if (GetVersion(out currentVersion))
                                                {
                                                    FileInfo fileinfo = null;
                                                    if (currentVersion.Contains("DPG"))
                                                    {
                                                        //解析主版本号进行搜索
                                                        List<string> verParts = new List<string>();
                                                        verParts.AddRange(currentVersion.Split('V').ToList());
                                                        int majorVerNumber = int.Parse(Regex.Replace(verParts[1].Split('.')[0], "[a-z]", "", RegexOptions.IgnoreCase));
                                                        fileinfo = dir.GetFiles(string.Format("DPCV0{0}*", majorVerNumber)).OrderByDescending(file => file.LastWriteTime).FirstOrDefault();

                                                    }
                                                    else
                                                    {
                                                        fileinfo = dir.GetFiles(string.Format("*{0}", f.FileExtension)).OrderByDescending(file => file.LastWriteTime).FirstOrDefault();
                                                    }
                                                    if (fileinfo != null)
                                                    {
                                                        f.LocalFilePath = fileinfo.FullName;
                                                        f.IsManualSelected = false;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        DirectoryInfo dir = new DirectoryInfo(Path.Combine(UpgradeFile.LocalCacheRoot, f.LocalURL));
                                        if (Directory.Exists(dir.FullName))
                                        {
                                            string currentVersion;
                                            if (GetVersion(out currentVersion))
                                            {
                                                FileInfo fileinfo = null;
                                                if (currentVersion.Contains("DPG"))
                                                {
                                                    //解析主版本号进行搜索
                                                    List<string> verParts = new List<string>();
                                                    verParts.AddRange(currentVersion.Split('V').ToList());
                                                    int majorVerNumber = int.Parse(Regex.Replace(verParts[1].Split('.')[0], "[a-z]", "", RegexOptions.IgnoreCase));
                                                    fileinfo = dir.GetFiles(string.Format("DPG V0{0}*", majorVerNumber)).OrderByDescending(file => file.LastWriteTime).FirstOrDefault();

                                                }
                                                else
                                                {
                                                    fileinfo = dir.GetFiles(string.Format("*{0}", f.FileExtension)).OrderByDescending(file => file.LastWriteTime).FirstOrDefault();
                                                }
                                                if (fileinfo != null)
                                                {
                                                    f.LocalFilePath = fileinfo.FullName;
                                                }
                                            }
                                        }
                                    }
                                    if (File.Exists(f.LocalFilePath))
                                    {
                                        f.FileName = Path.GetFileName(f.LocalFilePath);
                                        f.CachePath = Path.Combine(Path.Combine(UpgradeFile.LocalCacheRoot, f.LocalURL), f.FileName);
                                        UpgradeFile file = f.GetFile();
                                        file.CacheFile();
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                                else
                                {

                                    if (f.IsManualSelected)
                                    {
                                        if (!Bots.TestBench.Util.UpgradeFileHelper.FileIsExist(f.UserName, f.Password, f.RemoteFilePath))
                                        {
                                            string currentVersion;
                                            if (GetVersion(out currentVersion))
                                            {

                                                string vPrefix = "DPG";

                                                string attribute = string.Join("\\", f.RemoteURL.Replace("/", "\\").Split('\\').Where(a => !string.IsNullOrEmpty(a)).ToArray());
                                                string filePath = "Files\\FirmwareProgram\\" + attribute;
                                                string files = Bots.TestBench.Util.UpgradeFileHelper.GetFiles(f.UserName, f.Password, f.RemoteServicePath, "", attribute, f.FileExtension, vPrefix, "", "", "", "", currentVersion, "");

                                                if (!string.IsNullOrEmpty(files))
                                                {
                                                    JsonSerializerSettings jsonsetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, TypeNameHandling = TypeNameHandling.Auto };
                                                    dynamic jsonObj = JsonConvert.DeserializeObject(files);
                                                    if (jsonObj.Count > 0)
                                                    {

                                                        string fileID = jsonObj[0]["FileGUID"].Value;
                                                        f.RemoteFilePath = f.RemoteServicePath + @"api/upgradeFile/Download/" + fileID;
                                                        f.IsManualSelected = false;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                    else
                                    {
                                        //是最新匹配版本
                                        if (!Bots.TestBench.Util.UpgradeFileHelper.FileIsLatest(f.UserName, f.Password, f.RemoteFilePath))
                                        {
                                            string currentVersion;
                                            if (GetVersion(out currentVersion))
                                            {
                                                string vPrefix = "DPG";

                                                string attribute = string.Join("\\", f.RemoteURL.Replace("/", "\\").Split('\\').Where(a => !string.IsNullOrEmpty(a)).ToArray());
                                                string filePath = "Files\\FirmwareProgram\\" + attribute;
                                                string files = Bots.TestBench.Util.UpgradeFileHelper.GetFiles(f.UserName, f.Password, f.RemoteServicePath, "", attribute, f.FileExtension, vPrefix, "", "", "", "", currentVersion, "");

                                                if (!string.IsNullOrEmpty(files))
                                                {
                                                    JsonSerializerSettings jsonsetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, TypeNameHandling = TypeNameHandling.Auto };
                                                    dynamic jsonObj = JsonConvert.DeserializeObject(files);
                                                    if (jsonObj.Count > 0)
                                                    {

                                                        string fileID = jsonObj[0]["FileGUID"].Value;
                                                        f.RemoteFilePath = f.RemoteServicePath + @"api/upgradeFile/Download/" + fileID;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    //远程升级，同一版本任何时间只有一个是有效在用的正式版
                                    if (!Bots.TestBench.Util.UpgradeFileHelper.FileIsLatest(f.UserName, f.Password, f.RemoteFilePath))
                                    {
                                        f.FileName = Bots.TestBench.Util.UpgradeFileHelper.GetFileName(f.UserName, f.Password, f.RemoteFilePath);
                                        f.CachePath = Path.Combine(Path.Combine(UpgradeFile.LocalCacheRoot, f.LocalURL), f.FileName);
                                        UpgradeFile file = f.GetFile();
                                        file.CacheFile();
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                            }
                            catch
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return f.CheckFile();
                        }
                    });
                }
                else
                {
                    result = false;
                }
            }
            catch { result = false; }
            if (!connected)
            {
                this.Close();
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
                info.Name = Bots.TestBench.Device.Base.Properties.Resources.FirmwareVersion;
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
            string dpsexFirmware;
            if (GetDPSEXVersion(out dpsexFirmware))
            {
                VersionInfo info = new VersionInfo();
                info.Key = "SubModule";
                info.Name = Bots.TestBench.Device.Base.Properties.Resources.SubModule;
                info.CurrentVersion = dpsexFirmware;
                if (UpgradeInfo.VersionInfoIsContains(info))
                {
                    UpgradeInfo.VersionInfoDic["SubModule"].CurrentVersion = info.CurrentVersion;
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
                        var dpc_ex = mainUpgradeFile.Versions.Where(v => v.Key.Contains("DPG")).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(dpc_ex))
                        {
                            UpgradeInfo.VersionInfoDic["MainFirmware"].UpgradeVersion = dpc_ex;
                        }
                    }
                    if (UpgradeInfo.VersionInfoIsContains("SubModule"))
                    {
                        UpgradeFile esUpgradeFile = this.UpgradeSetting.GetUpgradeFile("DPSEXUPDATE");
                        if (esUpgradeFile != null)
                        {
                            var es_ex = esUpgradeFile.Versions.Where(v => v.Key.Contains("CDP")).Select(v => v.Value).FirstOrDefault();
                            if (!string.IsNullOrEmpty(es_ex))
                            {
                                UpgradeInfo.VersionInfoDic["SubModule"].UpgradeVersion = es_ex;
                            }
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

            //if (UpgradeInfo.MainInfoIsContains("Code"))
            //{
            //    SetCode(UpgradeInfo.MainInfoDic["Code"].Info);
            //}
            //if (UpgradeInfo.MainInfoIsContains("Type"))
            //{
            //    SetDevType(UpgradeInfo.MainInfoDic["Type"].Info);
            //}
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
            IsUpgrading = true;
            this.UpgradeInfo.IsProgress = true;
            this.UpgradeInfo.ProgressIsIndeterminate = true;
            this.UpgradeInfo.UpgradeResult = UpgradeResult.None;
            DateTime logDateTime = DateTime.Now;
            try
            {
                this.SaveInUpgradingLog(logDateTime);
                this.UpgradeInfo.ClearUpgradeMsgs();
                UpgradeFile mainUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();
                if (mainUpgradeFile != null)
                {
                    DateTime startDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade, startDT.ToString()));

                    #region 1、复位重启,Loader握手进入Bootloader
                    ////复位重启
                    //SetReset();
                    ////关闭当前连接
                    //this.Close();
                    ////Loader握手
                    //System.Threading.Tasks.Task.Run(() =>
                    //{
                    //    //DetectUSB(10000);
                    //    DetectSerialPort(10000);
                    //});


                    if (!SoftReboot())
                    {
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    this.Close();
                    System.Threading.Tasks.Task.Delay(5000).Wait();

                    if (!DetectSerialPort(10000))
                    {
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    ////握手
                    //System.Threading.Tasks.Task<bool> handshakeResult = System.Threading.Tasks.Task.Run<bool>(() =>
                    //{
                    //    return Handshake(50000);
                    //});
                    //handshakeResult.Wait();
                    //if (!handshakeResult.Result)
                    //{
                    //    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    //    return UpgradeInfo;
                    //}
                    #region 读取Bootloader版本
                    string loaderVersion = string.Empty;
                    if (GetLoaderVersion(out loaderVersion))
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + " Loader " + Bots.TestBench.Device.Base.Properties.Resources.Version, loaderVersion));
                    }
                    else
                    {
                        RunApplication();
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + " Loader " + Bots.TestBench.Device.Base.Properties.Resources.Version, Bots.TestBench.Device.Base.Properties.Resources.GetFailed));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    #endregion
                    #endregion

                    #region 2、主机升级
                    //内部文件
                    UpgradeFile mainIntUpgradeFile = this.UpgradeSetting.GetMainUpgradeFile();

                    //外部文件
                    UpgradeFile mainExtUpgradeFile = this.UpgradeSetting.GetUpgradeFile("DPSEXUPDATE");

                    if (mainIntUpgradeFile == null)
                    {
                        RunApplication();
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileINT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileConfigError));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    else if (mainExtUpgradeFile == null)
                    {
                        RunApplication();
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileEXT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileConfigError));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    else
                    {

                        #region 3、从设备升级
                        if (this.UpgradeSetting.GetOriginalUpgradeFile(mainExtUpgradeFile).IsUpdateEnable)
                        {
                            if (!File.Exists(mainExtUpgradeFile.CachePath))
                            {
                                RunApplication();
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileEXT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileGetFailed));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }

                            if (UpgradeInfo.VersionInfoIsContains("SubModule"))
                            {
                                if (UpgradeInfo.VersionInfoDic["SubModule"].CurrentVersion != UpgradeInfo.VersionInfoDic["SubModule"].UpgradeVersion)
                                {
                                    UpgradeFile ESIntUpgradeFile = this.UpgradeSetting.GetUpgradeFile("DPSEXUPDATE");
                                    if (ESIntUpgradeFile != null && File.Exists(ESIntUpgradeFile.CachePath))
                                    {
                                        if (this.UpgradeSetting.GetOriginalUpgradeFile(ESIntUpgradeFile).IsUpdateEnable)
                                        {
                                            UpgradeSlaveDevice(Bots.TestBench.Device.Base.Properties.Resources.SubModule + "-ES", 1, ESIntUpgradeFile);
                                            System.Threading.Thread.Sleep(2000);
                                        }
                                    }

                                }
                            }
                        }

                        if (this.UpgradeSetting.GetOriginalUpgradeFile(mainIntUpgradeFile).IsUpdateEnable)
                        {
                            if (!File.Exists(mainIntUpgradeFile.CachePath))
                            {
                                RunApplication();
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileINT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileGetFailed));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }

                            #region 获取下载参数
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.UpgradeGetDownloadParameters));
                            CSTLoaderV2InternalFlash mainInternalFlash = new CSTLoaderV2InternalFlash();
                            if (GetInternalFlashParameters(out mainInternalFlash))
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, string.Format(Bots.TestBench.Device.Base.Properties.Resources.UpgradeDownloadParameters1, mainInternalFlash.PackageMaxLength, mainInternalFlash.PackageMaxTimeout, mainInternalFlash.ErasureFlashTimeout)));
                            }
                            else
                            {
                                RunApplication();
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.UpgradeGetDownloadParametersFail1));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }
                            //外部Flash
                            //CSTLoaderV2ExternalFlash mainExternalFlash = new CSTLoaderV2ExternalFlash();
                            //if (GetExternalFlashParameters(out mainExternalFlash))
                            //{
                            //    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, string.Format(Bots.TestBench.Device.Base.Properties.Resources.UpgradeDownloadParameters2, mainExternalFlash.PackageMaxLength, mainExternalFlash.PackageMaxTimeout, mainExternalFlash.ErasureFlashTimeoutFor1M)));
                            //}
                            //else
                            //{
                            //    RunApplication();
                            //    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.UpgradeGetDownloadParametersFail1));
                            //    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                            //    return UpgradeInfo;
                            //}
                            #endregion

                            #region 读文件
                            //内部文件
                            byte[] mainIntUpgradeFile_VectorTableData = null;
                            ushort mainIntUpgradeFile_VectorTableCRC = 0;
                            byte[] mainIntUpgradeFile_CodeData = null;
                            ushort mainIntUpgradeFile_CodeCRC = 0;
                            if (!ReadUpgradeFile(mainIntUpgradeFile, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileINT, out mainIntUpgradeFile_VectorTableData, out mainIntUpgradeFile_VectorTableCRC, out mainIntUpgradeFile_CodeData, out mainIntUpgradeFile_CodeCRC))
                            {
                                RunApplication();
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileINT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileReadFailed));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }
                            ////外部文件
                            //byte[] mainExtUpgradeFile_ExternalFlashData = null;
                            //ushort mainExtUpgradeFile_ExternalFlashCRC = 0;
                            //if (!ReadUpgradeFile(mainExtUpgradeFile, mainExternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileEXT, out mainExtUpgradeFile_ExternalFlashData, out mainExtUpgradeFile_ExternalFlashCRC))
                            //{
                            //    RunApplication();
                            //    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host + "-" + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileEXT, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileReadFailed));
                            //    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                            //    return UpgradeInfo;
                            //}
                            #endregion

                            #region 擦除Flash
                            if (ErasureInternalFlash(mainInternalFlash.ErasureFlashTimeout))
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.EraseInternalFlashOk));
                            }
                            else
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.EraseInternalFlashFailed));
                                this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                                return UpgradeInfo;
                            }
                            //if (ErasureExternalFlash(mainExternalFlash.ErasureFlashTimeoutFor1M, mainExternalFlash.FlashAddress, mainExtUpgradeFile.FileContent.Length - mainExternalFlash.FlashOffset))
                            //{
                            //    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.EraseExternallFlashOk));
                            //}
                            //else
                            //{
                            //    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.EraseExternallFlashFailed));
                            //    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                            //    return UpgradeInfo;
                            //}
                            #endregion

                            #region 下载文件
                            //下载内部Flash中断向量表
                            DownloadVectorTableData(mainIntUpgradeFile_VectorTableData, mainIntUpgradeFile_VectorTableCRC, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash1);
                            //下载内部Flash代码段
                            DownloadCodeData(mainIntUpgradeFile_CodeData, mainIntUpgradeFile_CodeCRC, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash2);
                            ////下载外部Flash
                            //DownloadExternalFlashData(mainExtUpgradeFile_ExternalFlashData, mainExtUpgradeFile_ExternalFlashCRC, mainExternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Host, Bots.TestBench.Device.Base.Properties.Resources.DownloadExternalFlash);
                            #endregion

                            #region 下载完成确认
                            DownloadConfirm();
                            #endregion
                        }
                    }

                    #endregion



                    #endregion

                    #region 4、运行程序
                    RunApplication();
                    #endregion

                    #region 5、断开连接,重新连接设备
                    System.Threading.Tasks.Task.Delay(2000).Wait();
                    this.Close();
                    System.Threading.Tasks.Task.Delay(2000).Wait();
                    int i = 0;
                    while (true)
                    {
                        if (this.Open())
                        {
                            System.Threading.Tasks.Task.Delay(1000).Wait();
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

                        System.Threading.Tasks.Task.Delay(2000).Wait();
                        if (i > 500)
                        {
                            break;
                        }
                        i++;
                    }

                    #endregion
                    DateTime stopDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeComplete, stopDT.ToString()));
                }
                else
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeError));
                }
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
                                            upgradeFile.FileContent = System.IO.File.ReadAllBytes(upgradeFile.CachePath);
                                            int substringlength = 0;
                                            string text = System.IO.File.ReadAllText(upgradeFile.CachePath);
                                            string textsub = null;
                                            string keyControllerVersion = "DPG ";
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
                                            var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
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
                                            var controllerVersion = text.Substring(selectfirst, substringlength);
                                            upgradeFile.AddVersion("DPG", controllerVersion);

                                        }
                                    }
                                    else if (upgradeFile.FileKey == "DPSEXUPDATE")
                                    {
                                        upgradeFile.FileContent = System.IO.File.ReadAllBytes(upgradeFile.CachePath);
                                        int substringlength = 0;
                                        string text = System.IO.File.ReadAllText(upgradeFile.CachePath);
                                        string textsub = null;
                                        string keyControllerVersion = "DPS-EX";
                                        int selectfirst = text.LastIndexOf(keyControllerVersion);
                                        for (int i = selectfirst + keyControllerVersion.Length; i < text.Length; i++)
                                        {
                                            textsub = text.Substring(i, 1);
                                            if (!(textsub == " " || textsub == "V" || textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                            {
                                                substringlength = i - selectfirst;
                                                break;
                                            }
                                        }
                                        var controllerHardwareVersion = text.Substring(selectfirst, substringlength);
                                        text = text.Substring(0, selectfirst);
                                        selectfirst = text.LastIndexOf(keyControllerVersion);
                                        for (int i = selectfirst + keyControllerVersion.Length + 2; i < text.Length; i++)
                                        {
                                            textsub = text.Substring(i, 1);
                                            if (!(textsub == " " || textsub == "V" || textsub == "0" || textsub == "1" || textsub == "2" || textsub == "3" || textsub == "4" || textsub == "5" || textsub == "6" || textsub == "7" || textsub == "8" || textsub == "9" || textsub == "."))
                                            {
                                                substringlength = i - selectfirst;
                                                break;
                                            }
                                        }
                                        var controllerVersion = text.Substring(selectfirst, substringlength);
                                        upgradeFile.AddVersion("CDP", controllerVersion);
                                    }
                                    else
                                    {
                                        upgradeFile.FileContent = System.IO.File.ReadAllBytes(upgradeFile.CachePath);
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
        /// 升级从设备
        /// </summary>
        /// <returns></returns>
        private bool UpgradeSlaveDevice(string slaveDeviceName, int slaveDeviceID, UpgradeFile upgradeFile)
        {

            //重设备开始升级
            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(slaveDeviceName, Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade));
            SlaveDeviceStartUpdate(slaveDeviceID);
            DateTime beginTime = DateTime.Now;
            System.Threading.Thread.Sleep(2000);
            //读取Bootloader版本

            string loaderVersion = string.Empty;
            while ((DateTime.Now - beginTime).TotalSeconds < 15)
            {
                if (GetSlaveDeviceLoaderVersion(out loaderVersion))
                {
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(slaveDeviceName + " Loader " + Bots.TestBench.Device.Base.Properties.Resources.Version, loaderVersion));
                    break;
                }
                System.Threading.Thread.Sleep(2000);
            }
            if ((DateTime.Now - beginTime).TotalSeconds > 15)
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(slaveDeviceName + " Loader " + Bots.TestBench.Device.Base.Properties.Resources.Version, Bots.TestBench.Device.Base.Properties.Resources.GetFailed));
                return false;
            }
            //获取下载参数
            CSTLoaderV2InternalFlash internalFlash = new CSTLoaderV2InternalFlash();
            if (GetSlaveDeviceInternalFlashParameters(out internalFlash))
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(slaveDeviceName, string.Format(Bots.TestBench.Device.Base.Properties.Resources.UpgradeDownloadParameters1, internalFlash.PackageMaxLength, internalFlash.PackageMaxTimeout, internalFlash.ErasureFlashTimeout)));
            }
            else
            {
                return false;
            }
            //读取文件
            byte[] vectorTableData = null;
            ushort vectorTableDataCRC = 0;
            byte[] vodeData = null;
            ushort vodeDataCRC = 0;
            if (!ReadUpgradeFile(upgradeFile, internalFlash, slaveDeviceName + Bots.TestBench.Device.Base.Properties.Resources.UpgradeFile, out vectorTableData, out vectorTableDataCRC, out vodeData, out vodeDataCRC))
            {
                return false;
            }
            //擦除Flash
            if (ErasureSlaveDeviceInternalFlash(internalFlash.ErasureFlashTimeout))
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(slaveDeviceName, Bots.TestBench.Device.Base.Properties.Resources.EraseInternalFlashOk));
            }
            else
            {
                return false;
            }
            //下载文件
            DownloadSlaveDeviceVectorTableData(vectorTableData, vectorTableDataCRC, internalFlash, slaveDeviceName, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash1);
            DownloadSlaveDeviceCodeData(vodeData, vodeDataCRC, internalFlash, slaveDeviceName, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash2);
            //下载完成确认
            SlaveDeviceDownloadConfirm();
            //运行程序
            RunSlaveDeviceApplication();
            return true;
        }


        /// <summary>
        /// 读度文件
        /// </summary>
        /// <param name="upgradeFile"></param>
        /// <param name="internalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="vectorTableData"></param>
        /// <param name="vectorTableCRC"></param>
        /// <param name="codeData"></param>
        /// <param name="codeCRC"></param>
        /// <returns></returns>
        private bool ReadUpgradeFile(UpgradeFile upgradeFile, CSTLoaderV2InternalFlash internalFlash, string msgName, out byte[] vectorTableData, out ushort vectorTableCRC, out byte[] codeData, out ushort codeCRC)
        {
            vectorTableData = null;
            vectorTableCRC = 0;
            codeData = null;
            codeCRC = 0;
            if (upgradeFile.FileContent.Length < internalFlash.VectorTableOffset + internalFlash.VectorTableUsableLength || upgradeFile.FileContent.Length <= internalFlash.CodeOffset)
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(msgName, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileError1));
                return false;
            }
            int codeLength = upgradeFile.FileContent.Length - internalFlash.CodeOffset;
            if (codeLength > internalFlash.CodeUsableLength)
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(msgName, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileError2));
                return false;
            }

            vectorTableData = new byte[internalFlash.VectorTableUsableLength];
            codeData = new byte[codeLength];

            CRCEntity crcEntity = new CRCEntity(CRCCoding.CRC16CCITT);

            Array.Copy(upgradeFile.FileContent, internalFlash.VectorTableOffset, vectorTableData, 0, internalFlash.VectorTableUsableLength);
            vectorTableCRC = (ushort)crcEntity.Sum(vectorTableData, 0, vectorTableData.Length);

            Array.Copy(upgradeFile.FileContent, internalFlash.CodeOffset, codeData, 0, codeLength);
            codeCRC = (ushort)crcEntity.Sum(codeData, 0, codeData.Length);

            return true;
        }

        /// <summary>
        /// 读度文件
        /// </summary>
        /// <param name="upgradeFile"></param>
        /// <param name="externalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="externalFlashData"></param>
        /// <param name="externalFlashCRC"></param>
        /// <returns></returns>
        private bool ReadUpgradeFile(UpgradeFile upgradeFile, CSTLoaderV2ExternalFlash externalFlash, string msgName, out byte[] externalFlashData, out ushort externalFlashCRC)
        {
            externalFlashData = null;
            externalFlashCRC = 0;
            if (upgradeFile.FileContent.Length <= externalFlash.FlashOffset)
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(msgName, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileError1));
                return false;
            }

            int externalFlashLength = upgradeFile.FileContent.Length - externalFlash.FlashOffset;
            if (externalFlashLength > externalFlash.FlashUsableLength)
            {
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(msgName, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileError3));
                return false;
            }
            externalFlashData = new byte[externalFlashLength];

            CRCEntity crcEntity = new CRCEntity(CRCCoding.CRC16CCITT);

            Array.Copy(upgradeFile.FileContent, externalFlash.FlashOffset, externalFlashData, 0, externalFlashLength);
            externalFlashCRC = (ushort)crcEntity.Sum(externalFlashData, 0, externalFlashData.Length);
            return true;
        }
        /// <summary>
        /// 下载内部Flash中断向量表
        /// </summary>
        /// <param name="vectorTableData"></param>
        /// <param name="vectorTableCRC"></param>
        /// <param name="internalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="msgContent"></param>
        /// <returns></returns>
        private bool DownloadVectorTableData(byte[] vectorTableData, ushort vectorTableCRC, CSTLoaderV2InternalFlash internalFlash, string msgName, string msgContent)
        {
            UpgradeMsg msg_VectorTableDownload = new UpgradeMsg(msgName);
            this.UpgradeInfo.AddUpgradeMsgs(msg_VectorTableDownload);
            while (true)
            {
                msg_VectorTableDownload.Content = msgContent;
                int vectorTableDataDownloadIndex = 0;
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;
                while (vectorTableDataDownloadIndex < vectorTableData.Length)
                {
                    int length = internalFlash.PackageMaxLength;
                    if (vectorTableData.Length - vectorTableDataDownloadIndex < internalFlash.PackageMaxLength)
                    {
                        length = vectorTableData.Length - vectorTableDataDownloadIndex;
                    }
                    byte[] data = new byte[length];
                    Array.Copy(vectorTableData, vectorTableDataDownloadIndex, data, 0, length);
                    if (Download(data, internalFlash.VectorTableAddress + vectorTableDataDownloadIndex, internalFlash.PackageMaxTimeout))
                    {

                    }
                    else
                    {

                    }
                    vectorTableDataDownloadIndex += length;
                    float percent = ((vectorTableDataDownloadIndex) / (float)vectorTableData.Length) * 100.0F;
                    this.UpgradeInfo.ProgressValue = percent;
                }
                this.UpgradeInfo.ProgressIsIndeterminate = true;
                msg_VectorTableDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.Finish;

                // 核验CRC校验
                ushort vectorTable_VerifyCRC = 0;
                if (VerifyCRC(internalFlash.VectorTableAddress, vectorTableData.Length, out vectorTable_VerifyCRC))
                {
                    if (vectorTableCRC == vectorTable_VerifyCRC)
                    {
                        msg_VectorTableDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.ConfirmOk;
                        break;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// 从设备下载内部Flash中断向量表
        /// </summary>
        /// <param name="vectorTableData"></param>
        /// <param name="vectorTableCRC"></param>
        /// <param name="internalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="msgContent"></param>
        /// <returns></returns>
        private bool DownloadSlaveDeviceVectorTableData(byte[] vectorTableData, ushort vectorTableCRC, CSTLoaderV2InternalFlash internalFlash, string msgName, string msgContent)
        {
            UpgradeMsg msg_VectorTableDownload = new UpgradeMsg(msgName);
            this.UpgradeInfo.AddUpgradeMsgs(msg_VectorTableDownload);
            while (true)
            {
                msg_VectorTableDownload.Content = msgContent;
                int vectorTableDataDownloadIndex = 0;
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;
                while (vectorTableDataDownloadIndex < vectorTableData.Length)
                {
                    int length = internalFlash.PackageMaxLength;
                    if (vectorTableData.Length - vectorTableDataDownloadIndex < internalFlash.PackageMaxLength)
                    {
                        length = vectorTableData.Length - vectorTableDataDownloadIndex;
                    }
                    byte[] data = new byte[length];
                    Array.Copy(vectorTableData, vectorTableDataDownloadIndex, data, 0, length);
                    if (SlaveDeviceDownload(data, internalFlash.VectorTableAddress + vectorTableDataDownloadIndex, internalFlash.PackageMaxTimeout * 2))
                    {

                    }
                    else
                    {

                    }
                    vectorTableDataDownloadIndex += length;
                    float percent = ((vectorTableDataDownloadIndex) / (float)vectorTableData.Length) * 100.0F;
                    this.UpgradeInfo.ProgressValue = percent;
                }
                this.UpgradeInfo.ProgressIsIndeterminate = true;
                msg_VectorTableDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.Finish;

                // 核验CRC校验
                ushort vectorTable_VerifyCRC = 0;
                if (SlaveDeviceVerifyCRC(internalFlash.VectorTableAddress, vectorTableData.Length, out vectorTable_VerifyCRC))
                {
                    if (vectorTableCRC == vectorTable_VerifyCRC)
                    {
                        msg_VectorTableDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.ConfirmOk;
                        break;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 下载内部Flash代码段
        /// </summary>
        /// <param name="codeData"></param>
        /// <param name="codeDataCRC"></param>
        /// <param name="internalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="msgContent"></param>
        /// <returns></returns>
        private bool DownloadCodeData(byte[] codeData, ushort codeDataCRC, CSTLoaderV2InternalFlash internalFlash, string msgName, string msgContent)
        {
            UpgradeMsg msg_CodeDataDownload = new UpgradeMsg(msgName);
            this.UpgradeInfo.AddUpgradeMsgs(msg_CodeDataDownload);
            while (true)
            {
                msg_CodeDataDownload.Content = msgContent;
                int codeDataDownloadIndex = 0;
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;
                while (codeDataDownloadIndex < codeData.Length)
                {
                    int length = internalFlash.PackageMaxLength;
                    if (codeData.Length - codeDataDownloadIndex < internalFlash.PackageMaxLength)
                    {
                        length = codeData.Length - codeDataDownloadIndex;
                    }
                    byte[] data = new byte[length];
                    Array.Copy(codeData, codeDataDownloadIndex, data, 0, length);
                    if (Download(data, internalFlash.CodeAddress + codeDataDownloadIndex, internalFlash.PackageMaxTimeout))
                    {

                    }
                    else
                    {

                    }
                    codeDataDownloadIndex += length;
                    float percent = ((codeDataDownloadIndex) / (float)codeData.Length) * 100.0F;
                    this.UpgradeInfo.ProgressValue = percent;
                }
                this.UpgradeInfo.ProgressIsIndeterminate = true;
                msg_CodeDataDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.Finish;

                // 核验CRC校验
                ushort codeData_VerifyCRC = 0;
                if (VerifyCRC(internalFlash.CodeAddress, codeData.Length, out codeData_VerifyCRC))
                {
                    if (codeDataCRC == codeData_VerifyCRC)
                    {
                        msg_CodeDataDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.ConfirmOk;
                        break;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// 从设备下载内部Flash代码段
        /// </summary>
        /// <param name="codeData"></param>
        /// <param name="codeDataCRC"></param>
        /// <param name="internalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="msgContent"></param>
        /// <returns></returns>
        private bool DownloadSlaveDeviceCodeData(byte[] codeData, ushort codeDataCRC, CSTLoaderV2InternalFlash internalFlash, string msgName, string msgContent)
        {
            UpgradeMsg msg_CodeDataDownload = new UpgradeMsg(msgName);
            this.UpgradeInfo.AddUpgradeMsgs(msg_CodeDataDownload);
            while (true)
            {
                msg_CodeDataDownload.Content = msgContent;
                int codeDataDownloadIndex = 0;
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;
                while (codeDataDownloadIndex < codeData.Length)
                {
                    int length = internalFlash.PackageMaxLength;
                    if (codeData.Length - codeDataDownloadIndex < internalFlash.PackageMaxLength)
                    {
                        length = codeData.Length - codeDataDownloadIndex;
                    }
                    byte[] data = new byte[length];
                    Array.Copy(codeData, codeDataDownloadIndex, data, 0, length);
                    if (SlaveDeviceDownload(data, internalFlash.CodeAddress + codeDataDownloadIndex, internalFlash.PackageMaxTimeout * 3))
                    {

                    }
                    else
                    {

                    }
                    codeDataDownloadIndex += length;
                    float percent = ((codeDataDownloadIndex) / (float)codeData.Length) * 100.0F;
                    this.UpgradeInfo.ProgressValue = percent;
                }
                this.UpgradeInfo.ProgressIsIndeterminate = true;
                msg_CodeDataDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.Finish;

                // 核验CRC校验
                ushort codeData_VerifyCRC = 0;
                if (SlaveDeviceVerifyCRC(internalFlash.CodeAddress, codeData.Length, out codeData_VerifyCRC))
                {
                    if (codeDataCRC == codeData_VerifyCRC)
                    {
                        msg_CodeDataDownload.Content += Bots.TestBench.Device.Base.Properties.Resources.ConfirmOk;
                        break;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 下载外部Flash
        /// </summary>
        /// <param name="externalFlashData"></param>
        /// <param name="externalFlashDataCRC"></param>
        /// <param name="externalFlash"></param>
        /// <param name="msgName"></param>
        /// <param name="msgContent"></param>
        /// <returns></returns>
        private bool DownloadExternalFlashData(byte[] externalFlashData, ushort externalFlashDataCRC, CSTLoaderV2ExternalFlash externalFlash, string msgName, string msgContent)
        {
            UpgradeMsg msg_externalFlashDataload = new UpgradeMsg(msgName);
            this.UpgradeInfo.AddUpgradeMsgs(msg_externalFlashDataload);
            while (true)
            {
                msg_externalFlashDataload.Content = msgContent;
                int externalFlashDataloadIndex = 0;
                this.UpgradeInfo.ProgressIsIndeterminate = false;
                this.UpgradeInfo.ProgressMaximum = 100;
                this.UpgradeInfo.ProgressMinimum = 0;
                this.UpgradeInfo.ProgressValue = 0;
                while (externalFlashDataloadIndex < externalFlashData.Length)
                {
                    int length = externalFlash.PackageMaxLength;
                    if (externalFlashData.Length - externalFlashDataloadIndex < externalFlash.PackageMaxLength)
                    {
                        length = externalFlashData.Length - externalFlashDataloadIndex;
                    }
                    byte[] data = new byte[length];
                    Array.Copy(externalFlashData, externalFlashDataloadIndex, data, 0, length);
                    if (Download(data, externalFlash.FlashAddress + externalFlashDataloadIndex, externalFlash.PackageMaxTimeout * 2))
                    {

                    }
                    else
                    {

                    }
                    externalFlashDataloadIndex += length;
                    float percent = ((externalFlashDataloadIndex) / (float)externalFlashData.Length) * 100.0F;
                    this.UpgradeInfo.ProgressValue = percent;
                }
                this.UpgradeInfo.ProgressIsIndeterminate = true;
                msg_externalFlashDataload.Content += Bots.TestBench.Device.Base.Properties.Resources.Finish;

                // 核验CRC校验
                ushort externalFlashData_VerifyCRC = 0;
                if (VerifyCRC(externalFlash.FlashAddress, externalFlashData.Length, out externalFlashData_VerifyCRC))
                {
                    if (externalFlashData_VerifyCRC == externalFlashDataCRC)
                    {
                        msg_externalFlashDataload.Content += Bots.TestBench.Device.Base.Properties.Resources.ConfirmOk;
                        break;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// 检查串口
        /// </summary>
        /// <param name="maxWaitTime"></param>
        /// <returns></returns>
        private bool DetectSerialPort(int maxWaitTime)
        {
            try
            {
                SerialPortConfig serialPortConfig = this.CommConfig as SerialPortConfig;
                if (serialPortConfig != null)
                {
                    DateTime begin = DateTime.Now;
                    while ((DateTime.Now - begin).TotalMilliseconds < maxWaitTime)
                    {
                        string portName = System.IO.Ports.SerialPort.GetPortNames().Where(n => n == serialPortConfig.SPName).FirstOrDefault();
                        if (!string.IsNullOrEmpty(portName))
                        {
                            Xmas11.Comm.Core.CommSettings st = this.CommConfig.GetCommSettings();
                            this.CommInstance = new DPG2SCPI(st);
                            try
                            {
                                if (!this.CommInstance.Connected && this.CommInstance.Open())
                                {
                                    return true;
                                }
                            }
                            catch
                            {
                            }
                        }
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return false;
        }

        /// <summary>
        /// 与Bootloader握手
        /// </summary>
        /// <returns></returns>
        private bool Handshake(int maxWaitTime)
        {
            try
            {
                DateTime begin = DateTime.Now;
                while ((DateTime.Now - begin).TotalMilliseconds < maxWaitTime)
                {
                    if (DPG2 != null && DPG2.CommInstance != null && DPG2.CommInstance.IsOpen)
                    {
                        int count = 0;
                        DPG2.CommInstance.ClearAllBuffer();
                        CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x00);
                        byte[] bytes = cmd.ToBytes().ToArray();
                        DPG2.CommInstance.Write(bytes);
                        count++;
                        while ((DateTime.Now - begin).TotalMilliseconds < maxWaitTime)
                        {
                            int bytesToRead = DPG2.CommInstance.Available;
                            if (bytesToRead > 0)
                            {
                                byte[] buffer = new byte[bytesToRead];
                                DPG2.CommInstance.Read(out buffer);
                                return true;
                            }
                            if (count % 5 == 0)
                            {
                                DPG2.CommInstance.ClearAllBuffer();
                                DPG2.CommInstance.Write(bytes);
                            }
                            System.Threading.Thread.Sleep(10);
                            count++;
                        }
                    }
                    System.Threading.Thread.Sleep(10);
                }
            }
            catch
            {

            }
            return false;
        }

        /// <summary>
        /// 获取Bootloader版本
        /// </summary>
        /// <param name="loaderVersion"></param>
        /// <returns></returns>
        private bool GetLoaderVersion(out string loaderVersion)
        {
            loaderVersion = string.Empty;
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x02);
            ;
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            byte[] data = response.GetData();
            if (data.Length < 1)
            {
                return false;
            }
            loaderVersion = System.Text.ASCIIEncoding.ASCII.GetString(data);
            return true;
        }

        /// <summary>
        /// 从设备开始升级
        /// </summary>
        /// <param name="slaveDeviceID"></param>
        /// <returns></returns>
        private bool SlaveDeviceStartUpdate(int slaveDeviceID)
        {
            byte[] requestData = new byte[2];
            requestData[0] = 0x01;
            requestData[1] = (byte)(slaveDeviceID);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0xF3, requestData);
            if (!Execute(cmd, 2000, out CSTLoaderV2Response response))
            {

                return false;
            }
            return true;
        }
        /// <summary>
        /// 获取从设备Bootloader版本
        /// </summary>
        /// <param name="loaderVersion"></param>
        /// <returns></returns>
        private bool GetSlaveDeviceLoaderVersion(out string loaderVersion)
        {
            loaderVersion = string.Empty;
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x02, true);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            byte[] data = response.GetRetransmissionData();
            if (data.Length < 1)
            {
                return false;
            }
            loaderVersion = System.Text.ASCIIEncoding.ASCII.GetString(data);
            return true;
        }
        /// <summary>
        /// 获取内部Flash下载参数
        /// </summary>
        /// <param name="internalFlash"></param>
        /// <returns></returns>
        private bool GetInternalFlashParameters(out CSTLoaderV2InternalFlash internalFlash)
        {
            internalFlash = new CSTLoaderV2InternalFlash();
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x01, new byte[] { 0x00 });
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }

            byte[] data = response.GetData();
            if (data.Length < 30)
            {
                return false;
            }
            internalFlash.PackageMaxLength = IBitConverter.ToInt16(data, 0, false);
            internalFlash.PackageMaxTimeout = IBitConverter.ToInt16(data, 2, false);
            internalFlash.ErasureFlashTimeout = IBitConverter.ToInt16(data, 4, false);
            internalFlash.VectorTableAddress = IBitConverter.ToInt32(data, 6, false);
            internalFlash.VectorTableUsableLength = IBitConverter.ToInt32(data, 10, false);
            internalFlash.VectorTableOffset = IBitConverter.ToInt32(data, 14, false);
            internalFlash.CodeAddress = IBitConverter.ToInt32(data, 18, false);
            internalFlash.CodeUsableLength = IBitConverter.ToInt32(data, 22, false);
            internalFlash.CodeOffset = IBitConverter.ToInt32(data, 26, false);
            return true;
        }
        /// <summary>
        /// 获取外部Flash下载参数
        /// </summary>
        /// <param name="externalFlash"></param>
        /// <returns></returns>
        private bool GetExternalFlashParameters(out CSTLoaderV2ExternalFlash externalFlash)
        {
            externalFlash = new CSTLoaderV2ExternalFlash();
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x01, new byte[] { 0x01 });
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            byte[] data = response.GetData();
            if (data.Length < 18)
            {
                return false;
            }
            externalFlash.PackageMaxLength = IBitConverter.ToInt16(data, 0, false);
            externalFlash.PackageMaxTimeout = IBitConverter.ToInt16(data, 2, false);
            externalFlash.ErasureFlashTimeoutFor1M = IBitConverter.ToInt16(data, 4, false);
            externalFlash.FlashAddress = IBitConverter.ToInt32(data, 6, false);
            externalFlash.FlashUsableLength = IBitConverter.ToInt32(data, 10, false);
            externalFlash.FlashOffset = IBitConverter.ToInt32(data, 14, false);
            return true;
        }

        /// <summary>
        /// 获取从设备内部Flash下载参数
        /// </summary>
        /// <param name="internalFlash"></param>
        /// <returns></returns>
        private bool GetSlaveDeviceInternalFlashParameters(out CSTLoaderV2InternalFlash internalFlash)
        {
            internalFlash = new CSTLoaderV2InternalFlash();
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x01, new byte[] { 0x00 }, true);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }

            byte[] data = response.GetRetransmissionData();
            if (data.Length < 30)
            {
                return false;
            }
            internalFlash.PackageMaxLength = IBitConverter.ToInt16(data, 0, false);
            internalFlash.PackageMaxTimeout = IBitConverter.ToInt16(data, 2, false);
            internalFlash.ErasureFlashTimeout = IBitConverter.ToInt16(data, 4, false);
            internalFlash.VectorTableAddress = IBitConverter.ToInt32(data, 6, false);
            internalFlash.VectorTableUsableLength = IBitConverter.ToInt32(data, 10, false);
            internalFlash.VectorTableOffset = IBitConverter.ToInt32(data, 14, false);
            internalFlash.CodeAddress = IBitConverter.ToInt32(data, 18, false);
            internalFlash.CodeUsableLength = IBitConverter.ToInt32(data, 22, false);
            internalFlash.CodeOffset = IBitConverter.ToInt32(data, 26, false);
            return true;
        }

        /// <summary>
        /// 擦除内部Flash
        /// </summary>
        /// <param name="erasureFlashTimeout"></param>
        /// <returns></returns>
        private bool ErasureInternalFlash(int erasureFlashTimeout)
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0A);
            if (!Execute(cmd, erasureFlashTimeout, out CSTLoaderV2Response response))
            {

                return false;
            }
            return true;
        }

        /// <summary>
        /// 擦除从设备内部Flash
        /// </summary>
        /// <param name="erasureFlashTimeout"></param>
        /// <returns></returns>
        private bool ErasureSlaveDeviceInternalFlash(int erasureFlashTimeout)
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0A, true);
            if (!Execute(cmd, erasureFlashTimeout, out CSTLoaderV2Response response))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 擦除外部Flash
        /// </summary>
        /// <param name="erasureFlashTimeoutFor1M"></param>
        /// <param name="externalFlashAddress"></param>
        /// <param name="externalFlashDataLength"></param>
        /// <returns></returns>
        private bool ErasureExternalFlash(int erasureFlashTimeoutFor1M, int externalFlashAddress, int externalFlashDataLength)
        {
            byte[] requestData = new byte[8];
            Array.Copy(IBitConverter.GetBytes(externalFlashAddress, false), 0, requestData, 0, 4);
            Array.Copy(IBitConverter.GetBytes(externalFlashDataLength, false), 0, requestData, 4, 4);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0A, requestData);
            int erasureFlashTimeout = (externalFlashDataLength / (1024 * 1024) + 1) * erasureFlashTimeoutFor1M;
            if (!Execute(cmd, erasureFlashTimeout, out CSTLoaderV2Response response))
            {

                return false;
            }
            return true;
        }

        /// <summary>
        /// 下载固件文件数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="address"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private bool Download(byte[] data, int address, int timeout)
        {
            int length = data.Length;
            byte[] package = new byte[4 + length];
            Array.Copy(IBitConverter.GetBytes(address, false), 0, package, 0, 4);
            Array.Copy(data, 0, package, 4, length);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0B, package);
            if (!Execute(cmd, timeout, out CSTLoaderV2Response response))
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// 从设备下载固件文件数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="address"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private bool SlaveDeviceDownload(byte[] data, int address, int timeout)
        {
            int length = data.Length;
            byte[] package = new byte[4 + length];
            Array.Copy(IBitConverter.GetBytes(address, false), 0, package, 0, 4);
            Array.Copy(data, 0, package, 4, length);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0B, package, true);
            if (!Execute(cmd, timeout, out CSTLoaderV2Response response))
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// 核验CRC
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="crc"></param>
        /// <returns></returns>
        private bool VerifyCRC(int address, int length, out ushort crc)
        {
            crc = 0;
            int timeout = (length / (1024 * 1024) + 1) * 2000;
            byte[] requestData = new byte[8];
            Array.Copy(IBitConverter.GetBytes(address, false), 0, requestData, 0, 4);
            Array.Copy(IBitConverter.GetBytes(length, false), 0, requestData, 4, 4);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x04, requestData);
            if (!Execute(cmd, timeout, out CSTLoaderV2Response response))
            {
                return false;
            }

            byte[] data = response.GetData();
            if (data.Length < 2)
            {
                return false;
            }
            crc = IBitConverter.ToUInt16(data, 0, false);
            return true;
        }
        /// <summary>
        /// 从设备核验CRC
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="crc"></param>
        /// <returns></returns>
        private bool SlaveDeviceVerifyCRC(int address, int length, out ushort crc)
        {
            crc = 0;
            int timeout = (length / (1024 * 1024) + 1) * 2000;
            byte[] requestData = new byte[8];
            Array.Copy(IBitConverter.GetBytes(address, false), 0, requestData, 0, 4);
            Array.Copy(IBitConverter.GetBytes(length, false), 0, requestData, 4, 4);
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x04, requestData, true);
            if (!Execute(cmd, timeout, out CSTLoaderV2Response response))
            {
                return false;
            }

            byte[] data = response.GetRetransmissionData();
            if (data.Length < 2)
            {
                return false;
            }
            crc = IBitConverter.ToUInt16(data, 0, false);
            return true;
        }

        /// <summary>
        /// 下载完成确认
        /// </summary>
        /// <returns></returns>
        private bool DownloadConfirm()
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0C);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从设备下载完成确认
        /// </summary>
        /// <returns></returns>
        private bool SlaveDeviceDownloadConfirm()
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0C, true);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 运行程序
        /// </summary>
        /// <returns></returns>
        private bool RunApplication()
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0F);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从设备运行程序
        /// </summary>
        /// <returns></returns>
        private bool RunSlaveDeviceApplication()
        {
            CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x0F, true);
            if (!Execute(cmd, 1000, out CSTLoaderV2Response response))
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 执行请求
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="timeoutMilliseconds"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool Execute(CSTLoaderV2Request cmd, int timeoutMilliseconds, out CSTLoaderV2Response response)
        {
            response = null;
            byte[] data = cmd.ToBytes().ToArray();
            lock (DPG2.ExecuteLock)
            {
                DPG2.CommInstance.ClearAllBuffer();
                DPG2.CommInstance.Write(data);
                DateTime start = DateTime.Now;
                List<byte> tmpBytes = new List<byte>();
                while (true)
                {
                    if (DPG2.CommInstance.Available > 0)
                    {
                        byte[] buffer = null;
                        int readCount = DPG2.CommInstance.Read(out buffer);
                        if (readCount > 0)
                        {
                            tmpBytes.AddRange(buffer);
                        }
                    }
                    if (tmpBytes.Count > 0)
                    {
                        response = CSTLoaderV2Response.Parse(tmpBytes);
                        if (response != null)
                        {
                            if (response.IsSuccessful() && response.GetFunctionCode() == cmd.GetFunctionCode())
                            {
                                if (cmd.IsRetransmission && response.SlaveDeviceFunctionCode == cmd.SlaveDeviceFunctionCode)
                                {
                                    return true;
                                }
                                else
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(10);
                    TimeSpan span = DateTime.Now - start;
                    if (span.TotalMilliseconds >= timeoutMilliseconds)
                    {
                        break;
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
