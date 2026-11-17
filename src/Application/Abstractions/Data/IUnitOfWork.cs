using SharedKernel.Results;

namespace Application.Abstractions.Data;

/// <summary>
/// Transaction boundary of a use case. Repositories never save; the handler commits everything at once.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all tracked changes atomically. Refuses to persist an invalid aggregate.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction committed only if it returns a successful result.
    /// Domain events are dispatched after the commit. Nested calls join the open transaction.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
        where TResult : CommandResult;
}
