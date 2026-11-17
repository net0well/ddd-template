using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Time;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// Common substitutes for handler tests. Repositories are mocked per test; the unit of work, the clock and
/// the current user are shared here.
/// </summary>
public abstract class BaseHandlerTest
{
    protected static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    protected BaseHandlerTest()
    {
        DateTimeProvider.UtcNow.Returns(UtcNow);
        UserContext.UserId.Returns(CurrentUserId);
    }

    protected Guid CurrentUserId { get; } = Guid.CreateVersion7();

    protected IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    protected IDateTimeProvider DateTimeProvider { get; } = Substitute.For<IDateTimeProvider>();

    protected IUserContext UserContext { get; } = Substitute.For<IUserContext>();

    protected static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
