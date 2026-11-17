using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.DependencyInjection;
using SharedKernel.Events;

namespace Infrastructure.DomainEvents;

/// <summary>
/// Calls every <see cref="IDomainEventHandler{T}"/> of each event, in its own DI scope, one after the other.
/// </summary>
/// <remarks>
/// Runs after the changes are committed, so a failing handler is logged and the others still run: the data is
/// already saved, and failing the request would make the client retry something that succeeded. Side effects
/// that must not be lost belong in a background job (see <c>IJobScheduler</c>), which retries.
/// </remarks>
internal sealed class DomainEventsDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DomainEventsDispatcher> logger)
    : IDomainEventsDispatcher, IInjectTransient
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeDictionary = new();
    private static readonly ConcurrentDictionary<Type, Type> WrapperTypeDictionary = new();

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

            Type domainEventType = domainEvent.GetType();
            Type handlerType = HandlerTypeDictionary.GetOrAdd(
                domainEventType,
                et => typeof(IDomainEventHandler<>).MakeGenericType(et));

            foreach (object? handler in scope.ServiceProvider.GetServices(handlerType))
            {
                if (handler is null)
                {
                    continue;
                }

                try
                {
                    await HandlerWrapper.Create(handler, domainEventType).Handle(domainEvent, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "Domain event handler {Handler} failed for {DomainEvent}",
                        handler.GetType().Name,
                        domainEventType.Name);
                }
            }
        }
    }

    // Avoids reflection on every call: the generic wrapper casts once and calls the typed handler.
    private abstract class HandlerWrapper
    {
        public abstract Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken);

        public static HandlerWrapper Create(object handler, Type domainEventType)
        {
            Type wrapperType = WrapperTypeDictionary.GetOrAdd(
                domainEventType,
                et => typeof(HandlerWrapper<>).MakeGenericType(et));

            return (HandlerWrapper)Activator.CreateInstance(wrapperType, handler)!;
        }
    }

    private sealed class HandlerWrapper<T>(object handler) : HandlerWrapper
        where T : IDomainEvent
    {
        private readonly IDomainEventHandler<T> _handler = (IDomainEventHandler<T>)handler;

        public override Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken) =>
            _handler.Handle((T)domainEvent, cancellationToken);
    }
}
