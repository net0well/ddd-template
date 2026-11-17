using Application.Abstractions.Messaging;
using FluentValidation;
using FluentValidation.Results;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Application.Abstractions.Behaviors;

/// <summary>
/// Runs the FluentValidation validators before the handler; each failure becomes a Validation notification
/// keyed by the property name. Domain invariants are still enforced by the aggregates.
/// </summary>
internal static class ValidationDecorator
{
    private const string RequestLevelKey = "Request";

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<CommandResult<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Notification[] notifications = await ValidateAsync(command, validators, cancellationToken);

            return notifications.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : CommandResult.Fail<TResponse>(notifications);
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<CommandResult> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Notification[] notifications = await ValidateAsync(command, validators, cancellationToken);

            return notifications.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : CommandResult.Fail(notifications);
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<CommandResult<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            Notification[] notifications = await ValidateAsync(query, validators, cancellationToken);

            return notifications.Length == 0
                ? await innerHandler.Handle(query, cancellationToken)
                : CommandResult.Fail<TResponse>(notifications);
        }
    }

    // Sequential on purpose: an async rule may use the scoped DbContext, which is not thread-safe.
    private static async Task<Notification[]> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();

        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(context, cancellationToken);

            failures.AddRange(result.Errors);
        }

        return [.. failures.Select(ToNotification)];
    }

    private static Notification ToNotification(ValidationFailure failure) =>
        Notification.Validation(
            string.IsNullOrWhiteSpace(failure.PropertyName) ? RequestLevelKey : failure.PropertyName,
            failure.ErrorMessage);
}
