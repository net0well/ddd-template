using Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel.DependencyInjection;
using SharedKernel.Primitives;
using SharedKernel.Time;

namespace Infrastructure.Database;

/// <summary>Stamps <see cref="IAuditable"/> entities on save. Creation values can never be overwritten.</summary>
internal sealed class AuditingInterceptor(IDateTimeProvider dateTimeProvider, IUserContext userContext)
    : SaveChangesInterceptor, IInjectScoped
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = dateTimeProvider.UtcNow;
        Guid? userId = userContext.TryGetUserId(out Guid id) ? id : null;

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedOnUtc)).CurrentValue = utcNow;
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.CreatedOnUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    entry.Property(nameof(IAuditable.ModifiedOnUtc)).CurrentValue = utcNow;
                    entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = userId;
                    break;
            }
        }
    }
}
