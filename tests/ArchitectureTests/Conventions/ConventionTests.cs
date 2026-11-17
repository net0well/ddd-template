using System.Reflection;
using Application.Abstractions.Messaging;
using NetArchTest.Rules;
using SharedKernel.DependencyInjection;
using SharedKernel.Persistence;
using SharedKernel.Primitives;
using Shouldly;

using TestResult = NetArchTest.Rules.TestResult;

namespace ArchitectureTests.Conventions;

public class ConventionTests : BaseTest
{
    private static readonly Type[] InjectionMarkers = [typeof(IInjectScoped), typeof(IInjectTransient), typeof(IInjectSingleton)];

    [Fact]
    public void RepositoryInterfaces_Should_LiveInDomain_AndFollowTheNamingConvention()
    {
        Type[] searchable = [.. DomainAssembly.GetTypes().Where(t => t.IsInterface && t.IsAssignableTo(typeof(ISearchableRepository)))];
        Type[] writable = [.. DomainAssembly.GetTypes().Where(t => t.IsInterface && t.IsAssignableTo(typeof(IWritableRepository)))];

        searchable.ShouldNotBeEmpty();
        writable.ShouldNotBeEmpty();
        searchable.ShouldAllBe(t => t.Name.StartsWith('I') && t.Name.EndsWith("SearchableRepository", StringComparison.Ordinal));
        writable.ShouldAllBe(t => t.Name.StartsWith('I') && t.Name.EndsWith("WritableRepository", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryImplementations_Should_BeInternalSealed_InInfrastructure()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .ImplementInterface(typeof(ISearchableRepository))
            .Or()
            .ImplementInterface(typeof(IWritableRepository))
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Handlers_Should_BeInternalSealed()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Or()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void InjectableClasses_Should_DeclareASingleLifetime()
    {
        Type[] conflicting = [.. AllAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && InjectionMarkers.Count(marker => type.IsAssignableTo(marker)) > 1)];

        conflicting.ShouldBeEmpty();
    }

    [Fact]
    public void Aggregates_Should_NotExposePublicSetters()
    {
        string[] publicSetters = [.. DomainAssembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(Entity)) && !type.IsAbstract)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")];

        publicSetters.ShouldBeEmpty();
    }

    private static Assembly[] AllAssemblies() =>
        [SharedKernelAssembly, DomainAssembly, ApplicationAssembly, InfrastructureAssembly, JobsAssembly, PresentationAssembly];
}
