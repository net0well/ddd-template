namespace SharedKernel.Notifications;

/// <summary>
/// Kind of problem a <see cref="Notification"/> reports. The API maps it to the HTTP status.
/// </summary>
public enum NotificationType
{
    /// <summary>Malformed input (400).</summary>
    Validation = 0,

    /// <summary>Valid input that a domain rule rejects (400).</summary>
    BusinessRule = 1,

    /// <summary>The resource does not exist or is not visible to the caller (404).</summary>
    NotFound = 2,

    /// <summary>Clashes with the current state: duplicate, concurrent change (409).</summary>
    Conflict = 3,

    /// <summary>Not authenticated or invalid credentials (401).</summary>
    Unauthorized = 4,

    /// <summary>Authenticated but not allowed (403).</summary>
    Forbidden = 5,

    /// <summary>Unexpected error, not caused by the caller (500).</summary>
    Failure = 6
}
