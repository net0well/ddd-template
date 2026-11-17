using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.DependencyInjection;
using SharedKernel.Persistence;

namespace Application.Abstractions.DependencyInjection;

public static class InjectableServiceCollectionExtensions
{
    // Classification-only interfaces are not registered as services.
    private static readonly HashSet<Type> MarkerInterfaces =
    [
        typeof(IInjectScoped),
        typeof(IInjectTransient),
        typeof(IInjectSingleton),
        typeof(ISearchableRepository),
        typeof(IWritableRepository),
        typeof(IDisposable),
        typeof(IAsyncDisposable)
    ];

    /// <summary>
    /// Registers every class marked with <see cref="IInjectScoped"/>, <see cref="IInjectTransient"/> or
    /// <see cref="IInjectSingleton"/> as itself and its interfaces (same instance per lifetime).
    /// Framework extension points (hosted services, authorization handlers…) stay explicit.
    /// </summary>
    public static IServiceCollection AddInjectableServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo<IInjectScoped>(), publicOnly: false)
                .AsSelfWithInterfaces(IsServiceInterface)
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<IInjectTransient>(), publicOnly: false)
                .AsSelfWithInterfaces(IsServiceInterface)
                .WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IInjectSingleton>(), publicOnly: false)
                .AsSelfWithInterfaces(IsServiceInterface)
                .WithSingletonLifetime());

        return services;
    }

    private static bool IsServiceInterface(Type type) => !MarkerInterfaces.Contains(type);
}
