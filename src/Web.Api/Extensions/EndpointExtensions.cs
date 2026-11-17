using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Results;
using Web.Api.Endpoints;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] serviceDescriptors = [.. assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))];

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(
        this WebApplication app,
        RouteGroupBuilder? routeGroupBuilder = null)
    {
        IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }

    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder app, string permission) =>
        app.RequireAuthorization(permission);

    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<IdempotencyFilter>();

    /// <summary>Documents the response envelope in Swagger (success and failure).</summary>
    public static RouteHandlerBuilder ProducesCommandResult<TData>(this RouteHandlerBuilder builder) =>
        builder
            .Produces<CommandResult<TData>>(StatusCodes.Status200OK)
            .Produces<CommandResult>(StatusCodes.Status400BadRequest);

    /// <inheritdoc cref="ProducesCommandResult{TData}"/>
    public static RouteHandlerBuilder ProducesCommandResult(this RouteHandlerBuilder builder) =>
        builder
            .Produces<CommandResult>(StatusCodes.Status200OK)
            .Produces<CommandResult>(StatusCodes.Status400BadRequest);
}
