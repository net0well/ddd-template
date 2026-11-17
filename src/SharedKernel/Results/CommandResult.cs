using System.Collections.ObjectModel;
using System.Diagnostics;
using SharedKernel.Notifications;

namespace SharedKernel.Results;

/// <summary>
/// Outcome of a use case: success without notifications, or failure with every notification that explains it.
/// </summary>
/// <remarks>
/// Immutable. A failure always has at least one notification, and <see cref="Success"/> is derived from them,
/// so the two states cannot be mixed. The API returns it as <c>{ "success", "data", "notifications", "traceId" }</c>.
/// </remarks>
public class CommandResult
{
    protected CommandResult(IReadOnlyCollection<Notification> notifications)
    {
        Notifications = notifications;
        TraceId = CurrentTraceId();
    }

    public bool Success => Notifications.Count == 0;

    public IReadOnlyCollection<Notification> Notifications { get; }

    /// <summary>
    /// Trace of the request (or job) that produced the result — the <c>trace_id</c> of its logs in Loki and of
    /// its trace in Tempo. <see langword="null"/> outside a traced operation.
    /// </summary>
    public string? TraceId { get; }

    public static CommandResult Ok() => new(ReadOnlyCollection<Notification>.Empty);

    public static CommandResult<TData> Ok<TData>(TData data) =>
        new(data, ReadOnlyCollection<Notification>.Empty);

    public static CommandResult Fail(Notification notification) =>
        new(Freeze([notification]));

    public static CommandResult Fail(IEnumerable<Notification> notifications) =>
        new(Freeze(notifications));

    public static CommandResult Fail(Notifiable notifiable)
    {
        ArgumentNullException.ThrowIfNull(notifiable);

        return Fail(notifiable.Notifications);
    }

    public static CommandResult<TData> Fail<TData>(Notification notification) =>
        new(default, Freeze([notification]));

    public static CommandResult<TData> Fail<TData>(IEnumerable<Notification> notifications) =>
        new(default, Freeze(notifications));

    public static CommandResult<TData> Fail<TData>(Notifiable notifiable)
    {
        ArgumentNullException.ThrowIfNull(notifiable);

        return Fail<TData>(notifiable.Notifications);
    }

    private static string? CurrentTraceId() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.TraceId.ToHexString() : null;

    // Copies the notifications so the caller cannot change the result afterwards.
    private static ReadOnlyCollection<Notification> Freeze(IEnumerable<Notification?> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        var copy = new List<Notification>();

        foreach (Notification? notification in notifications)
        {
            if (notification is null)
            {
                throw new ArgumentException("A notification cannot be null.", nameof(notifications));
            }

            copy.Add(notification);
        }

        if (copy.Count == 0)
        {
            throw new ArgumentException("A failed result needs at least one notification.", nameof(notifications));
        }

        return copy.AsReadOnly();
    }
}

/// <summary>
/// Outcome of a use case that returns <typeparamref name="TData"/>. <see cref="Data"/> is <see langword="default"/> on failure.
/// </summary>
public sealed class CommandResult<TData> : CommandResult
{
    internal CommandResult(TData? data, IReadOnlyCollection<Notification> notifications)
        : base(notifications)
    {
        Data = data;
    }

    public TData? Data { get; }
}
