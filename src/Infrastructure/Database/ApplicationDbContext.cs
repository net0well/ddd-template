using System.Diagnostics;
using Domain.Todos;
using Domain.Users;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Events;
using SharedKernel.Notifications;
using SharedKernel.Primitives;
using SharedKernel.Time;

namespace Infrastructure.Database;

internal sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    DomainEventSerializer domainEventSerializer,
    IDateTimeProvider dateTimeProvider)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<OutboxMessageConsumer> OutboxMessageConsumers => Set<OutboxMessageConsumer>();

    /// <summary>
    /// Saves the changes and, in the same transaction, the domain events they raised as outbox messages.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureNoInvalidAggregateIsSaved();
        AddDomainEventsToOutbox();

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schemas.Default);

        // Notifications are transient validation state, never persisted.
        modelBuilder.Ignore<Notification>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        MapAuditStamps(modelBuilder);
    }

    // Get-only on the entity (the domain never sets them), so they are mapped here once for every IAuditable.
    private static void MapAuditStamps(ModelBuilder modelBuilder)
    {
        foreach (Type auditable in modelBuilder.Model.GetEntityTypes()
                     .Select(entityType => entityType.ClrType)
                     .Where(type => type.IsAssignableTo(typeof(IAuditable))))
        {
            EntityTypeBuilder entity = modelBuilder.Entity(auditable);

            entity.Property(nameof(IAuditable.CreatedOnUtc));
            entity.Property(nameof(IAuditable.CreatedBy));
            entity.Property(nameof(IAuditable.ModifiedOnUtc));
            entity.Property(nameof(IAuditable.ModifiedBy));
        }
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

    private void AddDomainEventsToOutbox()
    {
        var domainEvents = new List<IDomainEvent>();

        foreach (AggregateRoot aggregate in ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity))
        {
            domainEvents.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }

        if (domainEvents.Count == 0)
        {
            return;
        }

        DateTime utcNow = dateTimeProvider.UtcNow;
        string? traceParent = Activity.Current?.Id;

        OutboxMessages.AddRange(domainEvents.Select(domainEvent =>
        {
            (string type, string content) = domainEventSerializer.Serialize(domainEvent);

            return new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                Type = type,
                Content = content,
                OccurredOnUtc = utcNow,
                TraceParent = traceParent
            };
        }));
    }
}
