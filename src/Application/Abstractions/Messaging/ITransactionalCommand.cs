namespace Application.Abstractions.Messaging;

/// <summary>
/// Runs the command's handler inside one transaction (see <c>IUnitOfWork.ExecuteInTransactionAsync</c>).
/// </summary>
/// <remarks>
/// One <c>SaveChangesAsync</c> is already atomic. Use this when the handler saves more than once, mixes bulk
/// statements with tracked changes, or creates several aggregates that must exist together.
/// </remarks>
public interface ITransactionalCommand;
