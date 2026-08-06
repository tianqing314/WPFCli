using Microsoft.Extensions.DependencyInjection;

namespace TESTRIG.UI.Shared.BoardExtras;

/// <summary>
/// 板卡专属工具栏扩展的 DI 注册。
/// </summary>
public static class BoardExtrasServiceCollectionExtensions
{
    /// <summary>
    /// 注册全部板卡专属工具栏扩展的提供者。测试运行页注入 <c>IEnumerable&lt;IBoardToolbarExtraProvider&gt;</c>，
    /// 取第一个 <c>Supports(manifest)</c> 为真的；没有匹配的板就不显示扩展位，工具栏与原来完全一致。
    /// <para>新增板卡扩展 = 在此加一行注册 + 在 <c>BoardExtrasTemplates.xaml</c> 加一条 DataTemplate。</para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddPcbaBoardExtras(this IServiceCollection services)
    {
        // 模板仅保留 TemplateUUT（TemplateUUT），无板卡专属工具栏扩展。新增板卡扩展时在此注册提供者。
        return services;
    }
}
