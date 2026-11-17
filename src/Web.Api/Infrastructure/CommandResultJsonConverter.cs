using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharedKernel.Results;

namespace Web.Api.Infrastructure;

/// <summary>
/// Writes every <see cref="CommandResult"/> with the same shape: <c>{ success, data, notifications, traceId }</c>.
/// <c>data</c> is <c>null</c> on failure and for commands without a value — without this, a failed
/// <c>CommandResult&lt;Guid&gt;</c> would send <c>"00000000-…"</c>.
/// </summary>
internal sealed class CommandResultJsonConverter : JsonConverter<CommandResult>
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> DataProperties = new();

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsAssignableTo(typeof(CommandResult));

    public override CommandResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("CommandResult is only written to responses.");

    public override void Write(Utf8JsonWriter writer, CommandResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteBoolean(Name(nameof(CommandResult.Success), options), value.Success);

        writer.WritePropertyName(Name("Data", options));
        PropertyInfo? dataProperty = DataProperties.GetOrAdd(value.GetType(), type => type.GetProperty("Data"));

        if (value.Success && dataProperty?.GetValue(value) is { } data)
        {
            JsonSerializer.Serialize(writer, data, dataProperty.PropertyType, options);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName(Name(nameof(CommandResult.Notifications), options));
        JsonSerializer.Serialize(writer, value.Notifications, options);

        writer.WriteString(Name(nameof(CommandResult.TraceId), options), value.TraceId);

        writer.WriteEndObject();
    }

    private static string Name(string propertyName, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
