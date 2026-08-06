using Bots.TestBench.Device.Base;
using Bots.TestBench.Device.Base.Comm;
using Bots.TestBench.Device.Upgrade;
using Bots.TestBench.Model.Scripts;
using Bots.TestBench.Util;
using Bots.TestBench.Util.CRC;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Data.DPSEX;
using Xmas11.Comm.Devices;
using Xmas11.Domain.Mechanics;

namespace Bots.TestBench.Device
{
    /// <summary>
    /// 被检设备DPSEX模块
    /// </summary>
    [Serializable]
    public class DPSEX : UpgradeDevice
    {
        #region 构造函数 

        /// <summary>
        /// 构造函数 
        /// </summary>
        public DPSEX()
        {
            this.DeviceType = DeviceType.DUT;
        }

        #endregion

        #region 属性

        /// <summary>
        /// DPS模块
        /// </summary>
        public Xmas11.Comm.Devices.DPSEX DPS
        {
            get
            {
                if (this.CommInstance == null)
                {
                    return null;
                }
                return this.CommInstance as Xmas11.Comm.Devices.DPSEX;
            }
        }
        /// <summary>
        /// 获取设备图片
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Bitmap GetDeviceMainImage()
        {
            return Bots.TestBench.Device.Properties.Resource.main;
        }
        #endregion

