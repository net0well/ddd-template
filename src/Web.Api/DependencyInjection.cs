using System.Text.Json.Serialization;
using Web.Api.Infrastructure;

namespace Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // Enums travel as names ("High", "NotFound"); every CommandResult has the same envelope shape.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.Converters.Add(new CommandResultJsonConverter());
        });

        // Binding errors (malformed JSON, wrong types) throw in every environment, so the exception handler
        // can answer with the standard envelope instead of an empty 400.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Only because UseExceptionHandler() requires the service; responses use the CommandResult envelope.
        services.AddProblemDetails();

        return services;
    }
}
