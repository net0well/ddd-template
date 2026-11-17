using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SharedKernel.DependencyInjection;
using SharedKernel.Results;

namespace Infrastructure.Database;

internal sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork, IInjectScoped
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new DataConflictException("The data was changed by another request.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DataConflictException("The data conflicts with an existing record.", exception);
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
        where TResult : CommandResult
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // Going through the execution strategy keeps this correct if connection retries are enabled.
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

        TResult result;

        try
        {
            result = await strategy.ExecuteAsync(
                async token =>
                {
                    await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(token);

                    TResult operationResult = await operation(token);

                    if (operationResult.Success)
                    {
                        await transaction.CommitAsync(token);
                    }
                    else
                    {
                        await transaction.RollbackAsync(token);

                        // Entities saved inside the rolled-back transaction would look persisted otherwise.
                        context.ChangeTracker.Clear();
                    }

                    return operationResult;
                },
                cancellationToken);
        }
        catch
        {
            context.DiscardDeferredDomainEvents();
            context.ChangeTracker.Clear();
            throw;
        }

        if (result.Success)
        {
            await context.DispatchDeferredDomainEventsAsync();
        }
        else
        {
            context.DiscardDeferredDomainEvents();
        }

        return result;
    }
}
