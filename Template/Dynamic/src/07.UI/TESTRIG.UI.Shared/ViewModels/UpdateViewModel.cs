using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.UI.Shared.Services;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 更新对话框视图模型：展示版本/说明，驱动 下载(进度/校验) → 拉起升级器重启；
/// 强更时不可关闭（暂不按钮隐藏），测试运行中禁止安装。
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    /// <summary>
    /// 升级服务。
    /// </summary>
    private readonly UpdateService _update;

    /// <summary>
    /// 是否有板卡在测试中（安装前实时查询主壳）。
    /// </summary>
    private readonly Func<bool> _anyBoardBusy;

    /// <summary>
    /// 下载取消令牌（窗口关闭时取消）。
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// 下载进度（0~1）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double _progress;

    /// <summary>
    /// 是否正在下载。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionText))]
    private bool _isDownloading;

    /// <summary>
    /// 错误信息（空 = 无错）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";

    /// <summary>
    /// 测试中被拦截提示是否显示。
    /// </summary>
    [ObservableProperty]
    private bool _isBlockedByTest;

    /// <summary>
    /// 构造。
    /// </summary>
    /// <param name="update">升级服务。</param>
    /// <param name="anyBoardBusy">查询是否有板卡在测试中。</param>
    public UpdateViewModel(UpdateService update, Func<bool> anyBoardBusy)
    {
        _update = update;
        _anyBoardBusy = anyBoardBusy;
        IsBlockedByTest = anyBoardBusy();
    }

    /// <summary>当前版本文本。</summary>
    public string CurrentVersionText => $"v{_update.CurrentVersion.ToString(3)}";

    /// <summary>新版本文本。</summary>
    public string NewVersionText => $"v{_update.Latest?.Version}";

    /// <summary>包大小文本。</summary>
    public string SizeText => _update.Latest is { Size: > 0 } m ? $"{m.Size / 1024.0 / 1024.0:F1} MB" : "";

    /// <summary>更新说明。</summary>
    public string Notes => _update.Latest?.Notes ?? "";

    /// <summary>是否有更新说明。</summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    /// <summary>是否强制更新（决定「暂不」是否可用、窗口能否关闭）。</summary>
    public bool IsMandatory => _update.IsMandatory;

    /// <summary>非强更才允许关闭/暂不。</summary>
    public bool CanDismiss => !IsMandatory;

    /// <summary>是否有错误。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>主按钮文字。</summary>
    public string ActionText => IsDownloading ? "下载中…" : "立即更新";

    /// <summary>进度行文字。</summary>
    public string ProgressText => Progress >= 1 ? "校验并准备安装 …" : "正在下载升级包 …";

    /// <summary>
    /// 立即更新：测试中拦截 → 下载+校验 → 拉起升级器并退出主程序。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateNow))]
    private async Task UpdateNowAsync()
    {
        if (_anyBoardBusy())
        {
            IsBlockedByTest = true;
            return;
        }

        IsBlockedByTest = false;
        Error = "";
        IsDownloading = true;
        UpdateNowCommand.NotifyCanExecuteChanged();
        try
        {
            var zip = await _update.DownloadAsync(new Progress<double>(p => Progress = p), _cts.Token);
            _update.InstallAndRestart(zip);
        }
        catch (OperationCanceledException)
        {
            // 窗口被关闭，静默
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsDownloading = false;
            UpdateNowCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 下载中禁止重复点击。
    /// </summary>
    /// <returns>是否可执行。</returns>
    private bool CanUpdateNow() => !IsDownloading;

    /// <summary>
    /// 窗口关闭时取消下载。
    /// </summary>
    public void CancelPending() => _cts.Cancel();
}
