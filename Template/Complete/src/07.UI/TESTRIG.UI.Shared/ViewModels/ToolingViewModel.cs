using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 工装/治具台账条目：名称/类型/SN/状态/校准到期/备注。
/// </summary>
public sealed partial class ToolingItemViewModel : ObservableObject
{
    /// <summary>
    /// 工装名称。
    /// </summary>
    [ObservableProperty] private string _name = "";

    /// <summary>
    /// 工装类型（如 测试治具/针床/标准表/高温炉）。
    /// </summary>
    [ObservableProperty] private string _type = "";

    /// <summary>
    /// 工装序列号。
    /// </summary>
    [ObservableProperty] private string _serialNo = "";

    /// <summary>
    /// 状态（在用/维修/停用）。
    /// </summary>
    [ObservableProperty] private string _status = "在用";

    /// <summary>
    /// 校准到期日（yyyy-MM-dd，空 = 不要求校准）。
    /// </summary>
    [ObservableProperty] private string _calibrationDue = "";

    /// <summary>
    /// 备注。
    /// </summary>
    [ObservableProperty] private string _remark = "";
}

/// <summary>
/// 工装/治具管理（组件模板专属）：台账的查看/增删/保存，数据持久化到 <c>AppContext.BaseDirectory/tooling.json</c>。
/// </summary>
public sealed partial class ToolingViewModel : ObservableObject
{
    /// <summary>
    /// 台账持久化文件。
    /// </summary>
    private static readonly string StorePath = Path.Combine(AppContext.BaseDirectory, "tooling.json");

    /// <summary>
    /// 工装/治具列表。
    /// </summary>
    public ObservableCollection<ToolingItemViewModel> Items { get; } = [];

    /// <summary>
    /// 状态栏文字。
    /// </summary>
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// 构造工装管理视图模型：加载已有台账。
    /// </summary>
    public ToolingViewModel()
    {
        Load();
        Status = $"工装台账：共 {Items.Count} 条（{StorePath}）";
    }

    /// <summary>
    /// 从磁盘加载台账（不存在则空表）。
    /// </summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return;
            }
            var list = JsonSerializer.Deserialize<List<ToolingItemViewModel>>(File.ReadAllText(StorePath));
            if (list is null)
            {
                return;
            }
            foreach (var item in list)
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Status = $"加载工装台账失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 保存台账到磁盘。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
            Status = $"已保存：共 {Items.Count} 条（{StorePath}）";
        }
        catch (Exception ex)
        {
            Status = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 新增一行工装。
    /// </summary>
    [RelayCommand]
    private void Add()
    {
        Items.Add(new ToolingItemViewModel { Name = "新工装", Status = "在用" });
        Status = "已新增一行，填写后点「保存」";
    }

    /// <summary>
    /// 删除选中工装。
    /// </summary>
    [RelayCommand]
    private void Remove(ToolingItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }
        Items.Remove(item);
        Status = $"已删除 {item.Name}（点「保存」生效）";
    }
}
