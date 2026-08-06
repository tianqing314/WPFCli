using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Infrastructure.Data;
using TESTRIG.UI.Shared.Services;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 证书/合格证（出厂检验模板专属）：列出本型号通过的测试记录，选中后生成合格证 HTML 并打开预览/打印。
/// </summary>
public sealed partial class CertificateViewModel : ObservableObject
{
    /// <summary>
    /// 测试结果仓储。
    /// </summary>
    private readonly ITestResultStore _store;

    /// <summary>
    /// 被检型号（只显示本型号的记录）。
    /// </summary>
    private readonly string _deviceModel;

    /// <summary>
    /// 证书输出目录。
    /// </summary>
    private static readonly string CertDir = Path.Combine(AppContext.BaseDirectory, "Certificates");

    /// <summary>
    /// 通过的测试记录列表。
    /// </summary>
    public ObservableCollection<CertificateRecordViewModel> Records { get; } = [];

    /// <summary>
    /// 状态栏文字。
    /// </summary>
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// 是否加载中。
    /// </summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// 构造证书视图模型。
    /// </summary>
    /// <param name="store">测试结果仓储。</param>
    /// <param name="deviceModel">被检型号。</param>
    public CertificateViewModel(ITestResultStore store, string deviceModel)
    {
        _store = store;
        _deviceModel = deviceModel;
    }

    /// <summary>
    /// 加载本型号已通过的测试记录。
    /// </summary>
    [RelayCommand]
    private Task Load() => LoadAsync();

    /// <summary>
    /// 加载本型号已通过的测试记录。
    /// </summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Records.Clear();
            var page = await _store.QueryMainAsync(new TestRecordFilter(IsPass: true, DeviceModel: _deviceModel), 1, 200);
            foreach (var r in page.Items)
            {
                Records.Add(new CertificateRecordViewModel(r));
            }
            Status = $"本型号通过的记录：{Records.Count} 条（仅通过记录可出证）";
        }
        catch (Exception ex)
        {
            Status = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 生成选中记录的合格证：渲染 HTML 保存到 Certificates 目录并用默认浏览器打开（可打印/另存 PDF）。
    /// </summary>
    [RelayCommand]
    private async Task Generate(CertificateRecordViewModel? item)
    {
        if (item is null)
        {
            Status = "请先在列表中选择一条记录";
            return;
        }

        IsBusy = true;
        try
        {
            var steps = await _store.GetStepsBySnAsync(item.Record.DeviceSn);
            var html = CertificateBuilder.Build(item.Record, steps);
            Directory.CreateDirectory(CertDir);
            var safeSn = string.Concat(item.Record.DeviceSn.Where(char.IsLetterOrDigit));
            var path = Path.Combine(CertDir, $"Cert_{safeSn}_{DateTime.Now:yyyyMMddHHmmss}.html");
            File.WriteAllText(path, html, System.Text.Encoding.UTF8);
            Status = $"合格证已生成：{path}";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// 证书列表一行记录。
/// </summary>
public sealed class CertificateRecordViewModel
{
    /// <summary>
    /// 原始主表记录。
    /// </summary>
    public MainTestRecord Record { get; }

    /// <summary>
    /// 构造列表行。
    /// </summary>
    /// <param name="record">主表记录。</param>
    public CertificateRecordViewModel(MainTestRecord record)
    {
        Record = record;
    }

    /// <summary>
    /// 序列号。
    /// </summary>
    public string DeviceSn => Record.DeviceSn;

    /// <summary>
    /// 批次号。
    /// </summary>
    public string BatchNo => Record.BatchNo ?? "-";

    /// <summary>
    /// 检验员。
    /// </summary>
    public string Operator => Record.Operator ?? "-";

    /// <summary>
    /// 检验时间。
    /// </summary>
    public string EndTime => Record.EndTime.ToString("yyyy-MM-dd HH:mm:ss");
}
