using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 手册目录项（Docs/ 下的一个 markdown 文件）。
/// </summary>
public sealed class ManualDocItem
{
    /// <summary>
    /// 显示名（默认取文件名去扩展名，可被入口覆盖，如 README → “系统介绍”）。
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// 相对应用目录的路径（等宽字显示在工具条）。
    /// </summary>
    public string RelativePath { get; init; } = "";
}

/// <summary>
/// 当前手册的一个正文标题锚点。<see cref="Level"/> 为 2（"## " 一级导航）或 3（"### " 二级导航）。
/// </summary>
public sealed class ManualHeading
{
    /// <summary>
    /// 标题文字（去掉 markdown 井号前缀）。
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// 标题层级：2 = "## "，3 = "### "。用于左侧导航缩进区分。
    /// </summary>
    public int Level { get; init; } = 2;
}

/// <summary>
/// 手册预览视图模型（对应设计稿 07）：左侧目录列出 Docs/ 下全部手册 + 当前篇的标题锚点，
/// 右侧 markdown 预览（仅 admin 打开）。
/// </summary>
public partial class ManualViewModel : ObservableObject
{
    /// <summary>
    /// 窗口标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Docs/ 下的手册文件列表。
    /// </summary>
    public ObservableCollection<ManualDocItem> Docs { get; } = [];

    /// <summary>
    /// 当前手册的标题锚点（"## " 一级 + "### " 二级）。
    /// </summary>
    public ObservableCollection<ManualHeading> Headings { get; } = [];

    /// <summary>
    /// 当前选中的手册。
    /// </summary>
    [ObservableProperty] private ManualDocItem? _selectedDoc;

    /// <summary>
    /// 手册正文（markdown 原文）。
    /// </summary>
    [ObservableProperty] private string _content = "";

    /// <summary>
    /// 是否正在渲染正文（首次打开大文档 markdown 渲染较慢，期间显示加载缓冲层）。
    /// </summary>
    [ObservableProperty] private bool _isLoading = true;

    /// <summary>
    /// 扫描 Docs/ 组装目录，并默认选中指定手册（找不到则选第一篇）。
    /// </summary>
    /// <param name="title">窗口标题。</param>
    /// <param name="relativePath">默认选中的手册相对路径，如 "Docs/新增测试项目指导手册.md"。</param>
    public ManualViewModel(string title, string relativePath)
    {
        Title = title;
        try
        {
            var docsDir = Path.Combine(AppContext.BaseDirectory, "Docs");
            if (Directory.Exists(docsDir))
            {
                foreach (var f in Directory.EnumerateFiles(docsDir, "*.md").OrderBy(f => f))
                {
                    Docs.Add(new ManualDocItem
                    {
                        Title = Path.GetFileNameWithoutExtension(f),
                        RelativePath = Path.Combine("Docs", Path.GetFileName(f)),
                    });
                }
            }

            SelectedDoc = Docs.FirstOrDefault(d => string.Equals(d.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                          ?? Docs.FirstOrDefault();
            // 默认篇的目录显示名跟随窗口标题（README 文件名不变，但列表显示“系统介绍”）。
            if (SelectedDoc is not null)
            {
                SelectedDoc.Title = title;
            }
            if (SelectedDoc is null)
            {
                Content = $"未找到手册文件：{relativePath}";
            }
        }
        catch (Exception ex)
        {
            Content = "加载手册失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 切换手册：重读正文并重建锚点列表。
    /// </summary>
    /// <param name="value">新选中的手册。</param>
    partial void OnSelectedDocChanged(ManualDocItem? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, value.RelativePath);
            Content = File.Exists(path) ? File.ReadAllText(path) : $"未找到手册文件：{value.RelativePath}";
        }
        catch (Exception ex)
        {
            Content = "加载手册失败：" + ex.Message;
        }

        Headings.Clear();
        foreach (var line in Content.Split('\n'))
        {
            var t = line.TrimEnd('\r');
            // 先判 "### " 再判 "## "：三级前缀不会被二级前缀误吞（第三个字符是 '#' 而非空格）。
            if (t.StartsWith("### ", StringComparison.Ordinal))
            {
                Headings.Add(new ManualHeading { Text = t[4..].Trim(), Level = 3 });
            }
            else if (t.StartsWith("## ", StringComparison.Ordinal))
            {
                Headings.Add(new ManualHeading { Text = t[3..].Trim(), Level = 2 });
            }
        }
    }

    /// <summary>
    /// 目录点击：切换手册。
    /// </summary>
    /// <param name="doc">目标手册。</param>
    [RelayCommand]
    private void SelectDoc(ManualDocItem? doc)
    {
        if (doc is not null)
        {
            SelectedDoc = doc;
        }
    }
}
