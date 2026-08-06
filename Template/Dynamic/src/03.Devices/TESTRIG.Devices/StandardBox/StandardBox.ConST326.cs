using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices.Abstractions;
using Xmas11.Comm.Data.Common;
using Xmas11.Comm.Devices;
using Xmas11.Comm.Devices.DPC2;
using Xmas11.Domain.Thermology;

namespace TESTRIG.Devices.StandardBox;

/// <summary>
/// <see cref="StandardBox"/> 的 ConST560 测量板扩展：ConST326 标准表源/测（Xmas11 <c>DPC2Base</c>）、
/// DAM6803D 电压表、基于 <see cref="BoxRelayCommand"/> 的继电器切换。**忠实迁移**旧
/// <c>DynamicStandardTestBench</c>（DSTB.cs / StringInfos.cs）。ConST326/DAM6803D 惰性连接——
/// 仅用到（跑 ConST560 测量板）时才建连，避免影响不需要它们的板（如 218A）。
/// </summary>
public sealed partial class StandardBox
{
    /// <summary>
    /// ConST326 标准表（DPC2 协议，串口）。惰性建连。
    /// </summary>
    private DPC2Base? _const326;

    /// <summary>
    /// DAM6803D 电压采集模块（网络）。惰性建连。
    /// </summary>
    private DAM6803D? _dam6803d;

    /// <summary>
    /// 继电器指令(枚举) → 寄存器(继电器,路号,通/断)列表。惰性解析自 <see cref="RelayConfigs"/>。
    /// </summary>
    private static Dictionary<BoxRelayCommand, List<(char Relay, int Road, bool On)>>? _relayMap;

    /// <summary>
    /// (继电器, 档位号) → 寄存器列表。**与 <see cref="_relayMap"/> 同一张表**，供按动态档位号切换（如 218A 的 Pos*2+33）。
    /// 合并了旧 strToInit 真值表——两表已核对逐档一致，故统一到本寄存器表。
    /// </summary>
    private static Dictionary<(char Relay, int Gear), List<(char Relay, int Road, bool On)>>? _gearIndex;

