namespace SharedKernel.Notifications;

/// <summary>
/// Base for objects that validate themselves and collect <see cref="Notification"/>s instead of throwing.
/// </summary>
/// <remarks>
/// A method that finds a broken rule adds a notification and returns without changing state; the caller
/// checks <see cref="IsValid"/>. Notifications are never persisted, and saving an invalid aggregate is
/// refused by the persistence layer. Programming errors still throw.
/// </remarks>
public abstract class Notifiable
{
    private readonly List<Notification> _notifications = [];

    public IReadOnlyCollection<Notification> Notifications => _notifications.AsReadOnly();

    public bool IsValid => _notifications.Count == 0;

    public void AddNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _notifications.Add(notification);
    }

    public void AddNotifications(IEnumerable<Notification> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        _notifications.AddRange(notifications);
    }

    /// <summary>Merges the notifications of composed objects (e.g. value objects).</summary>
    public void AddNotifications(params Notifiable[] notifiables)
    {
        ArgumentNullException.ThrowIfNull(notifiables);

        foreach (Notifiable notifiable in notifiables)
        {
            _notifications.AddRange(notifiable.Notifications);
        }
    }

    public void ClearNotifications() => _notifications.Clear();
}
