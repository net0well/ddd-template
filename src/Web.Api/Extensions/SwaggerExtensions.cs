using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using Web.Api.OpenApi;

namespace Web.Api.Extensions;

internal static class SwaggerExtensions
{
    internal const string BearerScheme = "Bearer";

    private const string Description = """
        ### Authentication
        1. `POST /users/register`, then `POST /users/login` — the response has an `accessToken` and a `refreshToken`.
        2. Click **Authorize** and paste the `accessToken`.
        3. When it expires, `POST /users/refresh-token`: the refresh token is rotated (reusing an old one ends every session).

        ### Response envelope
        Every response — including errors — is `{ "success", "data", "notifications", "traceId" }`.
        The notification `type` decides the status code:

        | type | status |
        |---|---|
        | `Validation`, `BusinessRule` | 400 |
        | `Unauthorized` | 401 |
        | `Forbidden` | 403 |
        | `NotFound` | 404 |
        | `Conflict` | 409 |
        | `Failure` | 500 |

        ### Tracing
        `traceId` (and the `Correlation-Id` response header) identifies the request in the logs and traces:
        Grafana → Explore → Loki `{service_name="Web.Api"} | trace_id="<traceId>"`, or search it in Tempo.
        Send your own `Correlation-Id` header to propagate a correlation id from the caller.

        ### Limits
        A global rate limit applies per user (or IP); authentication endpoints have a stricter one. Exceeding it
        returns 429 with `Retry-After`.
        """;

    internal static IServiceCollection AddSwaggerWithAuth(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DDD Template API",
                Version = "v1",
                Description = Description
            });

            // Several slices have a class with the same name (TodoResponse, Request…); ids must be unique.
            options.CustomSchemaIds(SchemaId);

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token returned by `POST /users/login`."
            });

            options.OperationFilter<ApiConventionsOperationFilter>();
            options.SchemaFilter<CommandResultSchemaFilter>();
            options.DocumentFilter<TagDescriptionsDocumentFilter>();
        });

        return services;
    }

    internal static WebApplication UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "DDD Template API v1");
            options.DocumentTitle = "DDD Template API";
            options.DocExpansion(DocExpansion.List);
            options.DefaultModelsExpandDepth(0);
            options.DisplayOperationId();
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.EnablePersistAuthorization();
            options.EnableTryItOutByDefault();
        });

        return app;
    }

    // "CommandResultOfGuid", "GetByIdTodoResponse", "TodosCreateRequest"…
    private static string SchemaId(Type type)
    {
        string name = type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}Of{string.Concat(type.GetGenericArguments().Select(SchemaId))}"
            : type.Name;

        if (type.IsNested)
        {
            return SchemaId(type.DeclaringType!) + name;
        }

        string? ns = type.Namespace;

        bool isSharedType = ns is null ||
                            ns.StartsWith("System", StringComparison.Ordinal) ||
                            ns.StartsWith("SharedKernel", StringComparison.Ordinal) ||
                            ns.StartsWith("Domain", StringComparison.Ordinal);

        return isSharedType ? name : ns![(ns!.LastIndexOf('.') + 1)..] + name;
    }
}