    /// <summary>
    /// 真连通性测试：按子设备键构造对应驱动、真 <c>Open()</c> + <c>IsExist()</c> 探活，
    /// 测完立即关闭释放端口（不占用，测试跑起来时再由各自路径重连）。供连接配置页调用。
    /// </summary>
    /// <param name="key">子设备键。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>(是否连通, 说明)。</returns>
    public Task<(bool Ok, string Message)> TestSubDeviceAsync(string key, CommEndpoint endpoint, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            try
            {
                return key switch
                {
                    "ConST326" => TestConST326(endpoint),
                    "DAM6803D" => TestNet(key, endpoint, (ip, port) => new DAM6803D(ip, port, true)),
                    "BNRC32A" or "BNRC32B" => TestNet(key, endpoint, (ip, port) => new BNRC32(ip, port)),
                    "BNRC16C" => TestNet(key, endpoint, (ip, port) => new BNRC16(ip, port)),
                    "ZH4402A" => TestNet(key, endpoint, (ip, port) => new ZH44023D(ip, port)),
                    "ZH4412A" => TestNet(key, endpoint, (ip, port) => new ZH4412(ip, port)),
                    _ => (false, $"未知子设备 {key}"),
                };
            }
            catch (Exception ex)
            {
                return (false, $"{key} 连接异常：{ex.Message}");
            }
        }, ct);
    }

    /// <summary>
    /// 测 ConST326（串口，端点取页面当前值）：Open + IsExist，测完关闭。
    /// </summary>
    /// <param name="ep">通讯端点（串口）。</param>
    /// <returns>(是否连通, 说明)。</returns>
    private (bool Ok, string Message) TestConST326(CommEndpoint ep)
    {
        var sp = ep.Serial ?? new SerialParams();
        // 配置页存的是物理链路（USB 端口链），需解析成当前 COM 才能开口；填 "COMx" 时解析器按 Com 直接匹配
        var r = _resolver.Resolve(ep);
        if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
        {
            return (false, $"ConST326 串口解析失败：{r.Message}");
        }
        var portName = r.Target;
        // 占用预检：被占/不存在直接给可读提示，不再抛原生英文异常
        var probe = Comm.SerialPortProbe.Probe(portName);
        if (!probe.Ok)
        {
            return (false, $"ConST326 {probe.Message}");
        }
        var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;
        var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;

        var dpc = new DPC2Base(portName, sp.Baud, sp.DataBits, stopBits, parity);
        try
        {
            var ok = dpc.Open() && dpc.IsExist();
            return (ok, ok ? $"ConST326 连接成功（{portName}）" : $"ConST326 无响应（{portName}）");
        }
        finally
        {
            try { dpc.Close(); } catch { /* 忽略关闭异常 */ }
        }
    }

    /// <summary>
    /// 测一个网络子设备：校验端点、用 <paramref name="factory"/> 建驱动、Open，测完关闭。
    /// </summary>
    /// <param name="key">子设备键。</param>
    /// <param name="ep">通讯端点（网络，取页面当前值）。</param>
    /// <param name="factory">按 (IP,端口) 建驱动的工厂。</param>
    /// <returns>(是否连通, 说明)。</returns>
    private static (bool Ok, string Message) TestNet(string key, CommEndpoint ep, Func<IPAddress, int, dynamic> factory)
    {
        if (ep.Link != LinkType.Ethernet || string.IsNullOrWhiteSpace(ep.Ip) || ep.Port is null)
        {
            return (false, $"{key} 缺少网络端点（IP/端口）");
        }
        var ip = IPAddress.Parse(ep.Ip);
        var port = ep.Port.Value;
        dynamic dev = factory(ip, port);
        try
        {
            // Open() 后再 IsExist() 探活（与 ConST326 一致）：仅 Open 成功不代表设备真在线
            bool opened = dev.Open();
            bool ok = opened && (bool)dev.IsExist();
            var msg = ok
                ? $"{key} 连接成功（{ip}:{port}）"
                : opened
                    ? $"{key} 无响应（Open 成功但 IsExist 失败，{ip}:{port}）"
                    : $"{key} 连接失败（{ip}:{port}）";
            return (ok, msg);
        }
        finally
        {
            try { dev.Close(); } catch { /* 忽略关闭异常 */ }
        }
    }

    /// <summary>
    /// 惰性建连并返回 ConST326（DPC2Base，串口）。端点取连接配置里的 ConST326 子设备。
    /// </summary>
    /// <returns>ConST326 通讯实例。</returns>
    private DPC2Base EnsureConST326()
    {
        if (_const326 is not null)
        {
            return _const326;
        }

        var sub = _connections.StandardBox.FirstOrDefault(s => s.Key == "ConST326");
        var ep = sub?.Comm ?? CommEndpoint.OfSerial("COM1");
        var sp = ep.Serial ?? new SerialParams();
        // 物理链路号 → 当前实际 COM（COM 号会变、物理链路不变）。填的是 "COMx" 时解析器也按 Com 直接匹配。
        var r = _resolver.Resolve(ep);
        if (!r.Ok || string.IsNullOrWhiteSpace(r.Target))
        {
            throw new DeviceCommException($"ConST326 串口解析失败：{r.Message}", TestResultStatus.HardwareError);
        }
        var portName = r.Target;
        // 占用预检：把原生 "Access denied" 提前翻成可读提示
        var probe = Comm.SerialPortProbe.Probe(portName);
        if (!probe.Ok)
        {
            throw new DeviceCommException($"ConST326 串口不可用：{probe.Message}", TestResultStatus.HardwareError);
        }
        var stopBits = Enum.TryParse<StopBits>(sp.StopBits, out var sb) ? sb : StopBits.One;
        var parity = Enum.TryParse<Parity>(sp.Parity, out var pa) ? pa : Parity.None;

        var dpc = new DPC2Base(portName, sp.Baud, sp.DataBits, stopBits, parity);
        if (!(dpc.Open() && dpc.IsExist()))
        {
            throw new DeviceCommException($"ConST326 标准表连接失败（{portName}）", TestResultStatus.HardwareError);
        }

        _const326 = dpc;
        _logger.LogInformation("ConST326 标准表连接成功 {Port}", portName);
        return dpc;
    }

    /// <summary>
    /// 惰性建连并返回 DAM6803D（网络）。端点取连接配置里的 DAM6803D 子设备。
    /// </summary>
    /// <returns>DAM6803D 实例。</returns>
    private DAM6803D EnsureDam()
    {
        if (_dam6803d is not null)
        {
            return _dam6803d;
        }

        var sub = _connections.StandardBox.FirstOrDefault(s => s.Key == "DAM6803D");
        var ep = sub?.Comm ?? CommEndpoint.OfEthernet("192.168.40.22", 502);
        var dam = new DAM6803D(IPAddress.Parse(ep.Ip ?? "192.168.40.22"), ep.Port ?? 502, true);
        if (!dam.Open())
        {
            throw new DeviceCommException("DAM6803D 电压采集模块连接失败", TestResultStatus.HardwareError);
        }

        _dam6803d = dam;
        _logger.LogInformation("DAM6803D 电压采集模块连接成功 {Ip}:{Port}", ep.Ip, ep.Port);
        return dam;
    }

    /// <summary>
    /// ConST326 输出档位切换。PORT: DSTB.ConST326SwitchOutputGearTo。
    /// </summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetOutputGearAsync(Gear326 gear, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            var dpc = EnsureConST326();
            switch (gear)
            {
                case Gear326.mAWith24V:
                    dpc.Set24VState(Power24VState.Open);
                    dpc.SetCalibratorOutputFunction(SourceChannelType.ES_mA);
                    break;
                case Gear326.mA:
                    dpc.SetCalibratorOutputFunction(SourceChannelType.ES_mA);
                    break;
                case Gear326.V:
                    // PORT: SetSouceGear_ES_V —— 新硬件走 ChangeToSource_V_H，否则 ES_V
                    if (dpc.IsNewESHardware())
                    {
                        dpc.ChangeToSource_V_H();
                    }
                    else
                    {
                        dpc.SetCalibratorOutputFunction(SourceChannelType.ES_V);
                    }
                    break;
                case Gear326.mV:
                    dpc.SetCalibratorOutputFunction(SourceChannelType.ES_mV);
                    break;
                case Gear326.Close24V:
                    dpc.Set24VState(Power24VState.Close);
                    break;
                case Gear326.TC_K:
                    // PORT: ConST326SwitchOutputGearTo(TC_K) —— TS_TC 源 + K 型内部冷端补偿
                    dpc.SetCalibratorOutputFunction(SourceChannelType.TS_TC);
                    dpc.SetCalibratorOutputTCConfig(new TCConfig
                    {
                        TCSensorType = TCSensorType.K,
                        TCUnit = TemperatureUnit.C,
                        CJCType = Xmas11.Comm.Devices.DPC2.CJCType.Inner,
                    });
                    break;
                case Gear326.TC_N:
                    // PORT: ConST326SwitchOutputGearTo(TC_N) —— TS_TC 源 + N 型固定冷端 0℃
                    dpc.SetCalibratorOutputFunction(SourceChannelType.TS_TC);
                    dpc.SetCalibratorOutputTCConfig(new TCConfig
                    {
                        TCSensorType = TCSensorType.N,
                        TCUnit = TemperatureUnit.C,
                        CJCType = Xmas11.Comm.Devices.DPC2.CJCType.Fixed,
                        CJCValue = 0,
                    });
                    break;
                default:
                    // Switch 等 ConST560/ConST660 未用到，暂不支持
                    _logger.LogWarning("ConST326 输出档 {Gear} 未支持", gear);
                    break;
            }
        }, ct);
    }

    /// <summary>
    /// ConST326 测量档位切换。PORT: DSTB.ConST326SwitchMeasureGearTo。
    /// </summary>
    /// <param name="gear">目标档位。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetMeasureGearAsync(Gear326 gear, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            var dpc = EnsureConST326();
            switch (gear)
            {
                case Gear326.V:
                    dpc.SetCalibratorMeasureFunction(MeasureChannelType.EM_V);
                    break;
                case Gear326.mAWith24V:
                    dpc.SetCalibratorMeasureFunction(MeasureChannelType.EM_mA);
                    dpc.Set24VState(Power24VState.Open);
                    break;
                case Gear326.mA:
                    dpc.SetCalibratorMeasureFunction(MeasureChannelType.EM_mA);
                    break;
                case Gear326.mV:
                    dpc.SetCalibratorMeasureFunction(MeasureChannelType.EM_mV);
                    break;
                case Gear326.Switch:
                    dpc.SetCalibratorMeasureFunction(MeasureChannelType.EM_Switch);
                    break;
                case Gear326.Close24V:
                    dpc.Set24VState(Power24VState.Close);
                    break;
                default:
                    _logger.LogWarning("ConST326 测量档 {Gear} 未支持", gear);
                    break;
            }
        }, ct);
    }

    /// <summary>
    /// ConST326 设置电压输出值（V）。PORT: DSTB.ConST326SetOutputVoltage_V。
    /// </summary>
    /// <param name="volts">电压（V）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetOutputVoltageVAsync(double volts, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => EnsureConST326().SetCalibratorOutputValue(volts), ct);
    }

    /// <summary>
    /// ConST326 设置电流输出值（mA）。PORT: DSTB.ConST326SetOutputCurrent_mA。
    /// </summary>
    /// <param name="milliAmps">电流（mA）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetOutputCurrentMaAsync(double milliAmps, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => EnsureConST326().SetCalibratorOutputValue(milliAmps), ct);
    }

    /// <summary>
    /// ConST326 设置热电偶输出温度（℃）。PORT: ConST326SetOutputTCCentigrade（TC 档下 SetSouceValue ℃）。
    /// </summary>
    /// <param name="centigrade">温度（℃）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task SetOutputTCCentigradeAsync(double centigrade, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => EnsureConST326().SetCalibratorOutputValue(centigrade), ct);
    }

    /// <summary>
    /// ConST326 开/关 24V 供电。PORT: DSTB.ConST326OpenDCVoltage/CloseDCVoltage。
    /// </summary>
    /// <param name="open">true 开，false 关。</param>
    /// <param name="ct">取消令牌。</param>
    public Task Set24VAsync(bool open, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => EnsureConST326().Set24VState(open ? Power24VState.Open : Power24VState.Close), ct);
    }

    /// <summary>
    /// ConST326 读当前测量值。PORT: DSTB.ConST326ReadValue。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>测量值（读失败返回 NaN）。</returns>
    public async Task<double> ReadConST326ValueAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var res = EnsureConST326().GetCalibratorMeasureValue();
                return res.IsCorrect ? res.Result.ValueAndUnit.Value : double.NaN;
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// DAM6803D 读某通道电压测量值（通道 0 起）。PORT: DSTB.GetVoltageMeasureValue。
    /// </summary>
    /// <param name="channel">通道（0 起）。</param>
    /// <param name="reverse">是否取反。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>电压测量值。</returns>
    public async Task<double> GetVoltageMeasureValueAsync(int channel, bool reverse = false, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                EnsureDam().ReadValue(channel, out var v);
                return reverse ? -v : v;
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 关闭继电器 C 通道（默认仅前 8 路，逐档反相断开）。PORT: DSTB.NetSwitchCCloseAllChannels。
    /// </summary>
    /// <param name="firstEightOnly">仅关前 8 路。</param>
    /// <param name="ct">取消令牌。</param>
    public Task CloseAllCChannelsAsync(bool firstEightOnly = true, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            if (!firstEightOnly)
            {
                _bnrc16C!.ZQWL_SetOutputCloseAll();
                return;
            }

            // PORT: for i=2,4,...,16 逐档按同一张寄存器表反相断开（on ? 0 : 1）
            EnsureRelayMaps();
            for (int i = 2; i < 17; i += 2)
            {
                if (_gearIndex!.TryGetValue(('C', i), out var regs))
                {
                    ApplyRegs(regs, invert: true);
                }
            }
        }, ct);
    }

    /// <summary>
    /// 关闭继电器 A 全部通道。PORT: NetSwitchACloseAllChannels（BNRC32A.ZQWL_SetOutputCloseAll）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task CloseAllAChannelsAsync(CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => _bnrc32A!.ZQWL_SetOutputCloseAll(), ct);
    }

    /// <summary>
    /// 关闭继电器 B 全部通道。PORT: NetSwitchBCloseAllChannels（BNRC32B.ZQWL_SetOutputCloseAll）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task CloseAllBChannelsAsync(CancellationToken ct = default)
    {
        return RunOnBoxAsync(() => _bnrc32B!.ZQWL_SetOutputCloseAll(), ct);
    }

    /// <summary>
    /// 关闭全部继电器（A/B/C 三组）。PORT: DSTB.NetSwitchCloseAllChannels
    /// （BNRC32A/BNRC32B/BNRC16C 各 ZQWL_SetOutputCloseAll）。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    public Task CloseAllRelaysAsync(CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            _bnrc32A!.ZQWL_SetOutputCloseAll();
            _bnrc32B!.ZQWL_SetOutputCloseAll();
            _bnrc16C!.ZQWL_SetOutputCloseAll();
        }, ct);
    }

    /// <summary>
    /// 按继电器指令(枚举)切换。PORT: DSTB.RespondToCommand。
    /// </summary>
    /// <param name="cmd">继电器指令。</param>
    /// <param name="ct">取消令牌。</param>
    public Task RelayCommandAsync(BoxRelayCommand cmd, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            EnsureRelayMaps();
            if (!_relayMap!.TryGetValue(cmd, out var regs))
            {
                _logger.LogWarning("继电器指令 {Cmd} 无映射", cmd);
                return;
            }
            ApplyRegs(regs, invert: false);
            _logger.LogDebug("继电器指令 {Cmd}", cmd);
        }, ct);
    }

    /// <summary>
    /// 按 (继电器, 档位号) 切换一个或多个档位。**统一继电器切换入口**——与枚举法
    /// <see cref="RelayCommandAsync"/> 共用同一张寄存器表；供处理器用动态档位号切换（如 218A 的 Pos*2+33）。
    /// 取代旧独立的 NetSwitchA/B/C 真值表切换。
    /// </summary>
    /// <param name="relay">继电器（A/B/C）。</param>
    /// <param name="gears">档位号（1 起）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task RelayGearAsync(char relay, int[] gears, CancellationToken ct = default)
    {
        return RunOnBoxAsync(() =>
        {
            EnsureRelayMaps();
            // 注意：这里**不要**加"同继电器最小间隔"之类的等待。本方法在共享闸门 _gate 内执行，
            // 任何 Sleep 都会把其它并行号位一起堵住，导致按键测试的「按下→松开」时长被别的号位拉长，
            // 电源键被当成长按而读不到（实测 4 工位并行时工位 1/3 偶发不合格）。
            // 需要间隔由调用方（处理器）在自己的线程里等，与旧平台 SetSleepTime 的语义一致。
            foreach (var gear in gears)
            {
                if (_gearIndex!.TryGetValue((relay, gear), out var regs))
                {
                    ApplyRegs(regs, invert: false);
                }
                else
                {
                    _logger.LogWarning("继电器 {Relay} 无档位 {Gear}", relay, gear);
                }
            }
        }, ct);
    }

    /// <summary>
    /// 把一组寄存器施加到对应继电器板。<paramref name="invert"/>=true 时反相（用于断开）。
    /// </summary>
    /// <param name="regs">寄存器列表。</param>
    /// <param name="invert">是否反相。</param>
    private void ApplyRegs(List<(char Relay, int Road, bool On)> regs, bool invert)
    {
        foreach (var (relay, road, on) in regs)
        {
            var board = relay switch
            {
                'A' => (object)_bnrc32A!,
                'B' => _bnrc32B!,
                'C' => _bnrc16C!,
                _ => null!,
            };
            if (board is null)
            {
                continue;
            }
            dynamic b = board;
            b.ZQWL_SetOutputCMD(invert ? (on ? 0 : 1) : (on ? 1 : 0), road, NetRelayAddress);
        }
    }

    /// <summary>
    /// 串行化执行一段阻塞盒操作（与 <c>_gate</c> 一致，保证并行号位下 socket 操作原子）。
    /// </summary>
    /// <param name="action">阻塞操作。</param>
    /// <param name="ct">取消令牌。</param>
    private async Task RunOnBoxAsync(Action action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await Task.Run(action, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 串行化执行一段阻塞盒操作并取回结果（与 <c>_gate</c> 一致，保证并行号位下 socket 操作原子）。
    /// </summary>
    /// <typeparam name="T">结果类型。</typeparam>
    /// <param name="func">阻塞操作。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>操作结果。</returns>
    private async Task<T> RunOnBoxAsync<T>(Func<T> func, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await Task.Run(func, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 惰性解析继电器寄存器映射串（<see cref="RelayConfigs"/>）为两张同源索引：
    /// 枚举键 <see cref="_relayMap"/> 与 (继电器,档位号) 键 <see cref="_gearIndex"/>。
    /// 格式 <c>指令名(A1:X,A2:O,)指令名2(...)</c>：O=通、X=断；指令名形如 <c>继电器A_33档位_…</c>。
    /// PORT: DSTB.InitializeGearInfos 的 <c>Configs</c> 段（并合并了旧 strToInit）。
    /// </summary>
    private static void EnsureRelayMaps()
    {
        if (_relayMap is not null)
        {
            return;
        }

        var byCmd = new Dictionary<BoxRelayCommand, List<(char, int, bool)>>();
        var byGear = new Dictionary<(char, int), List<(char, int, bool)>>();
        foreach (var item in RelayConfigs.Split(')', StringSplitOptions.RemoveEmptyEntries))
        {
            var splx = item.Split('(');
            if (splx.Length < 2 || !Enum.TryParse<BoxRelayCommand>(splx[0], out var cmd))
            {
                continue;
            }

            var regs = new List<(char, int, bool)>();
            foreach (var reg in splx[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = reg.Split(':');
                if (kv.Length != 2 || kv[0].Length < 2)
                {
                    continue;
                }
                var relay = kv[0][0];
                if (!int.TryParse(kv[0].Substring(1), out var road))
                {
                    continue;
                }
                regs.Add((relay, road, kv[1] == "O"));
            }
            byCmd[cmd] = regs;

            // 从指令名 继电器{X}_{N}档位… 解析 (继电器,档位号)，与枚举指向同一份 regs
            var name = splx[0];
            var us = name.IndexOf('_');
            var dang = name.IndexOf('档');
            if (name.Length > 3 && us > 0 && dang > us && int.TryParse(name[(us + 1)..dang], out var gear))
            {
                byGear[(name[3], gear)] = regs;
            }
        }

        _relayMap = byCmd;
        _gearIndex = byGear;
    }
}
