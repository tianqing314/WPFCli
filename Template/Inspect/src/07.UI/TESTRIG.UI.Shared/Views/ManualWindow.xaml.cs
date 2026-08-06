using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 手册预览窗口（对应设计稿 07：左目录/锚点 + 右 markdown 预览，仅 admin 打开）。
/// </summary>
public partial class ManualWindow : ChromeWindow
{
    /// <summary>
    /// 注入手册 ViewModel 构造。
    /// </summary>
    /// <param name="vm">手册 ViewModel。</param>
    public ManualWindow(ManualViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => RenderMarkdown();
    }

    /// <summary>
    /// 正文变化（切换手册）时重新走一次“缓冲层 → 延迟渲染”流程。
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManualViewModel.Content) && IsLoaded)
        {
            RenderMarkdown();
        }
    }

    /// <summary>
    /// 先显示加载缓冲层并让其绘制，再在较低优先级触发耗时的 markdown 渲染，渲染完成后隐藏缓冲层。
    /// </summary>
    private void RenderMarkdown()
    {
        if (DataContext is not ManualViewModel vm)
        {
            return;
        }

        vm.IsLoading = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                MdViewer.Markdown = vm.Content;
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => vm.IsLoading = false));
            }));
    }

    /// <summary>
    /// 锚点点击：在渲染后的 FlowDocument 里找到对应标题段落并滚动到可视区（纯 UI 交互）。
    /// </summary>
    /// <param name="sender">锚点按钮。</param>
    /// <param name="e">事件参数。</param>
    private void Anchor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string heading } || MdViewer.Document is not { } doc)
        {
            return;
        }

        foreach (var block in doc.Blocks)
        {
            if (block is Paragraph p && new TextRange(p.ContentStart, p.ContentEnd).Text.Trim() == heading)
            {
                p.BringIntoView();
                return;
            }
        }
    }
}
