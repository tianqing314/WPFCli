using TESTRIG.Core.Abstractions;

namespace TESTRIG.UI.Shared.BoardExtras;

/// <summary>
/// **板卡专属工具栏扩展**的视图模型契约。
/// <para>
/// 存在的原因：个别板卡在测试运行页需要一两个只对它有意义的操作（如 PS02/A20 的「烧录板类型：自动识别 / 人工指定」）。
/// 这类东西写进 <c>TestRunViewModel</c> / <c>TestRunView.xaml</c> 会把**框架级**的页面越堆越厚，且每加一块板就要改一次
/// 公共文件。改为：测试运行页只留一个扩展位（<c>ContentControl</c>），具体内容由本接口的实现提供，
/// **框架侧不认识任何一块具体的板**。
/// </para>
/// <para>
/// 新增一块板的专属工具栏 = 加一个 VM（实现本接口）+ 一个 UserControl + 一个 <see cref="IBoardToolbarExtraProvider"/>，
/// 再在 <c>BoardExtrasTemplates.xaml</c> 里加一条 VM→View 的 <c>DataTemplate</c>；<c>TestRunViewModel</c> 一行不用改。
/// </para>
/// </summary>
public interface IBoardToolbarExtra
{
    /// <summary>
    /// 测试是否正在进行——由测试运行页在运行状态变化时写入，扩展据此禁用自身控件
    /// （运行中途改设置会让同一盘板前后用上不同参数）。
    /// </summary>
    bool IsBusy { get; set; }

    /// <summary>
    /// 每轮测试开始时要推到状态栏的提醒；无需提醒返回 <c>null</c>。
    /// 用于「设置跨轮保持」的防呆（如 A20 人工指定了烧录板类型，换料后忘记切回来）。
    /// </summary>
    string? RunStartNotice { get; }
}

/// <summary>
/// 板卡专属工具栏扩展的**提供者**：判断某套针床要不要这个扩展、要的话造一个出来。
/// 实现类与它服务的板卡放在一起，由 DI 注册；
/// 测试运行页拿到的是 <c>IEnumerable&lt;IBoardToolbarExtraProvider&gt;</c>，取**第一个** <see cref="Supports"/> 为真的。
/// </summary>
public interface IBoardToolbarExtraProvider
{
    /// <summary>
    /// 本提供者是否服务于该套针床（一般按 <see cref="JigManifest.Key"/> 判断）。
    /// </summary>
    /// <param name="manifest">当前针床清单。</param>
    /// <returns>是则返回 true。</returns>
    bool Supports(JigManifest manifest);

    /// <summary>
    /// 为该套针床创建工具栏扩展视图模型（仅在 <see cref="Supports"/> 为真时调用）。
    /// </summary>
    /// <param name="manifest">当前针床清单。</param>
    /// <returns>扩展视图模型。</returns>
    IBoardToolbarExtra Create(JigManifest manifest);
}
