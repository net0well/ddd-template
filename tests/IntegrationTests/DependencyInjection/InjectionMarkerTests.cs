using System.Reflection;
using Application.Abstractions.Messaging;
using Domain.Users;
using Infrastructure.Database;
using Jobs;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.DependencyInjection;
using SharedKernel.Persistence;

namespace IntegrationTests.DependencyInjection;

/// <summary>
/// Resolves every class marked with an <c>IInject*</c> interface from the real container and checks that it
/// gets the lifetime its marker declares, and that its interfaces resolve to the same instance.
/// </summary>
public sealed class InjectionMarkerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(User).Assembly,
        typeof(ICommand).Assembly,
        typeof(ApplicationDbContext).Assembly,
        typeof(JobsOptions).Assembly
    ];

    private static readonly Type[] NonServiceInterfaces =
    [
        typeof(IInjectScoped), typeof(IInjectTransient), typeof(IInjectSingleton),
        typeof(ISearchableRepository), typeof(IWritableRepository), typeof(IDisposable), typeof(IAsyncDisposable)
    ];

    public static TheoryData<Type> MarkedClasses()
    {
        var data = new TheoryData<Type>();

        foreach (Type type in ApplicationAssemblies
                     .SelectMany(assembly => assembly.GetTypes())
                     .Where(type => type is { IsClass: true, IsAbstract: false } &&
                                    (type.IsAssignableTo(typeof(IInjectScoped)) ||
                                     type.IsAssignableTo(typeof(IInjectTransient)) ||
                                     type.IsAssignableTo(typeof(IInjectSingleton)))))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MarkedClasses))]
    public void MarkedClass_Should_ResolveWithTheDeclaredLifetime(Type type)
    {
        // Arrange
        using IServiceScope first = Factory.Services.CreateScope();
        using IServiceScope second = Factory.Services.CreateScope();

        // Act
        object a1 = first.ServiceProvider.GetRequiredService(type);
        object a2 = first.ServiceProvider.GetRequiredService(type);
        object b = second.ServiceProvider.GetRequiredService(type);

        // Assert
        if (type.IsAssignableTo(typeof(IInjectSingleton)))
        {
            a1.ShouldBeSameAs(a2);
            a1.ShouldBeSameAs(b);
        }
        else if (type.IsAssignableTo(typeof(IInjectScoped)))
        {
            a1.ShouldBeSameAs(a2);
            a1.ShouldNotBeSameAs(b);
        }
        else
        {
            a1.ShouldNotBeSameAs(a2);
        }
    }

    [Theory]
    [MemberData(nameof(MarkedClasses))]
    public void MarkedClass_Should_BeRegisteredForEachOfItsInterfaces(Type type)
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();
        Type[] serviceInterfaces = [.. type.GetInterfaces().Where(i => !NonServiceInterfaces.Contains(i))];

        // Act & Assert
        foreach (Type serviceInterface in serviceInterfaces)
        {
            object service = scope.ServiceProvider.GetRequiredService(serviceInterface);
            service.ShouldBeOfType(type);

            if (!type.IsAssignableTo(typeof(IInjectTransient)))
            {
                service.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService(type));
            }
        }
    }

    [Fact]
    public void MarkerInterfaces_Should_NotBeServices()
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();

        // Act & Assert
        scope.ServiceProvider.GetServices<IInjectScoped>().ShouldBeEmpty();
        scope.ServiceProvider.GetServices<ISearchableRepository>().ShouldBeEmpty();
        scope.ServiceProvider.GetServices<IWritableRepository>().ShouldBeEmpty();
    }
}
