using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.UI.Shared.Services;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 单个测试项编辑器：编辑 Key/Kind/名称/描述 + 设置项 + 参数 + 判定条件。
/// 编辑的是主页面传入的工作副本，确认（<see cref="Confirmed"/>=true）后由主页面回写。
/// </summary>
public partial class StepEditorViewModel : ObservableObject
{
    /// <summary>
    /// 编辑中的测试项（工作副本）。
    /// </summary>
    public StepEditModel Step { get; }

    /// <summary>
    /// 可选 Kind（当前设备族下已注册处理器）。
    /// </summary>
    public IReadOnlyList<string> AvailableKinds { get; }

    /// <summary>
    /// 条件类型可选值。
    /// </summary>
    public IReadOnlyList<string> ConditionKinds { get; } = ["Range", "Value", "Text"];

    /// <summary>
    /// 是否点了确定。
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>
    /// 请求关闭窗口（参数=是否确认）。窗口订阅后设 DialogResult 并关闭。
    /// </summary>
    public event Action<bool>? CloseRequested;

    /// <summary>
    /// 构造测试项编辑器。
    /// </summary>
    /// <param name="step">工作副本。</param>
    /// <param name="availableKinds">可选 Kind。</param>
    public StepEditorViewModel(StepEditModel step, IReadOnlyList<string> availableKinds)
    {
        Step = step;
        AvailableKinds = availableKinds;
    }

    /// <summary>新增判定条件。</summary>
    [RelayCommand]
    private void AddCondition()
    {
        Step.Conditions.Add(new ConditionEditModel());
    }

    /// <summary>删除指定判定条件。</summary>
    /// <param name="c">要删除的条件。</param>
    [RelayCommand]
    private void DeleteCondition(ConditionEditModel? c)
    {
        if (c is not null) { Step.Conditions.Remove(c); }
    }

    /// <summary>新增参数。</summary>
    [RelayCommand]
    private void AddParameter()
    {
        Step.Parameters.Add(new ParameterEditModel());
    }

    /// <summary>删除指定参数。</summary>
    /// <param name="p">要删除的参数。</param>
    [RelayCommand]
    private void DeleteParameter(ParameterEditModel? p)
    {
        if (p is not null) { Step.Parameters.Remove(p); }
    }

    /// <summary>新增设置项。</summary>
    [RelayCommand]
    private void AddSetting()
    {
        Step.Settings.Add(new SettingEditModel());
    }

    /// <summary>删除指定设置项。</summary>
    /// <param name="s">要删除的设置项。</param>
    [RelayCommand]
    private void DeleteSetting(SettingEditModel? s)
    {
        if (s is not null) { Step.Settings.Remove(s); }
    }

    /// <summary>确定：校验必填后关闭。</summary>
    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Step.Key) || string.IsNullOrWhiteSpace(Step.Kind) || string.IsNullOrWhiteSpace(Step.Name))
        {
            AppDialog.Error("无法保存", "Key、Kind、名称都不能为空。");
            return;
        }

        Confirmed = true;
        CloseRequested?.Invoke(true);
    }

    /// <summary>取消：不回写。</summary>
    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke(false);
    }
}
