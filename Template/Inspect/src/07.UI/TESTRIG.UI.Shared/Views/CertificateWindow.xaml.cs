using System.Windows;
using TESTRIG.UI.Shared.ViewModels;
using TESTRIG.UI.Shared.Views.Chrome;

namespace TESTRIG.UI.Shared.Views;

/// <summary>
/// 证书/合格证窗口（出厂检验模板专属）：选择通过的测试记录生成合格证并预览/打印。
/// </summary>
public partial class CertificateWindow : ChromeWindow
{
    /// <summary>
    /// 构造证书窗口。
    /// </summary>
    /// <param name="vm">证书视图模型。</param>
    public CertificateWindow(CertificateViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
