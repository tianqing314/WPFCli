using Bots.Service.ServiceHelper;
using Bots.TestBench.Device;
using Bots.TestBench.Device.Base;
using Bots.TestBench.Model.Scripts;
using Bots.TestBench.Model.Task;
using Bots.TestBench.UI.Common;
using Bots.TestBench.UI.P27.Task.P27.Business;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Xml.Linq;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices.ConST171.Data;
/// <summary>
/// 智能温湿度检测仪整机自动化测试
/// </summary>
class P27_HostSctript
{

    #region 整机测试
    ToolBusiness toolBusiness = new ToolBusiness();
    /// <summary>
    /// 基础信息写入
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic SelfTestWriteSNAndType(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.TestWriteSNAndType(item, result, rData);
        return result;
    }
    /// <summary>
    /// 版本验证
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic TestVersions(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.TestVersions(item, result, rData);
        return result;
    }

    /// <summary>
    /// 供电测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic TestBatteryTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData($"功能测试结果") };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.TestBatteryTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 传感器测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic SensorTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData($"功能测试结果") };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.SensorTest(item, result, rData);
        return result;
    }
    /// <summary>
    /// 风扇测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic FanTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.FanTest(item, result, rData);
        return result;
    }
    /// <summary>
    /// 正压气源静音模式开启
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic SetDeviceParameter(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.SetDeviceParameter(item, result, rData);
        return result;
    }
    /// <summary>
    /// 屏幕颜色测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic ScreenGeneralTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.ScreenGeneralTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 屏幕触摸测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic ScreenTouchTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.ScreenGeneralTest(item, result, rData);
        return result;
    }


    /// <summary>
    /// 屏幕亮度测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic ScreenLightTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.ScreenGeneralTest(item, result, rData);
        return result;
    }
    /// <summary>
    /// 屏幕亮度测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic ScreenSoundTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.ScreenGeneralTest(item, result, rData);
        return result;
    }
    /// <summary>
    /// 正压测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic PositiveTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.PositiveTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 负压测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic VacuumTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.VacuumTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 吹扫测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic BlowTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.BlowTest(item, result, rData);
        return result;
    }
    /// <summary>
    /// 正压模块校准
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic PositiveCalibrationTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.PositiveCalibrationTest(item, result, rData, "DPSEX1", item.Parameters[0] as ValueParameter, item.Parameters[1] as ValueParameter, item.Conditions[0] as ValueCondition);
        return result;
    }
    /// <summary>
    /// 真空模块校准
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic VacuumCalibrationTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.VacuumCalibrationTest(item, result, rData, "DPSEX2", item.Parameters[0] as ValueParameter, item.Conditions[0] as ValueCondition);
        return result;
    }
    #endregion

    #region 组件测试
    /// <summary>
    /// 造压测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic PressureEfficiencyTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.PressureEfficiencyTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 造压测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic TemperatureSensorTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>()
        {
            new TextData("设备温度"),
            new TextData("环境温度")
        };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.TemperatureSensorTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 风扇转速测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic SpeedTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.SpeedTest(item, result, rData);
        return result;
    }

    /// <summary>
    /// 正压测试
    /// </summary>
    /// <param name="item">测试项目类</param>
    /// <returns>测试脚本返回值</returns>
    public dynamic VacuumPositiveTest(AutoTestItem item)
    {
        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData>() { new TextData() };
        result.Data = rData;
        item.ClearRealTimeMsgs();
        toolBusiness.VacuumPositiveTest(item, result, rData);
        return result;
    }
    #endregion

    #region 出厂检验
    /// <summary>
    /// 发货单信息
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic SaveFH(dynamic item)
    {
        try
        {
            Result<List<TextData>> result = new Result<List<TextData>>(true);
            List<TextData> rData = new List<TextData> { new TextData("接口返回") };
            result.Data = rData;
            TextParameter parameter1 = item.Parameters[0] as TextParameter;
            item.ClearRealTimeMsgs();

            DUT dut = item.Root.DUT;

            //item.AddRealTimeMsgs(new RealTimeMsg("型号", $"   -->   {dut.OrderMode}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("量程", $"   {dut.DeviceRange}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("销售规格", $"   -->   {dut.OrderSaleMode}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("设备类型", $"   -->   {dut.DeviceSalesType}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("订单备注", $"   -->   {dut.OrderRemark}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("销售渠道", $"   -->   {dut.DeviceCategory1}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("送检信息", $"   -->   {dut.DeviceCategory2}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("订单类别", $"   -->   {dut.DeviceCategory3}"));

            //item.AddRealTimeMsgs(new RealTimeMsg("精度等级", $"   -->   {dut["RangeStr"]}"));
            //item.AddRealTimeMsgs(new RealTimeMsg("特殊标识", $"   -->   {dut["IsSpecialty"]}"));
            //item.AddRealTimeMsgs(new RealTimeMsg());

            string info = dut.GetInfo();
            XElement xmlInfo = XElement.Parse(info);
            item.AddRealTimeMsgs(new RealTimeMsg(">>>发货单信息"));
            foreach (var attr in xmlInfo.Attributes())
            {
                item.AddRealTimeMsgs(new RealTimeMsg(dut.GetChineseFieldName(attr.Name.ToString()), $"   -->   {attr.Value}"));
            }
            if (!string.IsNullOrEmpty(dut.SpecialUrl))
            {
                bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"订单中包含特制品链接：{dut.SpecialUrl}\r\n，打开链接查看详细信息；");
            }



            #region 单独解析
            if (!string.IsNullOrWhiteSpace(dut.OrderRemark))
            {
                bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"【 {dut.OrderRemark} 】\r\n请确认以上订单备注信息！！！");
                result.SetConclusion(dut.OrderRemark);
            }
            #endregion
            return result;
        }
        finally { }
    }
    /// <summary>
    /// 型号信息检测
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic TestTypeInout(AutoTestItem item)
    {

        Result<List<TextData>> result = new Result<List<TextData>>(true);
        List<TextData> rData = new List<TextData> { new TextData("标准整机型号"), new TextData("实际整机型号"), new TextData("系统镜像版本"), new TextData("硬盘序列号"), new TextData("硬盘类型") };
        result.Data = rData;
        try
        {
            item.ClearRealTimeMsgs();
            #region 正式检验只核验不设置

            string language, salechannel;


            RealTimeMsg msg4 = new RealTimeMsg("销售渠道");
            item.AddRealTimeMsgs(msg4);
            salechannel = item.Root.DUT.DeviceCategory1;
            if (string.IsNullOrEmpty(salechannel) || salechannel == "Unknow")
            {
                bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"发货单销售渠道获取失败，请人工判断系统语言是否一致\r\n点击确定，继续进行测试；\r\n否则测试项不通过。");
                if (key != true)
                {
                    result.AddTestErrMsgs(new ErrMsg(20001, "当前系统语言信息不符合要求"));
                    return result;
                }

            }
            else
            {
                msg4.Content = salechannel;
                RealTimeMsg msg3 = new RealTimeMsg("设置系统语言");
                item.AddRealTimeMsgs(msg3);


                if (salechannel == "Const")
                {

                    if (!item.GetDevice<P27CommonBase>("ConST171A").SetSystemLanguage(LanguageSet.zh_CN))
                    {
                        return result.AddTestErrMsgs(new ErrMsg(20001, "读取中文语言失败"));
                    }
                    msg3.Content = "zh-CN";

                    msg3 = new RealTimeMsg("设置开机LOGO");
                    item.AddRealTimeMsgs(msg3);
                    if (!item.GetDevice<P27CommonBase>("ConST171A").SetLogoInfo(LogoSet.ConST))
                    {
                        return result.AddTestErrMsgs(new ErrMsg(20001, "设计ConST开机LOGO失败"));
                    }
                    msg3.Content = "ConST";
                }
                else
                {
                    if (!item.GetDevice<P27CommonBase>("ConST171A").SetSystemLanguage(LanguageSet.en_US))
                    {
                        return result.AddTestErrMsgs(new ErrMsg(20001, "读取英文语言失败"));
                    }
                    msg3.Content = "en-US";
                    msg3 = new RealTimeMsg("设置开机LOGO");
                    item.AddRealTimeMsgs(msg3);
                    if (!item.GetDevice<P27CommonBase>("ConST171A").SetLogoInfo(LogoSet.Additel))
                    {
                        return result.AddTestErrMsgs(new ErrMsg(20001, "设计Additel开机LOGO失败"));
                    }
                    msg3.Content = "Additel";
                }

                string deviceType = salechannel == "Const" ? "ConST171A" : "ADT705";
                RealTimeMsg message = new RealTimeMsg("设备整机型号");
                item.AddRealTimeMsgs(message);
                string DevTypeSTD;
                if (!item.GetDevice<P27CommonBase>("ConST171A").SetDeviceType(deviceType))
                {
                    return result.AddTestErrMsgs(new ErrMsg(20001, "读取设备类型失败"));
                }
                message.Content = deviceType;
                rData[1].Value = deviceType;
                item.Root.DUT.DeviceMode = deviceType;

                RealTimeMsg msg1 = new RealTimeMsg("获取发货单整机型号");
                item.AddRealTimeMsgs(msg1);
                DevTypeSTD = item.Root.DUT.DeviceSalesType;
                if (string.IsNullOrEmpty(DevTypeSTD))
                {
                    bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"发货单整机型号获取失败，请人工判断型号是否一致\r\n点击确定，继续进行测试；\r\n否则测试项不通过。");
                    if (key != true)
                    {
                        result.AddTestErrMsgs(new ErrMsg(20001, "当前设备型号信息不符合要求"));
                        return result;
                    }
                    else
                    {
                        msg1.Content = "人工判定通过";
                    }
                }
                else
                {
                    msg1.Content = DevTypeSTD;
                    rData[0].Value = DevTypeSTD;

                    if (deviceType != DevTypeSTD)
                    {
                        bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"发货单整机型号与设备型号不一致，请人工判断型号是否一致\r\n点击确定，继续进行测试；\r\n否则测试项不通过。");
                        if (key != true)
                        {
                            result.AddTestErrMsgs(new ErrMsg(30003, string.Format("设备实际型号{0}和标准信息{1}不一致", deviceType, DevTypeSTD)));
                            result.Conclusion += string.Format("设备实际型号{0}和标准信息{1}不一致", deviceType, DevTypeSTD);
                        }
                        else
                        {
                            msg1.Content += "人工判定通过";
                        }

                    }

                }
            }
            RealTimeMsg msg5 = new RealTimeMsg("设置正压造压范围:(7~8.5)MPa");
            item.AddRealTimeMsgs(msg5);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetPressureRange(ModuleName.Pressure, 7000, 8500))
            {
                msg5.Content = "×";
                return result.AddTestErrMsgs(new ErrMsg(20001, "正压造压范围设置失败"));
            }
            msg5 = new RealTimeMsg("设置真空造压范围:(5~20)kPa.a");
            item.AddRealTimeMsgs(msg5);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetPressureRange(ModuleName.Vacuum, 5, 20))
            {
                msg5.Content = "×";
                return result.AddTestErrMsgs(new ErrMsg(20001, "真空造压范围设置失败"));
            }

            RealTimeMsg msg6 = new RealTimeMsg("发货单的造压范围");
            item.AddRealTimeMsgs(msg6);
            string pressurerang = item.Root.DUT.DeviceRange;
            //if (string.IsNullOrEmpty(pressurerang))
            //{
            //    bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"发货单造压范围获取失败，请人工判断造压范围是否一致\r\n点击确定，继续进行测试；\r\n否则测试项不通过。");
            //    if (key != true)
            //    {
            //        result.AddTestErrMsgs(new ErrMsg(20001, "当前设备造压范围不符合要求"));
            //        return result;
            //    }
            //    else
            //    {
            //        msg6.Content = "人工判定通过";
            //    }


            //}
            //else
            //{
            //    //增压组件; 压力输出范围 :(0~8.5)MPa; 
            //    string[] rangelist = pressurerang.Split(';')[0].Split(',');
            //    msg6.Content = $"{rangelist[0]}{rangelist[1]}";
            //    if (!msg6.Content.Equals(preRange))
            //    {
            //        bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"请人工判断造压范围是否一致\r\n点击确定，继续进行测试；\r\n否则测试项不通过。");
            //        if (key != true)
            //        {
            //            msg6.Content += "×";
            //            return result.AddTestErrMsgs(new ErrMsg(20001, "造压范围与设置的不同。"));
            //        }
            //        else
            //        {
            //            msg6.Content += "人工判定通过";
            //        }
            //    }
            //    msg5.Content += "√";

            //}

            #region 校验设定点上限
            var msg = new RealTimeMsg("设定点上限调整至主动泄压至设定点");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetPressureADJ(OpenCloseState.Open))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "设备无法获取主动泄压设定点状态";
                result.AddTestErrMsgs(error);
                return result;
            }

            msg.Content = "√";
            #endregion

            #region 屏幕亮度
            msg = new RealTimeMsg("调整屏幕亮度至50");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetScreenBRIG(50))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "设备无法获取设备亮度信息";
                result.AddTestErrMsgs(error);
                return result;
            }

            msg.Content = "√";
            #endregion
            #region 触摸及提示音
            msg = new RealTimeMsg("开启触摸及提示音提示功能");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetSystemSound(OpenCloseState.Open))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "设备无法获取设备亮度信息";
                result.AddTestErrMsgs(error);
                return result;
            }

            msg.Content = "√";
            #endregion


            #region 波特率
            msg = new RealTimeMsg("设置设备波特率(115200,8,1,None)");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetMCUBaudrate(115200, 8, StopBits.One, Parity.None))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "无法获取设备波特率信息";
                result.AddTestErrMsgs(error);
                return result;
            }
            //if (baudrate != $"115200,8,{StopBits.One},{Parity.None}")
            //{
            //    msg.Content = "×";
            //    ErrMsg error = ErrMsg._402008;
            //    error.ErrDescribe = $"设备波特率设置（{baudrate}）与实际需求不一致(115200,8,1,None)";
            //    result.AddTestErrMsgs(error);
            //    return result;
            //}
            msg.Content = "√";
            #endregion

            #region 静音模式
            msg = new RealTimeMsg("静音模式开启");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetPressureMute(OpenCloseState.Open))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "无法获取设备是静音模式是否开启";
                result.AddTestErrMsgs(error);
                return result;
            }
            //if (state != OpenCloseState.Open)
            //{
            //    msg.Content = "×";
            //    ErrMsg error = ErrMsg._402008;
            //    error.ErrDescribe = $"设备静音模式未开启";
            //    result.AddTestErrMsgs(error);
            //    return result;
            //}
            msg.Content = "√";
            #endregion

            #region 获取真空气源开机排水模式
            msg = new RealTimeMsg("空气源开机排水模式开启");
            item.AddRealTimeMsgs(msg);
            if (!item.GetDevice<P27CommonBase>("ConST171A").SetPressureVacuumVent(OpenCloseState.Open))
            {
                msg.Content = "×";
                ErrMsg error = ErrMsg._201010;
                error.ErrDescribe = "无法获取设备真空开机排水使能状态";
                result.AddTestErrMsgs(error);
                return result;
            }
            //if (state != OpenCloseState.Open)
            //{
            //    msg.Content = "×";
            //    ErrMsg error = ErrMsg._402008;
            //    error.ErrDescribe = $"设备真空开机排水使能未处于开启状态";
            //    result.AddTestErrMsgs(error);
            //    return result;
            //}
            msg.Content = "√";
            #endregion
            #endregion
            return result;
        }
        catch (Exception ex) { return result.AddTestErrMsgs(new ErrMsg(20001, $"程序执行异常，请联系开发工程师{ex.Message},{ex.StackTrace},{ex.InnerException}")); }
        finally
        {
        }
    }

    /// <summary>
    /// 开关机检测。手动
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic OpenAndClose(dynamic item)
    {
        Result<List<DataBase>> result = new Result<List<DataBase>>(true);
        try
        {
            //清除之前测试信息
            item.ClearRealTimeMsgs();
            bool? key = ScriptHelper.OpenInfoImgConfirmWindow("设备应能正常开关机，显示屏能正常开启，无白屏、未响应现象。", "");
            if (key != true)
            {
                return result.SetConclusion("人为判定不通过", ErrMsg._501002);
            }
            item.AddRealTimeMsgs(new RealTimeMsg("开关机检测", "通过"));
            return result;
        }
        finally
        {
        }
    }

    /// <summary>
    /// 出厂检验和数据审核相关 的公共测试方法
    /// </summary>
    /// <param name="item"></param>
    /// <param name="TestName"></param>
    /// <returns></returns>
    public dynamic GetDataAuditing(dynamic item)
    {
        Result<BooleanData> result = new Result<BooleanData>(true);
        BooleanData rData = new BooleanData("数据审核状态");
        result.Data = rData;
        item.ClearRealTimeMsgs();
        //获取参数
        if (item.Parameters == null)
        {
            result.AddTestErrMsgs(new ErrMsg(10001, "参数为空"));
            return result;
        }
        TextParameter SN = item.Parameters[0] as TextParameter;
        string MeterCode = SN.Value;
        TextParameter isDataAuditingFinishedPar = item.Parameters[1] as TextParameter;
        bool isDataAuditingFinished = Convert.ToBoolean(isDataAuditingFinishedPar.Value); //是否数据审核
        rData.Value = isDataAuditingFinished;


        RealTimeMsg msg1 = new RealTimeMsg("数据审核状态");
        item.AddRealTimeMsgs(msg1);


        if (isDataAuditingFinished)
        {
            msg1.Content = "数据审核通过";
        }
        else
        {
            bool? key = ScriptHelper.OpenInfoConfirmWindow(item, $"当前设备暂未审核，请人工审核是否通过。");
            if (key != true)
            {
                msg1.Content += "×";
                return result.AddTestErrMsgs(new ErrMsg(20001, "人工判定失败。"));
            }
            else
            {
                msg1.Content += "人工判定通过";
            }
        }
        return result;
    }


    #endregion
}