        #region 方法
        public bool DirectOpen()
        {
            ConnectStatus = ConnectStatus.Connectting;
            try
            {
                AddressChanged();
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                var SPC = this.CommConfig as SerialPortConfig;
                this.CommInstance = new Xmas11.Comm.Devices.DPSEX(SPC.SPName, SPC.Bauds, SPC.DataBits, (StopBits)Enum.Parse(typeof(StopBits), SPC.StopBits), (Parity)Enum.Parse(typeof(Parity), SPC.Parity));
                var res = this.CommInstance.Open();
                if (res)
                {
                    ConnectStatus = ConnectStatus.Connected;
                }
                else
                {
                    ConnectStatus = ConnectStatus.DisConnected;
                }
                return res;
            }
            catch (Exception ex)
            {
                ConnectStatus = ConnectStatus.DisConnected;
                return false;
            }
        }
        /// <summary>
        /// 打开，DPSEX模块用的是网络通讯，其它测试用的是串口通讯。需要做兼容
        /// </summary>
        /// <returns></returns>
        public override bool Open()
        {
            ConnectStatus = ConnectStatus.Connectting;
            try
            {
                AddressChanged();
                if (this.CommInstance != null)
                {
                    this.CommInstance.Close();
                    this.CommInstance = null;
                }
                bool openResult = false;
                EthernetConfig ef = this.CommConfig.Clone() as EthernetConfig;
                if (ef != null)
                {
                    try
                    {
                        this.CommInstance = new Xmas11.Comm.Devices.DPSEX(this.CommConfig.GetCommSettings());
                        this.CommInstance.Open();
                        var vertemp = DPS.GetVersion();
                        if (vertemp.IsCorrect && (vertemp.Result.ToUpper().Contains("DPS-EX") || vertemp.Result.ToUpper().Contains("CDP") || vertemp.Result.ToUpper().Contains("DS")))
                        {
                            //用于CPDM多工位测试工装
                            if (!string.IsNullOrWhiteSpace(this.CommConfig.DevSn))
                            {
                                string snstr = "";
                                GetSerialNumber(out snstr);
                                if (snstr == this.CommConfig.DevSn || this.CommConfig.DevSn.ToLower().Contains(snstr.ToLower().Trim()))
                                {

                                    ConnectStatus = ConnectStatus.Connected;
                                    return true;
                                }
                                else
                                {
                                    this.CommInstance.Close();
                                    this.CommInstance = null;
                                }
                            }
                            else
                            {
                                ConnectStatus = ConnectStatus.Connected;
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {

                        throw;
                    }

                }
                else
                {
                    SerialPortConfig S1Config = this.CommConfig.Clone() as SerialPortConfig;
                    if (S1Config != null && !string.IsNullOrWhiteSpace(S1Config.SPName))
                    {
                        try
                        {
                            this.CommInstance = new Xmas11.Comm.Devices.DPSEX(this.CommConfig.GetCommSettings());
                            this.CommInstance.Open();
                            var vertemp = DPS.GetVersion();
                            if (vertemp.IsCorrect && (vertemp.Result.ToUpper().Contains("DPS-EX") || vertemp.Result.ToUpper().Contains("CDP") || vertemp.Result.ToUpper().Contains("DS")))
                            {
                                //增加对编号的检验，用于P23工装
                                if (!string.IsNullOrWhiteSpace(this.CommConfig.DevSn))
                                {
                                    string snstr = "";
                                    GetSerialNumber(out snstr);
                                    if (snstr == this.CommConfig.DevSn || this.CommConfig.DevSn.ToLower().Contains(snstr.ToLower().Trim()))
                                    {
                                        ConnectStatus = ConnectStatus.Connected;
                                        return true;
                                    }
                                    else
                                    {
                                        this.CommInstance.Close();
                                        this.CommInstance = null;
                                    }
                                }
                                else
                                {
                                    ConnectStatus = ConnectStatus.Connected;
                                    return true;
                                }
                            }
                            else
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    var comlist = SerialPortConfig.GetPortNames().Reverse();
                    foreach (var item in comlist)
                    {
                        //SerialPortConfig S1Config = this.CommConfig.Clone() as SerialPortConfig;
                        S1Config.SPName = item;

                        this.CommInstance = new Xmas11.Comm.Devices.DPSEX((S1Config).GetCommSettings());
                        if (!this.CommInstance.Connected)
                        {
                            try
                            {
                                openResult = this.CommInstance.Open();
                                if (openResult)
                                {
                                    var vertemp = DPS.GetVersion();
                                    if (vertemp.IsCorrect && vertemp.Result.ToUpper().Contains("DPS-EX"))
                                    {
                                        //增加对编号的检验，用于P23工装
                                        if (!string.IsNullOrWhiteSpace(this.CommConfig.DevSn))
                                        {
                                            string snstr = "";
                                            GetSerialNumber(out snstr);
                                            if (snstr == this.CommConfig.DevSn)
                                            {
                                                ConnectStatus = ConnectStatus.Connected;
                                                return true;
                                            }
                                            else
                                            {
                                                this.CommInstance.Close();
                                                this.CommInstance = null;
                                            }
                                        }
                                        else
                                        {
                                            ConnectStatus = ConnectStatus.Connected;
                                            return true;
                                        }
                                    }
                                    else
                                    {
                                        this.CommInstance.Close();
                                        this.CommInstance = null;
                                    }
                                }
                                else
                                {
                                    this.CommInstance.Close();
                                    this.CommInstance = null;
                                }
                            }
                            catch
                            {
                                this.CommInstance.Close();
                                this.CommInstance = null;
                                openResult = false;
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                ConnectStatus = ConnectStatus.Error;
                return false;
            }
            bool isExist = false;
            try
            {
                isExist = this.CommInstance == null ? false : this.CommInstance.IsExist();
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

        /// <summary>
        /// 初始化被检
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
                //激励值
                double oriv = double.NaN;
                if (GetSensorPowerSupplyValue(out oriv))
                {
                    this.DUT.AddInfo("ORIV", oriv.ToString());
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
        /// 设备复位
        /// </summary>
        /// <returns></returns>
        public bool Reset()
        {
            iResponse result = DPS.Reset();
            return result.IsCorrect;
        }
        /// <summary>
        /// 设备重启
        /// </summary>
        /// <returns></returns>
        public bool Restart()
        {
            iResponse result = DPS.Restart();
            return result.IsCorrect;
        }
        /// <summary>
        /// 是否在线
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsOnLine(out string name)
        {
            name = string.Empty;
            if (GetCode(out name))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        #region 序列号
        /// <summary>
        /// 获取序列号
        /// </summary>
        /// <returns></returns>
        public bool GetSerialNumber(out string code)
        {
            code = string.Empty;
            iResponse<string> result = DPS.GetDeviceSerialNumber();
            if (!result.IsCorrect)
            {
                return false;
            }
            code = result.Result;
            return true;
        }

        public ScriptHelperKVP GetSerialNumber_KVP(out string code)
        {
            var res = DPS.GetDeviceSerialNumber();
            code = res.IsCorrect ? res.Result : string.Empty;
            return new ScriptHelperKVP("获取设备序列号:" + code, res.IsCorrect);
        }
        /// <summary>
        /// 设备序列号
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetSerialNumber(string code)
        {
            iResponse result = DPS.SetDeviceSerialNumber(code);
            return result.IsCorrect;
        }
        #endregion

        #region 压力类型
        /// <summary>
        /// 设置压力类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool SetPressureType(string pressureType)
        {
            Xmas11.Comm.Data.Common.PressureType pt = Xmas11.Comm.Data.Common.PressureType.G;
            switch (pressureType.ToUpper())
            {
                case "G":
                    pt = Xmas11.Comm.Data.Common.PressureType.G;
                    break;
                case "A":
                    pt = Xmas11.Comm.Data.Common.PressureType.A;
                    break;
                case "D":
                    pt = Xmas11.Comm.Data.Common.PressureType.D;
                    break;
                case "S":
                    pt = Xmas11.Comm.Data.Common.PressureType.G;
                    break;
                case "V":
                    pt = Xmas11.Comm.Data.Common.PressureType.G;
                    break;
                default:
                    pt = Xmas11.Comm.Data.Common.PressureType.G;
                    break;

            }
            return SetPressureType(pt);
        }

        /// <summary>
        /// 设置压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool SetPressureType(Xmas11.Comm.Data.Common.PressureType pressureType)
        {
            if (pressureType == Xmas11.Comm.Data.Common.PressureType.S)
            {
                pressureType = Xmas11.Comm.Data.Common.PressureType.G;
            }
            else if (pressureType == Xmas11.Comm.Data.Common.PressureType.V)
            {
                pressureType = Xmas11.Comm.Data.Common.PressureType.G;
            }
            else if (pressureType == Xmas11.Comm.Data.Common.PressureType.UnKnown)
            {
                pressureType = Xmas11.Comm.Data.Common.PressureType.G;
            }
            iResponse result = DPS.SetPressureType(pressureType);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetPressureType(out Xmas11.Comm.Data.Common.PressureType pressureType)
        {
            pressureType = Xmas11.Comm.Data.Common.PressureType.UnKnown;
            iResponse<Xmas11.Comm.Data.Common.PressureType> result = DPS.GetPressureType();
            if (result.IsCorrect)
            {
                pressureType = result.Result;
            }

            return result.IsCorrect;
        }
        public ScriptHelperKVP GetPressureType_KVP(out PressureType pressureType)
        {
            var res = DPS.GetPressureType();
            pressureType = res.IsCorrect ? res.Result : PressureType.UnKnown;
            return new ScriptHelperKVP("获取压力类型:" + pressureType, res.IsCorrect);
        }
        /// <summary>
        /// 获取压力类型
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetPressureType(out string pressureType)
        {
            Xmas11.Comm.Data.Common.PressureType type = Xmas11.Comm.Data.Common.PressureType.UnKnown;
            pressureType = type.ToString();
            bool result = GetPressureType(out type);
            if (result)
            {
                pressureType = type.ToString();
            }
            return result;
        }


        /// <summary>
        /// 获取模块信息
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public bool GetPressureRangeDetailedInfo(out PressureRangeDetailedInfo pressureType)
        {
            pressureType = new PressureRangeDetailedInfo();
            iResponse<PressureRangeDetailedInfo> result = DPS.GetPressureRangeDetailedInfo();
            if (result.IsCorrect)
            {
                pressureType = result.Result;
            }

            return result.IsCorrect;
        }
        public ScriptHelperKVP GetPressureRangeDetailedInfo_KVP(out PressureRangeDetailedInfo pressureType)
        {
            iResponse<PressureRangeDetailedInfo> result = DPS.GetPressureRangeDetailedInfo();
            pressureType = result.IsCorrect ? result.Result : new PressureRangeDetailedInfo();
            return new ScriptHelperKVP("获取模块信息:" + PRDItoString(pressureType), result.IsCorrect);
        }

        /// <summary>
        /// 获取指令结果
        /// </summary>
        /// <param name="pressureType"></param>
        /// <returns></returns>
        public string GetCmdResult(string cmd)
        {
            iResponse<string> result = DPS.SendCMD(cmd);
            if (result.IsCorrect)
            {
                return result.Result;
            }
            return "";
        }
        #endregion

        #region 生产日期

        /// <summary>
        /// 获取生产日期
        /// </summary>
        /// <param name="manufactureDate"></param>
        /// <returns></returns>
        public bool GetManufactureDate(out DateTime manufactureDate)
        {
            manufactureDate = DateTime.MinValue;
            iResponse<DateTime> result = DPS.GetManufactureDate();
            if (!result.IsCorrect)
            {
                return false;
            }
            manufactureDate = result.Result;
            return true;
        }
        public ScriptHelperKVP GetManufactureDate_KVP(out DateTime manufactureDate)
        {
            iResponse<DateTime> result = DPS.GetManufactureDate();
            manufactureDate = result.IsCorrect ? result.Result : DateTime.MinValue;
            return new ScriptHelperKVP("获取生产日期:" + result.Result.ToString(), result.IsCorrect);
        }
        /// <summary>
        /// 设置生产日期
        /// </summary>
        /// <param name="manufactureDate"></param>
        /// <returns></returns>
        public bool SetManufactureDate(DateTime manufactureDate)
        {
            iResponse result = DPS.SetManufactureDate(manufactureDate);
            return result.IsCorrect;
        }

        #endregion

        #region 过压记录

        /// <summary>
        /// 清空过压记录
        /// </summary>
        /// <returns></returns>
        public bool ClearOverpressureData()
        {
            iResponse result = DPS.ClearOverpressureData();
            return result.IsCorrect;
        }

        #endregion

        #region 设备编号
        /// <summary>
        /// 设备编号
        /// </summary>
        /// <param name="devType"></param>
        /// <returns></returns>
        public bool GetCode(out string code)
        {
            return GetSerialNumber(out code);
        }
        #endregion

        #region 版本相关
        /// <summary>
        /// 获取软件版本
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool GetVersion(out string version)
        {
            iResponse<string> response = DPS.GetVersion();
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
        public ScriptHelperKVP GetVersion_KVP(out string version)
        {
            var res = DPS.GetVersion();
            version = res.IsCorrect ? res.Result : string.Empty;
            return new ScriptHelperKVP("获取软件版本:" + version, res.IsCorrect);
        }
        public class AccuracyBand
        {
            public enum BandType
            {
                Unknown,
                FullScale,
                Reading,
                WithUnit,
            }
            public double BandBegin;
            public double BandEnd;
            public double AccuracyPercent;
            public BandType Type;
            public bool IsFullBand = false;
            public bool IsValid = true;
            public string UnitString;
            public override string ToString()
            {
                if (Type == BandType.WithUnit)
                {
                    return "精度带单位,为" + UnitString;
                }
                if (IsFullBand)
                {
                    return string.Format("精度类型：{0} 量程范围:全量程 量程精度：{1}%", Type, AccuracyPercent);
                }
                return string.Format("精度类型：{0} 量程开始：{1}% 量程结束：{2}% 量程精度：{3}%", Type, BandBegin, BandEnd, AccuracyPercent);
            }
        }
        public ScriptHelperKVP GetAccuracyBands(out List<AccuracyBand> bands, out string originalAccuracyInfoStr)
        {
            originalAccuracyInfoStr = string.Empty;
            bands = new List<AccuracyBand>();
            var res = DPS.GetAccuracyInfo();
            if (res.IsCorrect)
            {
                originalAccuracyInfoStr = res.Result;

                // --- "NoAccuracy" → 无精度带，直接返回空列表 ---
                if (res.Result.IndexOf("NoAccuracy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bands.Clear();
                    return new ScriptHelperKVP("获取精度个数:0,设备返回无精度信息(NoAccuracy)", res.IsCorrect);
                }

                // --- 绝对值准确度（单位检测） ---
                // 若含 "Pa"（包括 "kPa"、"MPa" 等），视为绝对值准确度
                if (res.Result.Contains("Pa"))
                {
                    bands.Add(new AccuracyBand
                    {
                        Type = AccuracyBand.BandType.WithUnit,
                        UnitString = "±" + res.Result
                    });
                    return new ScriptHelperKVP("获取精度个数:1");
                }
                if (res.Result.Contains("pa"))
                {
                    return new ScriptHelperKVP("获取单位名称为小写pa,请核对后重新检测,本次未返回有效精度值" );
                }

                // --- IS 分段准确度 ---
                // 格式：如 "0.1% IS-20" → 第一段 0%~20% FS(0.1%*20%=0.02%FS)，第二段 20%~100% RD(0.1%RD)
                var isMatch = System.Text.RegularExpressions.Regex.Match(res.Result, @"^([\d.]+)%\s+IS-([\d.]+)$");
                if (isMatch.Success)
                {
                    double accPct = double.Parse(isMatch.Groups[1].Value);
                    double splitPct = double.Parse(isMatch.Groups[2].Value);
                    // 第一段：0~splitPct，满量程百分比 = accPct * splitPct / 100
                    bands.Add(new AccuracyBand
                    {
                        IsFullBand = false,
                        BandBegin = 0,
                        BandEnd = splitPct,
                        AccuracyPercent = accPct * splitPct / 100.0,
                        Type = AccuracyBand.BandType.FullScale,
                    });
                    // 第二段：splitPct~100，读数百分比 = accPct
                    bands.Add(new AccuracyBand
                    {
                        IsFullBand = false,
                        BandBegin = splitPct,
                        BandEnd = 100,
                        AccuracyPercent = accPct,
                        Type = AccuracyBand.BandType.Reading,
                    });
                    return new ScriptHelperKVP("获取精度个数:" + bands.Count);
                }

                // --- "or" 格式（Additel 全量程二选一精度） ---
                if (res.Result.Contains(" or "))
                {
                    var spl1 = res.Result.Split(' ');
                    foreach (var item in spl1)
                    {
                        if (item != "or")
                        {
                            var spl2 = item.Split('%');
                            if (spl2.Length > 1)
                            {
                                var band = new AccuracyBand
                                {
                                    IsFullBand = true,
                                    AccuracyPercent = double.Parse(spl2[0]),
                                };
                                switch (spl2[1])
                                {
                                    case "FS":
                                    case "F":
                                        band.Type = AccuracyBand.BandType.FullScale;
                                        break;
                                    case "RD":
                                    case "rdg":
                                        band.Type = AccuracyBand.BandType.Reading;
                                        break;
                                    default:
                                        band.IsValid = false;
                                        break;
                                }
                                bands.Add(band);
                            }
                        }
                    }
                }
                else
                {
                    // --- 分段准确度 / 复合准确度 / 绝对值准确度（无Pa） ---
                    // 格式举例：
                    //   "0%~50% 0.005%FS;50%~100% 0.01%RD"   (分段)
                    //   "0.02%FS+0.05%RD"                     (复合)
                    //   "10 mbar" / "±22 Pa"                  (绝对值)
                    var splSegments = res.Result.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var segment in splSegments)
                    {
                        var segTrimmed = segment.Trim();

                        // 若不含 % 且非空→绝对值准确度（如 "10 mbar"、"0.5 psi"）
                        if (!segTrimmed.Contains("%") && !string.IsNullOrWhiteSpace(segTrimmed))
                        {
                            bands.Add(new AccuracyBand
                            {
                                Type = AccuracyBand.BandType.WithUnit,
                                UnitString = "±" + segTrimmed
                            });
                            continue;
                        }

                        // 复合准确度：用 "+" 连接多个精度（如 "0.02%FS+0.05%RD"）
                        if (segTrimmed.Contains("+"))
                        {
                            var compoundParts = segTrimmed.Split('+');
                            foreach (var cp in compoundParts)
                            {
                                var cpt = cp.Trim();
                                var cpParts = cpt.Split('%');
                                if (cpParts.Length > 1 && double.TryParse(cpParts[0], out double cpVal))
                                {
                                    var band = new AccuracyBand
                                    {
                                        IsFullBand = true,
                                        AccuracyPercent = cpVal,
                                    };
                                    switch (cpParts[1])
                                    {
                                        case "FS": case "F": band.Type = AccuracyBand.BandType.FullScale; break;
                                        case "RD": case "rdg": band.Type = AccuracyBand.BandType.Reading; break;
                                        default: band.IsValid = false; break;
                                    }
                                    if (band.IsValid) bands.Add(band);
                                }
                            }
                            continue;
                        }

                        // 单精度格式（含 %，无 + 无 ~）：如 "0.01%FS"
                        if (!segTrimmed.Contains("~"))
                        {
                            var parts = segTrimmed.Split('%');
                            if (parts.Length > 1 && double.TryParse(parts[0], out double pctVal))
                            {
                                var band = new AccuracyBand { IsFullBand = true, AccuracyPercent = pctVal };
                                switch (parts[1])
                                {
                                    case "FS": case "F": band.Type = AccuracyBand.BandType.FullScale; break;
                                    case "RD": case "rdg": band.Type = AccuracyBand.BandType.Reading; break;
                                    default: band.IsValid = false; break;
                                }
                                if (band.IsValid) bands.Add(band);
                            }
                            continue;
                        }

                        // 分段格式：如 "0%~50% 0.005%FS" 或 "(0~20)% 0.02%FS"
                        var spl3 = segTrimmed.Split(' ');
                        if (spl3.Length > 1)
                        {
                            // 去除括号（兼容 "(0~20)%" 格式）
                            var rangePart = spl3[0].Trim().TrimStart('(').TrimEnd(')');
                            var accPart = spl3[1].Trim();

                            var spl4 = rangePart.Split('~');
                            if (spl4.Length > 1)
                            {
                                var spl5 = spl4[0].Split('%');
                                if (!double.TryParse(spl5[0], out double bandBegin))
                                {
                                    bandBegin = 0;
                                }
                                var spl6 = spl4[1].Split('%');
                                if (!double.TryParse(spl6[0], out double bandEnd))
                                {
                                    bandEnd = 100;
                                }

                                var spl7 = accPart.Split('%');
                                if (spl7.Length > 1 && double.TryParse(spl7[0], out double accPct))
                                {
                                    var band = new AccuracyBand
                                    {
                                        BandBegin = bandBegin,
                                        BandEnd = bandEnd,
                                        AccuracyPercent = accPct,
                                    };
                                    switch (spl7[1])
                                    {
                                        case "FS": case "F": band.Type = AccuracyBand.BandType.FullScale; break;
                                        case "RD": case "rdg": band.Type = AccuracyBand.BandType.Reading; break;
                                        default: band.IsValid = false; break;
                                    }
                                    if (band.IsValid) bands.Add(band);
                                }
                            }
                        }
                    }
                }

            // --- 后处理：合并值+类型相同的精度带（取范围并集） ---
            MergeCompatibleBands(bands);
            }
            return new ScriptHelperKVP("获取精度个数:" + bands.Count+",精度原始字符串为:"+originalAccuracyInfoStr, res.IsCorrect);
        }

        /// <summary>
        /// 合并值+类型完全相同的精度带：同值同类型时取范围并集，覆盖全量程则设为 IsFullBand
        /// </summary>
        private static void MergeCompatibleBands(List<AccuracyBand> bands)
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < bands.Count; i++)
                {
                    for (int j = i + 1; j < bands.Count; j++)
                    {
                        var a = bands[i];
                        var b = bands[j];
                        // 值相等 且 类型相等 且 都非 WithUnit
                        if (Math.Abs(a.AccuracyPercent - b.AccuracyPercent) >= 1e-9) continue;
                        if (a.Type != b.Type) continue;
                        if (a.Type == AccuracyBand.BandType.WithUnit) continue;

                        // 合并范围
                        double aBegin = a.IsFullBand ? 0 : a.BandBegin;
                        double aEnd = a.IsFullBand ? 100 : a.BandEnd;
                        double bBegin = b.IsFullBand ? 0 : b.BandBegin;
                        double bEnd = b.IsFullBand ? 100 : b.BandEnd;
                        double newBegin = Math.Min(aBegin, bBegin);
                        double newEnd = Math.Max(aEnd, bEnd);

                        bands[i] = new AccuracyBand
                        {
                            Type = a.Type,
                            AccuracyPercent = a.AccuracyPercent,
                            IsFullBand = newBegin <= 0 && newEnd >= 100,
                            BandBegin = newBegin,
                            BandEnd = newEnd,
                            IsValid = true,
                        };
                        bands.RemoveAt(j);
                        merged = true;
                        break;
                    }
                    if (merged) break;
                }
            } while (merged);
        }
        #endregion

        #region 压力相关
        /// <summary>
        /// 获取量程范围
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public bool GetPressureRange(out PressureRange range)
        {
            range = new PressureRange();
            iResponse<PressureRange> result = DPS.GetPressureRange();
            if (result.IsCorrect)
            {
                range = result.Result;
            }
            return result.IsCorrect;
        }
        public ScriptHelperKVP GetPressureRange_KVP(out PressureRange range)
        {
            iResponse<PressureRange> result = DPS.GetPressureRange();
            range = result.IsCorrect ? result.Result : new PressureRange();
            return new ScriptHelperKVP("获取量程范围:" + range.ToString(), result.IsCorrect);
        }
        /// <summary>
        /// 获取压力读值（kPa）
        /// </summary>
        /// <param name="pre"></param>
        /// <returns></returns>
        public bool GetPressureValueForkPa(out double pre)
        {
            Pressure pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            if (GetPressure(out pressure))
            {
                pre = pressure.ToUnit(PressureUnit.kPa).Value;
                return true;
            }
            else
            {
                pre = 0;
                return false;
            }
        }
        /// <summary>
        /// 获取压力读值（kPa）
        /// </summary>
        /// <param name="pre"></param>
        /// <returns></returns>
        public bool GetPressure(out double pre, string mesg = "")
        {
            pre = -1d;
            try
            {
                iResponse<Pressure> result = DPS.GetPressure();
                if (result.IsCorrect)
                {
                    pre = result.Result.ToUnit(PressureUnit.kPa).Value;
                    return true;
                }
                else
                {
                    pre = 0;
                    mesg = result.GetContent(true, true);
                    return false;
                }
            }
            catch
            {
                Close();
                Open();
                return false;
            }

        }
        /// <summary>
        /// 获取压力读值
        /// </summary>
        /// <param name="pressure"></param>
        /// <returns></returns>
        public bool GetPressure(out Pressure pressure)
        {
            pressure = new Pressure() { Value = 0, Unit = PressureUnit.kPa };
            iResponse<Pressure> result = DPS.GetPressure();
            if (result.IsCorrect)
            {
                pressure = result.Result;
            }
            return result.IsCorrect;
        }
        #endregion

        #region 温度相关
        public bool GetTemperatureValue(out double tem)
        {
            tem = double.NaN;
            iResponse<Xmas11.Domain.Thermology.Temperature> result = DPS.GetTemperature();
            if (result.IsCorrect)
            {
                tem = result.Result.Value;
            }
            return result.IsCorrect;
        }
        public ScriptHelperKVP GetTemperatureValue_KVP(out double tem)
        {
            iResponse<Xmas11.Domain.Thermology.Temperature> result = DPS.GetTemperature();
            tem = result.IsCorrect ? result.Result.Value : double.NaN;
            return new ScriptHelperKVP("读取温度测量值:" + tem, result.IsCorrect);
        }
        #endregion

        #region 放大倍数
        /// <summary>
        /// 设置放大倍数输入电流
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public bool SetADInputCurrent(double current)
        {
            iResponse result = DPS.AD_SetADInputCurrent(current);
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置放大倍数
        /// </summary>
        /// <param name="times"></param>
        /// <returns></returns>
        public bool SetADTimes(int times)
        {
            return SetADTimes((ADTimes)times);
        }
        /// <summary>
        ///  设置放大倍数
        /// </summary>
        /// <param name="times"></param>
        /// <returns></returns>
        public bool SetADTimes(ADTimes times)
        {
            iResponse result = DPS.AD_SetAD(times);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取AD激励值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool GetADOriginalValue(out double value)
        {
            value = double.NaN;
            iResponse<double> result = DPS.GetADOriginalFlagValue();
            if (result.IsCorrect)
            {
                value = result.Result;
            }
            return result.IsCorrect;
        }

        #endregion

        #region 量程设置
        /// <summary>
        /// 开始量程设置
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRangeStart()
        {
            iResponse result = DPS.RS_Start();
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置量程下限
        /// </summary>
        /// <param name="rangeLower"></param>
        /// <returns></returns>
        public bool SetPressureRangeLower(double rangeLower)
        {
            iResponse result = DPS.RS_SetPressureRangeLower(rangeLower);
            return result.IsCorrect;
        }
        /// <summary>
        /// 设置量程上限
        /// </summary>
        /// <param name="rangeUpper"></param>
        /// <returns></returns>
        public bool SetPressureRangeUpper(double rangeUpper)
        {
            iResponse result = DPS.RS_SetPressureRangeUpper(rangeUpper);
            return result.IsCorrect;
        }
        /// <summary>
        /// 标定量程
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetPressureRangeSettingData(RangeSettingData data)
        {
            iResponse result = DPS.RS_SetRangeSettingData(data);
            return result.IsCorrect;
        }
        /// <summary>
        /// 标定量程(0~1)kPa
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRangeSettingData()
        {
            RangeSettingData data = new RangeSettingData(new PressureRange(0, 1, Xmas11.Domain.Unit.Parse("kPa")), new Xmas11.Domain.Electricity.VoltageRange(0, 1, Xmas11.Domain.Unit.Parse("V")));
            return SetPressureRangeSettingData(data);
        }
        /// <summary>
        /// 标定量程(0~1)kPa
        /// </summary>
        /// <returns></returns>
        public bool SetPressureRangeSettingData(double low, Double Up)
        {
            RangeSettingData data = new RangeSettingData(new PressureRange(low, Up, Xmas11.Domain.Unit.Parse("kPa")), new Xmas11.Domain.Electricity.VoltageRange(0, 1, Xmas11.Domain.Unit.Parse("V")));

            return SetPressureRangeSettingData(data);
        }
        /// <summary>
        /// 结束量程设置
        /// </summary>
        /// <param name="isSave"></param>
        /// <returns></returns>
        public bool SetPressureRangeStop(bool isSave = true)
        {
            iResponse result = DPS.RS_Stop(isSave);
            return result.IsCorrect;
        }
        #endregion

        /// <summary>
        /// 读取传感器激励值
        /// </summary>
        /// <param name="pv"></param>
        /// <returns></returns>
        public bool GetSensorPowerSupplyValue(out double pv)
        {
            pv = double.NaN;
            iResponse<double> result = DPS.GetSensorPowerSupplyValue();
            if (result.IsCorrect)
            {
                pv = result.Result;
            }
            return result.IsCorrect;
        }
        public ScriptHelperKVP GetSensorPowerSupplyValue_KVP(out double pv)
        {
            iResponse<double> result = DPS.GetSensorPowerSupplyValue();
            pv = result.IsCorrect ? result.Result : double.NaN;
            return new ScriptHelperKVP("读取传感器激励值:" + pv, result.IsCorrect);
        }
        public ScriptHelperKVP GetSerialNumber_OTYPE(out string sn)
        {
            var res = DPS.GetDevType();
            sn = res.IsCorrect ? res.Result : string.Empty;
            return new ScriptHelperKVP("读取设备类型信息:" + sn, res.IsCorrect);
        }
        public ScriptHelperKVP GetDevType(out string type)
        {
            var res = DPS.GetDevType();
            type = res.IsCorrect ? res.Result : string.Empty;
            return new ScriptHelperKVP("读取设备类型:" + type, res.IsCorrect);
        }
        public ScriptHelperKVP GetTag(int length, out string tag)
        {
            var res = DPS.GetTag(length);
            tag = res.IsCorrect ? res.Result : string.Empty;
            return new ScriptHelperKVP("获取标签:" + tag, res.IsCorrect);
        }
        public ScriptHelperKVP GetManyRangeCount(out int count)
        {
            var res = DPS.GetManyRangeCount();
            count = res.IsCorrect ? res.Result : -1;
            return new ScriptHelperKVP("获取多量程数量:" + count, res.IsCorrect);
        }
        public ScriptHelperKVP GetManyRangeInfoByID(int id, out PressureRangeDetailedInfo detailInfo)
        {
            var res = DPS.GetManyRangeInfoByID(id);
            detailInfo = res.IsCorrect ? res.Result : new PressureRangeDetailedInfo();
            return new ScriptHelperKVP("读取第" + id + "个量程信息:" + PRDItoString(detailInfo), res.IsCorrect);
        }
        private string PRDItoString(PressureRangeDetailedInfo detailInfo)
        {
            return "类型:" + detailInfo.Type.ToString() + "范围:" + detailInfo.Range.ToString() + ",精度:" + detailInfo.Accuracy.Value;
        }
        /// <summary>
        /// 获取过压标志
        /// </summary>
        /// <returns></returns>
        public bool GetOverPressure(out string OverPressure)
        {
            OverPressure = string.Empty;
            iResponse<string> result = DPS.GetOverPressure();
            if (!result.IsCorrect)
            {
                return false;
            }
            OverPressure = result.Result;
            return true;
        }

        /// <summary>
        /// 设置BMP581的默认量程
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetBMP581Range()
        {
            iResponse result = DPS.SetBMP581Range1();
            if (result.IsCorrect)
            {
                Thread.Sleep(1000);
                result = DPS.SetBMP581Range2(0);
                if (result.IsCorrect)
                {
                    result = DPS.SetBMP581Range3(120);
                    Thread.Sleep(1000);
                    if (result.IsCorrect)
                    {
                        result = DPS.SetBMP581Range4();
                        Thread.Sleep(3000);
                    }
                }
            }

            return result.IsCorrect;
        }


        /// <summary>
        /// 设置BMP581
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetBMP581(bool value)
        {
            iResponse result = DPS.SetPressureBMP581(value);
            return result.IsCorrect;
        }

        /// <summary>
        /// 获取BMP581
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool GetBMP581(out bool Isbmp581)
        {
            Isbmp581 = false;
            iResponse<bool> result = DPS.GetPressureBMP581();
            if (result.IsCorrect)
            {
                Isbmp581 = result.Result;
                return result.IsCorrect;
            }
            return result.IsCorrect;
        }


        /// <summary>
        /// 主机启动模块进入/退出自诊断模式
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetPatternOpen(out SelfDiagnosisData outvalue)
        {
            outvalue = new SelfDiagnosisData();
            iResponse<string> result = DPS.SetPatternGetResult(true);
            if (!result.IsCorrect)
            {
                return false;
            }
            List<SelfDiagnosisItem> itemList = new List<SelfDiagnosisItem>();
            if (!string.IsNullOrEmpty(result.Result))
            {

                Regex rgx = new Regex("T.+(\\0)");
                var valuetemp = rgx.Match(result.Result).Value.Trim('T').Trim('\0');
                string[] returnValue = valuetemp.Split(',');
                if (returnValue.Length == 14)
                {
                    for (int i = 0; i < returnValue.Length; i++)
                    {
                        string[] strs = returnValue[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        SelfDiagnosisItem item = new SelfDiagnosisItem();
                        if (strs.Length == 3)
                        {
                            item.Sort = Convert.ToInt32(strs[0]);
                            item.FaultNo = Convert.ToInt32(strs[1]);
                            item.MeasureValue = strs[2];

                            #region

                            switch (strs[0])
                            {
                                case "0":
                                    item.ItemTypeName = "电源电压";
                                    item.MeasureValueUnit = "V";
                                    item.Name = "AD7124内基准测电源电压、恒流+比对电阻测电源电压";
                                    if (strs[1] == "3")
                                    {
                                        item.FaultName = "内基准、恒流电阻正常、AVDD故障";
                                    }
                                    else if (strs[1] == "2")
                                    {
                                        item.FaultName = "恒流电阻正常、AVDD正常、内基准故障";
                                    }
                                    else if (strs[1] == "1")
                                    {
                                        item.FaultName = "内基准、AVDD正常，恒流电阻故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "内基准、恒流电阻、AVDD正常";
                                    }
                                    break;
                                case "1":
                                    item.ItemTypeName = "STM32单片机";
                                    item.Name = "VCC电源电压";
                                    item.MeasureValueUnit = "V";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "电源电压故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "电源电压正常";
                                    }
                                    break;
                                case "2":
                                    item.ItemTypeName = "STM32单片机";
                                    item.Name = "MCU温度";
                                    item.MeasureValueUnit = "℃";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "MCU故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "MCU正常";
                                    }
                                    break;
                                case "3":
                                    item.ItemTypeName = "AD7124";
                                    item.Name = "CRC检查";
                                    item.MeasureValueUnit = "/";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "7124 CRC校验故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "7124 CRC校验正常";
                                    }
                                    break;
                                case "4":
                                    item.ItemTypeName = "AD7124";
                                    item.Name = "ALDO检查";
                                    item.MeasureValueUnit = "/";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "AD7124 ALDO故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "AD7124 ALDO正常";
                                    }
                                    break;
                                case "5":
                                    item.ItemTypeName = "AD7124";
                                    item.Name = "DLDO检查";
                                    item.MeasureValueUnit = "/";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "AD7124DLDO故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "AD7124DLDO正常";
                                    }
                                    break;
                                case "6":
                                    item.ItemTypeName = "AD7124";
                                    item.Name = "AD7124温度";
                                    item.MeasureValueUnit = "℃";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "AD7124故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "AD7124正常";
                                    }
                                    break;
                                case "7":
                                    item.ItemTypeName = "AD7124";
                                    item.Name = "内部参考电压REF";
                                    item.MeasureValueUnit = "V";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "AD7124内基准故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "AD7124内基准正常";
                                    }
                                    break;
                                case "8":
                                    item.ItemTypeName = "TMP117温度传感器";
                                    item.Name = "TMP117温度";
                                    item.MeasureValueUnit = "℃";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "ATMP117故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "TMP117正常";
                                    }
                                    break;
                                case "9":
                                    item.ItemTypeName = "压力传感器";
                                    item.Name = "传感器输入阻抗";
                                    item.MeasureValueUnit = "Ω";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "压力传感器开路故障";
                                    }
                                    else if (strs[1] == "2")
                                    {
                                        item.FaultName = "压力传感器短路故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "压力传感器正常";
                                    }
                                    break;
                                case "10":
                                    item.ItemTypeName = "压力传感器";
                                    item.Name = "恒流0.35mA激励（对恒流传感器有效）";
                                    item.MeasureValueUnit = "mA";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "恒流电阻故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "恒流电阻正常";
                                    }
                                    break;
                                case "11":
                                    item.ItemTypeName = "压力传感器";
                                    item.Name = "恒压2.5V激励（对恒压传感器有效）";
                                    item.MeasureValueUnit = "V";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "恒压激励（模拟开关ADG819）故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "恒压激励（模拟开关ADG819）正常";
                                    }
                                    break;
                                case "12":
                                    item.ItemTypeName = "存储器测试";
                                    item.Name = "MCU内置EEPROM测试";
                                    item.MeasureValueUnit = "/";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "MCU内置EEPROM存储故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "MCU内置EEPROM存储正常";
                                    }
                                    break;
                                case "13":
                                    item.ItemTypeName = "存储器测试";
                                    item.Name = "外扩EEPROM测试";
                                    item.MeasureValueUnit = "/";
                                    if (strs[1] == "1")
                                    {
                                        item.FaultName = "外扩EEPROM存储故障";
                                    }
                                    else if (strs[1] == "0")
                                    {
                                        item.FaultName = "外扩EEPROM存储正常";
                                    }
                                    break;
                                default:
                                    break;
                            }

                            #endregion

                            itemList.Add(item);
                        }
                        else
                        {
                            break;
                        }
                    }

                }
            }
            outvalue.ItemData = itemList;
            return true;
        }

        /// <summary>
        /// 主机启动模块进入/退出自诊断模式
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetPatternClose()
        {
            iResponse result = DPS.SetPattern(false);
            if (!result.IsCorrect)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取自检结果
        /// </summary>
        /// <returns></returns>
        public bool SelfDiagnosis(out SelfDiagnosisData SelfResult)
        {
            SelfResult = new SelfDiagnosisData();
            iResponse<SelfDiagnosisData> result = DPS.GetSelfDiagnosis();
            if (!result.IsCorrect)
            {
                return false;
            }
            SelfResult = result.Result;
            return true;
        }
        #region ID、型号信息
        /// <summary>
        /// 获取ID、型号信息
        /// </summary>
        /// <returns></returns>
        public bool GetOsInfo(out string info)
        {
            info = string.Empty;
            iResponse<string> result = DPS.GetOsInfo();
            if (!result.IsCorrect)
            {
                return false;
            }
            info = result.Result;
            return true;
        }

        /// <summary>
        /// 设置ID、型号信息
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool SetOsInfo(string info)
        {
            iResponse result = DPS.SetOsInfo(info);
            return result.IsCorrect;
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
                string path = UpgradeFile.LocalCacheRoot + @"/DPSEX/OS/UpgradeSetting.xml";
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
            else if (this.CommConfig is EthernetConfig)
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
                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModule, Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg31));
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
            this.DeveiceSN = code;
            if (UpgradeInfo.MainInfoIsContains(codeInfo))
            {
                UpgradeInfo.MainInfoDic["Code"].Info = codeInfo.Info;
            }
            else
            {
                UpgradeInfo.AddMainInfo(codeInfo);
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
                        var gpg = mainUpgradeFile.Versions.Where(v => v.Key.Contains("DPS-EX")).Select(v => v.Value).FirstOrDefault();
                        if (!string.IsNullOrEmpty(gpg))
                        {
                            UpgradeInfo.VersionInfoDic["MainFirmware"].UpgradeVersion = gpg;
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
                if (mainUpgradeFile != null && File.Exists(mainUpgradeFile.CachePath))
                {
                    DateTime startDT = DateTime.Now;
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.BeginUpgrade, startDT.ToString()));
                    #region 1.发送升级指令，进入Bootloader模式

                    var bootloaderEnter = new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.BootloaderEnter);
                    this.UpgradeInfo.AddUpgradeMsgs(bootloaderEnter);
                    if (EnterBootloder())
                    {
                        //切换波特率为9600
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleChangeBaudRate, "9600"));
                        if (!ChangeBaudRate(9600))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("9600", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                            return UpgradeInfo;
                        }
                    }
                    else
                    {
                        //切换波特率为9600
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleChangeBaudRate, "9600"));
                        if (!ChangeBaudRate(9600))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("9600", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                            return UpgradeInfo;
                        }
                        bool enterFlag = false;
                        System.Threading.Tasks.Task.Run(() =>
                        {

                            for (int j = 0; j < 30; j++)
                            {
                                if (enterFlag)
                                {
                                    break;
                                }
                                bootloaderEnter.Content = Bots.TestBench.Device.Base.Properties.Resources.BootloaderEnter + "(" + j + ")";
                                System.Threading.Thread.Sleep(1000);
                            }

                        });
                        if (Handshake(30000))
                        {
                            enterFlag = true;
                            bootloaderEnter.Content = Bots.TestBench.Device.Base.Properties.Resources.BootloaderEnter;
                        }
                        else
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.BootloaderEnterFailed));
                            return UpgradeInfo;
                        }
                    }



                    #endregion

                    #region 2.获取Bootloader版本
                    DateTime beginTime = DateTime.Now;
                    System.Threading.Thread.Sleep(2000);
                    //读取Bootloader版本
                    string loaderVersion = string.Empty;
                    while ((DateTime.Now - beginTime).TotalSeconds < 15)
                    {
                        if (GetLoaderVersion(out loaderVersion))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader + " " + Bots.TestBench.Device.Base.Properties.Resources.Version, loaderVersion));
                            break;
                        }
                        System.Threading.Thread.Sleep(2000);
                    }
                    if ((DateTime.Now - beginTime).TotalSeconds > 15)
                    {
                        RunApplication();
                        //波特率切回4800
                        if (!ChangeBaudRate(4800))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("4800", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                            return UpgradeInfo;
                        }
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader + " " + Bots.TestBench.Device.Base.Properties.Resources.Version, Bots.TestBench.Device.Base.Properties.Resources.GetFailed));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    #endregion

                    #region 3.Bootloader获取下载参数
                    this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.UpgradeGetDownloadParameters));
                    CSTLoaderV2InternalFlash mainInternalFlash = new CSTLoaderV2InternalFlash();
                    if (GetInternalFlashParameters(out mainInternalFlash))
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, string.Format(Bots.TestBench.Device.Base.Properties.Resources.UpgradeDownloadParameters1, mainInternalFlash.PackageMaxLength, mainInternalFlash.PackageMaxTimeout, mainInternalFlash.ErasureFlashTimeout)));
                    }
                    else
                    {
                        RunApplication();
                        //波特率切回4800
                        if (!ChangeBaudRate(4800))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("4800", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                            return UpgradeInfo;
                        }
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.UpgradeGetDownloadParametersFail1));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    #endregion

                    #region 4.读取升级文件
                    //读取文件
                    byte[] mainIntUpgradeFile_VectorTableData = null;
                    ushort mainIntUpgradeFile_VectorTableCRC = 0;
                    byte[] mainIntUpgradeFile_CodeData = null;
                    ushort mainIntUpgradeFile_CodeCRC = 0;
                    if (!ReadUpgradeFile(mainUpgradeFile, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, out mainIntUpgradeFile_VectorTableData, out mainIntUpgradeFile_VectorTableCRC, out mainIntUpgradeFile_CodeData, out mainIntUpgradeFile_CodeCRC))
                    {
                        RunApplication();
                        //波特率切回4800
                        if (!ChangeBaudRate(4800))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("4800", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                            return UpgradeInfo;
                        }
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.UpgradePackage, Bots.TestBench.Device.Base.Properties.Resources.UpgradeFileReadFailed));
                        this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                        return UpgradeInfo;
                    }
                    #endregion

                    #region 5.下载升级文件
                    while (true)
                    {
                        //擦除Flash
                        if (ErasureInternalFlash(mainInternalFlash.ErasureFlashTimeout))
                        {
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.EraseInternalFlashOk));
                        }
                        else
                        {
                            RunApplication();
                            //完成升级，波特率切回4800
                            if (!ChangeBaudRate(4800))
                            {
                                this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("4800", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                                return UpgradeInfo;
                            }
                            this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg(Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.EraseInternalFlashFailed));
                            this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                            return UpgradeInfo;
                        }

                        //下载内部Flash中断向量表
                        //下载内部Flash代码段
                        if (DownloadVectorTableData(mainIntUpgradeFile_VectorTableData, mainIntUpgradeFile_VectorTableCRC, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash1) &&
                            DownloadCodeData(mainIntUpgradeFile_CodeData, mainIntUpgradeFile_CodeCRC, mainInternalFlash, Bots.TestBench.Device.Base.Properties.Resources.Bootloader, Bots.TestBench.Device.Base.Properties.Resources.DownloadInternalFlash2))
                        {
                            // 下载完成确认
                            DownloadConfirm();
                            break;
                        }
                    }
                    #endregion

                    #region 6、运行程序
                    RunApplication();
                    //完成升级，波特率切回4800
                    if (!ChangeBaudRate(4800))
                    {
                        this.UpgradeInfo.AddUpgradeMsgs(new UpgradeMsg("4800", Bots.TestBench.Device.Base.Properties.Resources.UpgradeModuleConnectFail_Msg32));
                        return UpgradeInfo;
                    }
                    #endregion

                    #region 7、断开连接,重新连接设备
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
                    this.UpgradeInfo.UpgradeResult = UpgradeResult.Failed;
                    return UpgradeInfo;
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
                if (!RequestStopUpgrade)
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
                                            string text = System.IO.File.ReadAllText(upgradeFile.CachePath);
                                            string keyControllerVersion = "DPS-EX";
                                            Regex regex = new Regex(@"DPS-EX V\d+.\d+.\d+.\d+");
                                            var ver = regex.Match(text);
                                            if (ver.Success)
                                            {
                                                keyControllerVersion = ver.Value;
                                            }
                                            else
                                            {
                                                keyControllerVersion = "";
                                            }

                                            upgradeFile.AddVersion("DPS-EX", keyControllerVersion);
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
        /// 进入Bootloader模式
        /// </summary>
        /// <returns></returns>
        public bool EnterBootloder()
        {
            byte[] bytes = System.Text.Encoding.Default.GetBytes("255:W:ISP\r\n");

            DPS.CommInstance.ClearAllBuffer();
            DPS.CommInstance.Write(bytes);

            DateTime start = DateTime.Now;

            while (true)
            {
                System.Threading.Thread.Sleep(100);

                int bytesToRead = DPS.CommInstance.Available;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    DPS.CommInstance.Read(out buffer, bytesToRead);

                    string[] tmp = System.Text.Encoding.Default.GetString(buffer).Replace("\0", "").Replace("\r", "").Replace("\n", "").Split(':');
                    ;
                    if (tmp[1] == "F")
                    {
                        return true;
                    }
                }

                TimeSpan span = DateTime.Now - start;
                if (span.TotalMilliseconds >= 3000)
                {
                    break;
                }
            }

            return false;
        }
        /// <summary>
        /// 切换指定波特率
        /// </summary>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public bool ChangeBaudRate(int baudrate)
        {

            if (this.CommConfig is SerialPortConfig)
            {
                this.Close();
                var commConfig = this.CommConfig as SerialPortConfig;
                commConfig.Bauds = baudrate;
                this.CommInstance = new Xmas11.Comm.Devices.DPSEX(this.CommConfig.GetCommSettings());
                if (!this.CommInstance.Connected && this.CommInstance.Open())
                {
                    return true;
                }
            }
            else if (this.CommConfig is EthernetConfig)
            {
                var commConfig = this.CommConfig as EthernetConfig;
                if (commConfig.Port > 10000)
                {
                    //切串口服务器波特率
                    try
                    {
                        this.CommInstance.Close();
                        CommLab.Tool.SerialConfig build = new CommLab.Tool.SerialConfig();
                        string post = build.ConfigComplete(Convert.ToInt32(commConfig.Port.ToString().Substring(commConfig.Port.ToString().Length - 1, 1)), baudrate, 8, System.IO.Ports.StopBits.Two, System.IO.Ports.Parity.None);
                        if (!build.Execute(System.Net.IPAddress.Parse(commConfig.IP), post))
                        {
                        }
                        System.Threading.Thread.Sleep(100);
                        if (!build.Save(System.Net.IPAddress.Parse(commConfig.IP)))
                        {
                        }
                        System.Threading.Thread.Sleep(100);
                        if (!build.RestartSingleSerial(IPAddress.Parse(commConfig.IP), 8, Convert.ToInt32(commConfig.Port.ToString().Substring(commConfig.Port.ToString().Length - 1, 1))))
                        {
                        }
                        System.Threading.Thread.Sleep(100);
                        if (!this.CommInstance.Connected)
                        {
                            this.CommInstance.Open();
                        }

                    }
                    catch
                    {
                    }
                }
                else
                {
                    //切串口转网口波特率
                    try
                    {
                        string jcmd = "{\"JCMD\":1,\"PL\":	{\"UART\":{\"Baudrate\":" + baudrate + "}},\"CID\":10005}";
                        byte[] data = Encoding.UTF8.GetBytes(jcmd);
                        var SynCl = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                        SynCl.SendTimeout = 1000;
                        SynCl.ReceiveTimeout = 1000;
                        SynCl.SendBufferSize = 4096;
                        SynCl.ReceiveBufferSize = 8192;
                        SynCl.Connect(new IPEndPoint(IPAddress.Parse(commConfig.IP), 48899));
                        System.Threading.Thread.Sleep(100);
                        SynCl.Send(data);
                        System.Threading.Thread.Sleep(100);
                        SynCl.Close();
                        SynCl = null;
                    }
                    catch
                    {

                    }

                }

            }

            return true;
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
                    if (DPS != null && DPS.CommInstance != null && DPS.CommInstance.IsOpen)
                    {
                        int count = 0;
                        DPS.CommInstance.ClearAllBuffer();
                        CSTLoaderV2Request cmd = new CSTLoaderV2Request(0xC0, 0x01, 0x00);
                        byte[] bytes = cmd.ToBytes().ToArray();
                        DPS.CommInstance.Write(bytes);
                        count++;
                        while ((DateTime.Now - begin).TotalMilliseconds < maxWaitTime)
                        {
                            int bytesToRead = DPS.CommInstance.Available;
                            if (bytesToRead > 0)
                            {
                                byte[] buffer = new byte[bytesToRead];
                                DPS.CommInstance.Read(out buffer);
                                return true;
                            }
                            if (count % 5 == 0)
                            {
                                DPS.CommInstance.ClearAllBuffer();
                                DPS.CommInstance.Write(bytes);
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
            lock (DPS.ExecuteLock)
            {
                DPS.CommInstance.ClearAllBuffer();
                DPS.CommInstance.Write(data);
                DateTime start = DateTime.Now;
                List<byte> tmpBytes = new List<byte>();
                while (true)
                {
                    if (DPS.CommInstance.Available > 0)
                    {
                        byte[] buffer = null;
                        int readCount = DPS.CommInstance.Read(out buffer);
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

        #endregion
    }
}
