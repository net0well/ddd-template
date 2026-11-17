using Domain.Todos;
using Domain.Users;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Events;
using SharedKernel.Notifications;
using SharedKernel.Primitives;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options)
{
    // Events saved inside an explicit transaction wait here until the commit.
    // Dispatch never uses the request's token: once the data is committed, a client that disconnects must not
    // cancel its side effects.
    private readonly List<IDomainEvent> _deferredDomainEvents = [];

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureNoInvalidAggregateIsSaved();

        List<IDomainEvent> domainEvents = ExtractDomainEvents();

        int result = await base.SaveChangesAsync(cancellationToken);

        if (Database.CurrentTransaction is null)
        {
            await domainEventsDispatcher.DispatchAsync(domainEvents, CancellationToken.None);
        }
        else
        {
            _deferredDomainEvents.AddRange(domainEvents);
        }

        return result;
    }

    internal async Task DispatchDeferredDomainEventsAsync()
    {
        IDomainEvent[] domainEvents = [.. _deferredDomainEvents];

        _deferredDomainEvents.Clear();

        await domainEventsDispatcher.DispatchAsync(domainEvents, CancellationToken.None);
    }

    internal void DiscardDeferredDomainEvents() => _deferredDomainEvents.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schemas.Default);

        // Notifications are transient validation state, never persisted.
        modelBuilder.Ignore<Notification>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    // Safety net of the Notification pattern: a handler that forgot to check IsValid fails here
    // instead of persisting an inconsistent aggregate.
    private void EnsureNoInvalidAggregateIsSaved()
    {
        string[] invalid = [.. ChangeTracker.Entries<Entity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified && !entry.Entity.IsValid)
            .Select(entry => $"{entry.Entity.GetType().Name} {entry.Entity.Id}: {string.Join("; ", entry.Entity.Notifications)}")];

        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to save invalid aggregates (check IsValid before saving): {string.Join(" | ", invalid)}");
        }
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        var domainEvents = new List<IDomainEvent>();

        foreach (AggregateRoot aggregate in ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity))
        {
            domainEvents.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }

        return domainEvents;
    }
}
