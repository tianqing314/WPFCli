namespace TESTRIG.Infrastructure.Notifications;

/// <summary>
/// 全局通知总线：任意组件 Notify，状态栏订阅显示。取代旧零散的 SendInfoOrWarning。
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 通知事件（状态栏订阅）。
    /// </summary>
    event Action<string>? Notified;

    /// <summary>
    /// 推送一条通知。
    /// </summary>
    /// <param name="message">通知内容。</param>
    void Notify(string message);
}

/// <summary>
/// 通知总线默认实现（进程内事件广播）。
/// </summary>
public sealed class NotificationService : INotificationService
{
    /// <summary>
    /// 通知事件（状态栏订阅）。
    /// </summary>
    public event Action<string>? Notified;

    /// <summary>
    /// 推送一条通知（触发 <see cref="Notified"/>）。
    /// </summary>
    /// <param name="message">通知内容。</param>
    public void Notify(string message)
    {
        Notified?.Invoke(message);
    }
}
