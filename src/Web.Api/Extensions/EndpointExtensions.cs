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
        builder
            .AddEndpointFilter<IdempotencyFilter>()
            .WithMetadata(new IdempotentEndpointMetadata());

    /// <summary>Documents the success response (envelope with <typeparamref name="TData"/> in <c>data</c>).</summary>
    public static RouteHandlerBuilder ProducesCommandResult<TData>(
        this RouteHandlerBuilder builder,
        int statusCode = StatusCodes.Status200OK) =>
        builder.Produces<CommandResult<TData>>(statusCode);

    /// <summary>Documents the success response of an operation without a value.</summary>
    public static RouteHandlerBuilder ProducesCommandResult(this RouteHandlerBuilder builder) =>
        builder.Produces<CommandResult>(StatusCodes.Status200OK);

    /// <summary>
    /// Documents the failures specific to the endpoint. 401/403, 409 (idempotency), 429 and 500 are added
    /// automatically from the endpoint's metadata.
    /// </summary>
    public static RouteHandlerBuilder ProducesErrors(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (int statusCode in statusCodes)
        {
            builder.Produces<CommandResult>(statusCode);
        }

        return builder;
    }
}
