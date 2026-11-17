using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using SharedKernel.Results;
using Swashbuckle.AspNetCore.SwaggerGen;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.OpenApi;

/// <summary>
/// Documents what the API does for every endpoint without repeating it on each one: the bearer lock and
/// 401/403 on protected endpoints, the Idempotency-Key header, 429 on rate-limited endpoints, 500 everywhere,
/// and a meaningful description for each status code.
/// </summary>
internal sealed class ApiConventionsOperationFilter : IOperationFilter
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        ["200"] = "Success. The result is in `data`.",
        ["201"] = "Created. The id of the new resource is in `data`.",
        ["400"] = "Invalid input (`Validation`) or a business rule was broken (`BusinessRule`). See `notifications`.",
        ["401"] = "Missing, invalid or expired access token — or invalid credentials.",
        ["403"] = "Authenticated, but not allowed to perform this action.",
        ["404"] = "The resource does not exist or does not belong to the current user.",
        ["409"] = "Conflicts with the current state (duplicate, concurrent change or reused idempotency key).",
        ["429"] = "Too many requests. Retry after the number of seconds in the `Retry-After` header.",
        ["500"] = "Unexpected error. Use `traceId` to find the request in the logs."
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        IOpenApiSchema envelope = context.SchemaGenerator.GenerateSchema(typeof(CommandResult), context.SchemaRepository);

        operation.Responses ??= [];

        bool isProtected = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        if (isProtected)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SwaggerExtensions.BearerScheme, context.Document)] = []
                }
            ];

            AddResponse(operation, "401", envelope);

            if (metadata.OfType<IAuthorizeData>().Any(data => data.Policy is not null))
            {
                AddResponse(operation, "403", envelope);
            }
        }

        if (metadata.OfType<IdempotentEndpointMetadata>().Any())
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IdempotentEndpointMetadata.HeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional unique key (max 128 characters). A repeat with the same key gets 409; " +
                              "the key is released if the request fails, so it can be retried.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });

            AddResponse(operation, "409", envelope);
        }

        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            AddResponse(operation, "429", envelope);
        }

        AddResponse(operation, "500", envelope);

        foreach ((string statusCode, IOpenApiResponse response) in operation.Responses)
        {
            if (response is OpenApiResponse concrete && Descriptions.TryGetValue(statusCode, out string? description))
            {
                concrete.Description = description;
            }
        }
    }

    private static void AddResponse(OpenApiOperation operation, string statusCode, IOpenApiSchema schema) =>
        operation.Responses!.TryAdd(statusCode, new OpenApiResponse
        {
            Description = Descriptions[statusCode],
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = schema }
            }
        });
}
