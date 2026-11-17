using Microsoft.OpenApi;
using SharedKernel.Results;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Web.Api.OpenApi;

/// <summary>
/// Makes the envelope schema match what <c>CommandResultJsonConverter</c> writes: always
/// <c>success, data, notifications, traceId</c>, in this order, with <c>data</c> present (null on failure).
/// </summary>
internal sealed class CommandResultSchemaFilter : ISchemaFilter
{
    private static readonly string[] Order = ["success", "data", "notifications", "traceId"];

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsAssignableTo(typeof(CommandResult)) || schema is not OpenApiSchema envelope || envelope.Properties is null)
        {
            return;
        }

        if (!envelope.Properties.ContainsKey("data"))
        {
            envelope.Properties["data"] = new OpenApiSchema { Description = "Always null for operations without a value." };
        }

        var ordered = Order
            .Where(envelope.Properties.ContainsKey)
            .ToDictionary(name => name, name => envelope.Properties[name], StringComparer.Ordinal);

        envelope.Properties.Clear();

        foreach ((string name, IOpenApiSchema property) in ordered)
        {
            envelope.Properties[name] = property;
        }

        if (envelope.Properties["data"] is OpenApiSchema data)
        {
            data.Description ??= "The result on success; null on failure.";
        }

        if (envelope.Properties["traceId"] is OpenApiSchema traceId)
        {
            traceId.Description = "W3C trace id of the request — search it in Loki (`trace_id`) or Tempo.";
        }

        envelope.Required = new HashSet<string>(Order, StringComparer.Ordinal);
        envelope.Description = "Standard response envelope (Notification pattern).";
    }
}
