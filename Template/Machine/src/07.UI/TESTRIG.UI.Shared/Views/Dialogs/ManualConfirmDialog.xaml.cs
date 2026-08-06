using System.Windows;
using System.Windows.Threading;
using TESTRIG.Core.Abstractions;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views.Dialogs;

/// <summary>
/// 人工确认对话框（整机模板 ManualStep）：显示测试项名称与操作指引，操作员观察后点 通过/不合格。
/// 可选超时：倒计时归零自动按不合格收尾并关闭。非模态（<see cref="Show"/>），不阻塞其他号位 UI。
/// </summary>
public partial class ManualConfirmDialog : ChromeWindow
{
    /// <summary>
    /// 确认请求参数（关闭时回传结果给引擎）。
    /// </summary>
    private readonly ManualConfirmRequestedEventArgs _args;

    /// <summary>
    /// 超时倒计时（未配置超时则为 null）。
    /// </summary>
    private readonly DispatcherTimer? _timer;

    /// <summary>
    /// 超时截止时刻。
    /// </summary>
    private readonly System.DateTime _deadline;

    /// <summary>
    /// 本次确认结论（关闭时回传；未点按即超时）。
    /// </summary>
    private ManualConfirmResult _result = ManualConfirmResult.Timeout;

    /// <summary>
    /// 构造人工确认对话框。
    /// </summary>
    /// <param name="args">引擎发布的确认请求（含测试项与超时配置）。</param>
    public ManualConfirmDialog(ManualConfirmRequestedEventArgs args)
    {
        InitializeComponent();
        _args = args;

        Title = $"人工确认 · {args.Step.Name}";
        StepNameText.Text = args.Step.Name;
        DescText.Text = string.IsNullOrWhiteSpace(args.Step.Description)
            ? "请操作员按操作指引观察测试项并确认结果。"
            : args.Step.Description;

        if (args.TimeoutMs > 0)
        {
            _deadline = System.DateTime.Now.AddMilliseconds(args.TimeoutMs);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                var remain = (int)(_deadline - System.DateTime.Now).TotalSeconds;
                if (remain <= 0)
                {
                    _timer.Stop();
                    TimeoutText.Text = "确认超时，按不合格处理。";
                    Close();
                }
                else
                {
                    TimeoutText.Text = $"请在 {remain} 秒内确认（超时按不合格处理）";
                }
            };
            _timer.Start();
        }
        else
        {
            TimeoutText.Visibility = Visibility.Collapsed;
        }

        // 关闭（无论点按/超时/标题栏 X）都把结论回传引擎，避免号位挂死
        Closed += (_, _) =>
        {
            _timer?.Stop();
            _args.Respond(_result);
        };
    }

    /// <summary>
    /// 通过：结论 OK 并关闭。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _result = ManualConfirmResult.Ok;
        Close();
    }

    /// <summary>
    /// 不合格：结论 NG 并关闭。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void Ng_Click(object sender, RoutedEventArgs e)
    {
        _result = ManualConfirmResult.Ng;
        Close();
    }
}
