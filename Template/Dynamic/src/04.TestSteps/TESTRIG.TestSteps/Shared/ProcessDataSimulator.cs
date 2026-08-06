using TESTRIG.Core.Abstractions;

namespace TESTRIG.TestSteps;

/// <summary>
/// 实时采集数据仿真：真实硬件接入前，模拟多通道采集曲线供 test_process_data 落库 + 前端曲线绘制。
/// 真实驱动接入后改为从 DAM6803D/电流计按节拍读真实值即可，数据结构不变。
/// </summary>
public static class ProcessDataSimulator
{
    /// <summary>
    /// 采样间隔（毫秒）。0=不延时（头测/无界面快速跑）；GUI 启动时设为较大值（如 50）以观察实时曲线绘制。
    /// </summary>
    public static int StreamIntervalMs { get; set; }

    /// <summary>
    /// 流式模拟两通道电压采集：约 <paramref name="durationSec"/> 秒、<paramref name="count"/> 个采样点，
    /// 逐点 <see cref="ITestContext.ReportSample"/> 实时推送（UI 曲线随之增长）。
    /// CH1 围绕 3.3V、CH2 围绕 5.0V，叠加缓慢正弦波动 + 小幅随机噪声。
    /// </summary>
    public static async Task StreamTwoChannelVoltageAsync(ITestContext ctx, int count = 60, double durationSec = 20,
        CancellationToken ct = default)
    {
        var rng = new Random();
        var interval = durationSec / count;
        ctx.BeginSampling("V", "CH1 电压", "CH2 电压");

        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = Math.Round(i * interval, 2);
            var v1 = Math.Round(3.30 + 0.05 * Math.Sin(t * 0.6) + (rng.NextDouble() - 0.5) * 0.02, 4);
            var v2 = Math.Round(5.00 + 0.08 * Math.Sin(t * 0.4 + 1.0) + (rng.NextDouble() - 0.5) * 0.03, 4);
            ctx.ReportSample(t, v1, v2);
            if (StreamIntervalMs > 0)
            {
                await Task.Delay(StreamIntervalMs, ct);
            }
        }
    }
}
