using System.Reflection;
using Application.Abstractions.Messaging;
using Domain.Users;
using Infrastructure.Database;
using Jobs;
using SharedKernel.Results;

namespace ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly SharedKernelAssembly = typeof(CommandResult).Assembly;
    protected static readonly Assembly DomainAssembly = typeof(User).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    protected static readonly Assembly JobsAssembly = typeof(JobsOptions).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(Program).Assembly;
}
