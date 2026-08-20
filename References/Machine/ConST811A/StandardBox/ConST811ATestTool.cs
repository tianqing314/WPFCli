using Bots.TestBench.Device.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Device;
using Xmas11.Comm.Devices;

namespace Bots.TestBench.Device
{
    /// <summary>
    /// 811A工装设备通讯库
    /// </summary>
    public class ConST811ATestTool : Base.BaseDevice
    {
        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public ConST811ATestTool()
        {
            this.DeviceType = DeviceType.Tool;
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取82X工装
        /// </summary>
        public ConSTGZ811A GZ811A
        {
            get
            {
                if (this.CommInstance == null || !(this.CommInstance is ConSTGZ811A))
                {
                    return null;
                }
                else
                {
                    return this.CommInstance as ConSTGZ811A;
                }
            }
        }
        #endregion

        #region 方法

        #region 打开、关闭
        Task<bool> openTask;

        Task closeTask;
        /// <summary>
        /// 打开
        /// </summary>
        /// <returns></returns>
        public override bool Open()
        {
            if (closeTask != null && closeTask.Status == TaskStatus.Running)
            {
                return false;
            }
            if (openTask == null || openTask.Status != TaskStatus.Running)
            {
                openTask = Task<bool>.Run(() =>
                {
                    ConnectStatus = ConnectStatus.Connectting;
                    try
                    {
                        AddressChanged();
                        if (CommInstance != null)
                        {
                            CommInstance.Close();
                            CommInstance = null;
                        }
                        this.CommInstance = new Xmas11.Comm.Device.ConSTGZ811A(this.CommConfig.GetCommSettings());
                        if (!this.CommInstance.Connected)
                        {
                            this.CommInstance.Open();
                        }
                    }
                    catch (Exception)
                    {
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

                });
                openTask.Wait();
                return openTask.Result;

            }
            return false;
        }
        /// <summary>
        /// 关闭
        /// </summary>
        /// <returns></returns>
        public override void Close()
        {
            if (openTask != null && openTask.Status == TaskStatus.Running)
            {
                return;
            }
            if (closeTask == null || closeTask.Status != TaskStatus.Running)
            {
                closeTask = Task<bool>.Run(() =>
                {
                    ConnectStatus = ConnectStatus.DisConnectting;
                    if (this.CommInstance != null)
                    {
                        if (this.CommInstance.CommInstance != null)
                        {
                            this.CommInstance.CommInstance.Dispose();
                        }
                        this.CommInstance.Close();
                        this.CommInstance = null;
                    }
                    ConnectStatus = ConnectStatus.DisConnected;
                });
                closeTask.Wait();
            }
        }
        #endregion

        #region 操作阀门
        /// <summary>
        /// 获取启动状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetStartState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetX1SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }
        /// <summary>
        /// 打开绿灯
        /// </summary>
        /// <returns></returns>
        public bool SetGreenLightOpen()
        {
            iResponse result = GZ811A.SetY5SwitchState(OpenCloseState.Open, 0);
            return result.IsCorrect;
        }
        /// <summary>
        /// 关闭绿灯
        /// </summary>
        /// <returns></returns>
        public bool SetGreenLightClose()
        {
            iResponse result = GZ811A.SetY5SwitchState(OpenCloseState.Close, 0);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取绿灯状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetGreenLightState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY5SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }

        /// <summary>
        /// 打开红灯
        /// </summary>
        /// <returns></returns>
        public bool SetRedLightOpen()
        {
            iResponse result = GZ811A.SetY6SwitchState(OpenCloseState.Open, 0);
            return result.IsCorrect;
        }
        /// <summary>
        /// 关闭红灯
        /// </summary>
        /// <returns></returns>
        public bool SetRedLightClose()
        {
            iResponse result = GZ811A.SetY6SwitchState(OpenCloseState.Close, 0);
            return result.IsCorrect;
        }
        /// <summary>
        /// 获取红灯状态
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool GetRedLightState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY6SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }


        /// <summary>
        /// 设置27V状态
        /// </summary>
        /// <returns></returns>
        public bool Set27VState(OpenCloseState state)
        {
            iResponse result = GZ811A.SetY1SwitchState(state, 0);
            return result.IsCorrect;
        }

        /// <summary>
        /// 读取27V状态
        /// </summary>
        /// <returns></returns>
        public bool Gett27VState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY1SwitchState();
            state = result.Result;
            return result.IsCorrect;

        }

        /// <summary>
        /// 设置Hart状态
        /// </summary>
        /// <returns></returns>
        public bool SetHartState(OpenCloseState state)
        {
            iResponse result = GZ811A.SetY2SwitchState(state, 0);
            return result.IsCorrect;
        }

        /// <summary>
        /// 读取Hart状态
        /// </summary>
        /// <returns></returns>
        public bool GetHartState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY2SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }


        /// <summary>
        /// 设置PA状态
        /// </summary>
        /// <returns></returns>
        public bool SetPAState(OpenCloseState state)
        {
            iResponse result = GZ811A.SetY3SwitchState(state, 0);
            return result.IsCorrect;
        }

        /// <summary>
        /// 读取PA状态
        /// </summary>
        /// <returns></returns>
        public bool GetPAState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY3SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }


        /// <summary>
        ///设置电测状态
        /// </summary>
        /// <returns></returns>
        public bool SetEleState(OpenCloseState state)
        {
            iResponse result = GZ811A.SetY4SwitchState(state, 0);
            return result.IsCorrect;
        }

        /// <summary>
        /// 读取电测状态
        /// </summary>
        /// <returns></returns>
        public bool GetEleState(out OpenCloseState state)
        {
            state = OpenCloseState.UnKnown;
            iResponse<OpenCloseState> result = GZ811A.GetY4SwitchState();
            state = result.Result;
            return result.IsCorrect;
        }

        #endregion

        #endregion
    }
}
