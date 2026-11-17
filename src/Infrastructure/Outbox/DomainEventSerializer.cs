using System.Text.Json;
using Domain.Users;
using SharedKernel.DependencyInjection;
using SharedKernel.Events;

namespace Infrastructure.Outbox;

/// <summary>
/// Serializes domain events for the outbox. Types are resolved from a registry of the Domain assembly, never
/// from arbitrary type names stored in the database.
/// </summary>
internal sealed class DomainEventSerializer : IInjectSingleton
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Type> _eventTypes = typeof(User).Assembly
        .GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IDomainEvent)))
        .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public (string Type, string Content) Serialize(IDomainEvent domainEvent)
    {
        Type type = domainEvent.GetType();

        return (type.FullName!, JsonSerializer.Serialize(domainEvent, type, Options));
    }

    /// <returns><see langword="null"/> when the type is unknown (renamed or removed event).</returns>
    /// <exception cref="JsonException">The content does not match the type.</exception>
    public IDomainEvent? Deserialize(string type, string content) =>
        _eventTypes.TryGetValue(type, out Type? eventType)
            ? (IDomainEvent?)JsonSerializer.Deserialize(content, eventType, Options)
            : null;
}
