namespace SharedKernel.Notifications;

/// <summary>
/// A problem reported by a domain object, a validator or a use case.
/// </summary>
/// <remarks>
/// <see cref="Key"/> is <c>"{FeaturePlural}.{Reason}"</c> for domain rules (e.g. <c>"TodoItems.NotFound"</c>)
/// and the property name for input validation (e.g. <c>"Description"</c>).
/// </remarks>
public sealed record Notification
{
    public Notification(string key, string message, NotificationType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Key = key;
        Message = message;
        Type = type;
    }

    public string Key { get; }

    public string Message { get; }

    public NotificationType Type { get; }

    public static Notification Validation(string key, string message) =>
        new(key, message, NotificationType.Validation);

    public static Notification BusinessRule(string key, string message) =>
        new(key, message, NotificationType.BusinessRule);

    public static Notification NotFound(string key, string message) =>
        new(key, message, NotificationType.NotFound);

    public static Notification Conflict(string key, string message) =>
        new(key, message, NotificationType.Conflict);

    public static Notification Unauthorized(string key, string message) =>
        new(key, message, NotificationType.Unauthorized);

    public static Notification Forbidden(string key, string message) =>
        new(key, message, NotificationType.Forbidden);

    public static Notification Failure(string key, string message) =>
        new(key, message, NotificationType.Failure);

    public override string ToString() => $"{Key}: {Message}";
}
