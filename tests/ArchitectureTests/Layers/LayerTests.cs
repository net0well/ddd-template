using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace ArchitectureTests.Layers;

public class LayerTests : BaseTest
{
    [Fact]
    public void SharedKernel_Should_NotDependOnAnyOtherLayer() =>
        ShouldNotDependOn(SharedKernelAssembly, "Domain", "Application", "Infrastructure", "Jobs", "Web.Api");

    [Fact]
    public void Domain_Should_OnlyDependOnSharedKernel() =>
        ShouldNotDependOn(DomainAssembly, "Application", "Infrastructure", "Jobs", "Web.Api", "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Application_Should_NotDependOnOuterLayersOrFrameworks() =>
        ShouldNotDependOn(
            ApplicationAssembly,
            "Infrastructure",
            "Jobs",
            "Web.Api",
            "Microsoft.EntityFrameworkCore",
            "Hangfire",
            "Serilog");

    [Fact]
    public void Infrastructure_Should_NotDependOnJobsOrPresentation() =>
        ShouldNotDependOn(InfrastructureAssembly, "Jobs", "Web.Api");

    [Fact]
    public void Jobs_Should_NotDependOnInfrastructureOrPresentation() =>
        ShouldNotDependOn(JobsAssembly, "Infrastructure", "Web.Api", "Microsoft.EntityFrameworkCore");

    // Program/DependencyInjection wire Infrastructure up; endpoints, filters, middleware and extensions must not
    // touch its types or EF Core.
    [Fact]
    public void Presentation_Should_UseInfrastructureOnlyForWiring()
    {
        TestResult result = Types.InAssembly(PresentationAssembly)
            .That()
            .ResideInNamespaceStartingWith("Web.Api.")
            .ShouldNot()
            .HaveDependencyOnAny("Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static void ShouldNotDependOn(Assembly assembly, params string[] namespaces)
    {
        TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(namespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
