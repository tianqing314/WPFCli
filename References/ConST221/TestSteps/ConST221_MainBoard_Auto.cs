using Bots.TestBench.Device;
using Bots.TestBench.Model.Scripts;
using Bots.TestBench.Model.Task;
using Bots.TestBench.UI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Bots.TestBench.Device.ConST221_2;
class ConST221_MainBoard_Auto
{
    public class DecorationClass
    {
        public ConST221_2 P22;
        public DynamicStandardTestBench DSTB;
        public List<ConditionBase> Conditions;
        public List<ParameterBase> Parameters;
    }
    DecorationClass GetDC(AutoTestItem ati)
    {
        return new DecorationClass
        {
            P22 = ati.GetDevice<ConST221_2>("P22"),
            DSTB = ati.GetDevice<DynamicStandardTestBench>("DynamicStandardTestBench"),
            Conditions = ati.Conditions as List<ConditionBase>,
            Parameters = ati.Parameters as List<ParameterBase>
        };
    }
        /// <summary>
        /// 配置方法
        /// </summary>
        /// <param name="item">测试项</param>
        /// <returns>测试结果</returns>
    public bool WithInValue(double value, double target, double tolerance)
    {
        return value >= target - tolerance && value <= target + tolerance;
    }
        /// <summary>
        /// 配置方法
        /// </summary>
        /// <param name="item">测试项</param>
        /// <returns>测试结果</returns>
    public bool WithInPercentage(double value, double target, double percentage)
    {
        return value >= target * (100 - percentage) / 100 && value <= target * (100 + percentage) / 100;
    }
    /// <summary>
    /// 工装准备
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic BenchPreparation(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            ScriptHelper.AddNewRange(DC.DSTB.NetSwitchACloseAllChannels());
            ScriptHelper.AddNewRange(DC.DSTB.NetSwitchBCloseAllChannels());
            ScriptHelper.AddNewRange(DC.DSTB.NetSwitchCCloseAllChannels(false));
            ScriptHelper.AddNewRange(DC.DSTB.RespondToCommand(DynamicStandardTestBench.CommandEnum.继电器A_1档位_CHA_1_2_3_4_5_6_通道电源选择_3_3V供电));
            ScriptHelper.AddNewRange(DC.DSTB.RespondToCommand(DynamicStandardTestBench.CommandEnum.继电器C_18档位_CHA_1_2_3_4_5_6_通道电源控制_上电));
            ScriptHelper.Thread_Sleep(new ScriptHelperKVP(10 * 1000));
            var res = DC.P22.ReplenishLink();
            if (!res)
            {
                ScriptHelper.AddEasyResult(new ScriptHelperKVP("建立连接失败,重试中"));

            }
            ScriptHelper.AddEasyResult(new ScriptHelperKVP("建立连接", res));
        });
    }
    /// <summary>
    /// 开始前校验
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic BeginForeChecksum(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {

        });
    }
    /// <summary>
    /// 电源轨测试
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic PowerSourceTrackTest(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            var vbackCondition = DC.Conditions.FirstOrDefault(o => o.Name == "VBACK指标") as RangeCondition;
            var closeVolt = DC.Conditions.FirstOrDefault(o => o.Name == "关闭电源") as RangeCondition;
            var openVolt = DC.Conditions.FirstOrDefault(o => o.Name == "打开电源") as RangeCondition;
            //1.被检关闭CDP/液晶屏/触摸屏/FRAM/FLASH电源
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.CDP电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.液晶屏电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.触摸屏电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FRAM电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FLASH电源关闭));
            //2.读取8路电压采集器前6通道测量值，CDP/液晶屏/触摸屏/FRAM/FLASH/VBACK电源
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(0, out double CDPVoltage), closeVolt.Lower, closeVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(1, out double LCDScreen), closeVolt.Lower, closeVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(2, out double TouchScreen), closeVolt.Lower, closeVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(3, out double FramVolt), closeVolt.Lower, closeVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(4, out double FlashVolt), closeVolt.Lower, closeVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(5, out double VbackVolt), vbackCondition.Lower, vbackCondition.Upper);
            //被检打开CDP/液晶屏/触摸屏/FRAM/FLASH电源
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.CDP电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.液晶屏电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.触摸屏电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FRAM电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FLASH电源打开));
            //读取8路电压采集器前6通道测量值，CDP/液晶屏/触摸屏/FRAM/FLASH/VBACK电源
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(0, out CDPVoltage), openVolt.Lower, openVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(1, out LCDScreen), openVolt.Lower, openVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(2, out TouchScreen), openVolt.Lower, openVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(3, out FramVolt), openVolt.Lower, openVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(4, out FlashVolt), openVolt.Lower, openVolt.Upper);
            ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(5, out VbackVolt), vbackCondition.Lower, vbackCondition.Upper);
        });
    }
    /// <summary>
    /// RTC测试
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic RTCTest(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.能够写入读取时间即通过));
        });
    }
    /// <summary>
    /// 铁电测试
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic FerroElectricityTest(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.铁电正常写入擦除或其它操作));
        });
    }
    /// <summary>
    /// FLASH测试
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic FLASHTest(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FLASH正常写入擦除或其它操作));
        });
    }
    /// <summary>
    /// 功耗测试
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic ConsumeTest(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            var vbackCloseCondition = DC.Conditions.FirstOrDefault(o => o.Name == "功耗范围(关闭)") as RangeCondition;
            var vbackOpenCondition = DC.Conditions.FirstOrDefault(o => o.Name == "功耗范围(打开)") as RangeCondition;
            //1.被检关闭CDP/液晶屏/触摸屏/FRAM/FLASH电源
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.CDP电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.液晶屏电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.触摸屏电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FRAM电源关闭));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FLASH电源关闭));
            ScriptHelper.Thread_Sleep(new ScriptHelperKVP(5 * 1000));
            List<ScriptHelperKVP> currents = new List<ScriptHelperKVP>();
            while (currents.Count < 30)
            {
                ScriptHelperKVP scriptKvp = DC.DSTB.GetCurrentMeasureValue(false, 1, out double value);
                if (value != double.NaN)
                    currents.Add(scriptKvp);
                Thread.Sleep(500);
            }
            currents = ScriptHelperKVP.TrimCurrents(currents);
            ScriptHelper.AddNewIsRangeJudge(new ScriptHelperKVP(currents.FirstOrDefault().Content) { JudgeObject = currents.Average(o => double.Parse(o.JudgeObject.ToString())) }, vbackCloseCondition.Lower, vbackCloseCondition.Upper);
            //被检打开CDP/液晶屏/触摸屏/FRAM/FLASH电源
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.CDP电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.液晶屏电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.触摸屏电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FRAM电源打开));
            ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.FLASH电源打开));
            ScriptHelper.Thread_Sleep(new ScriptHelperKVP(5 * 1000));
            currents = new List<ScriptHelperKVP>();

            while (currents.Count < 30)
            {
                ScriptHelperKVP scriptKvp = DC.DSTB.GetCurrentMeasureValue(false, 1, out double value);
                if (value != double.NaN)
                    currents.Add(scriptKvp);
                Thread.Sleep(500);
            }
            currents = ScriptHelperKVP.TrimCurrents(currents);
            ScriptHelper.AddNewIsRangeJudge(new ScriptHelperKVP(currents.FirstOrDefault().Content) { JudgeObject = currents.Average(o => double.Parse(o.JudgeObject.ToString())) }, vbackOpenCondition.Lower, vbackOpenCondition.Upper);
        });
    }
    /// <summary>
    /// 测试结束
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public dynamic TestFinish(dynamic item)
    {
        var DC = GetDC(item as AutoTestItem);
        ScriptHelper.SetDisplayer(item as AutoTestItem);
        return ScriptHelper.WatchAndProcessIntergrade(false, () =>
        {
            //1.网络继电器A切档1：CHA通道3.3V供电
            //2.网络继电器C切档17：CHA通道断电
            ScriptHelper.AddNewRange(DC.DSTB.RespondToCommand(DynamicStandardTestBench.CommandEnum.继电器A_1档位_CHA_1_2_3_4_5_6_通道电源选择_3_3V供电));
            ScriptHelper.AddNewRange(DC.DSTB.RespondToCommand(DynamicStandardTestBench.CommandEnum.继电器C_17档位_CHA_1_2_3_4_5_6_通道电源控制_断电));
        });
    }
}

