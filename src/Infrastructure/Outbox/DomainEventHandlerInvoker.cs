using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Events;

namespace Infrastructure.Outbox;

/// <summary>Finds and calls the <see cref="IDomainEventHandler{T}"/>s of an event without reflection per call.</summary>
internal static class DomainEventHandlerInvoker
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes = new();
    private static readonly ConcurrentDictionary<Type, IHandlerWrapper> Wrappers = new();

    public static IEnumerable<object> GetHandlers(IServiceProvider serviceProvider, Type eventType)
    {
        Type handlerType = HandlerTypes.GetOrAdd(eventType, type => typeof(IDomainEventHandler<>).MakeGenericType(type));

        return serviceProvider.GetServices(handlerType).OfType<object>();
    }

    public static Task InvokeAsync(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        IHandlerWrapper wrapper = Wrappers.GetOrAdd(
            domainEvent.GetType(),
            type => (IHandlerWrapper)Activator.CreateInstance(typeof(HandlerWrapper<>).MakeGenericType(type))!);

        return wrapper.Handle(handler, domainEvent, cancellationToken);
    }

    private interface IHandlerWrapper
    {
        Task Handle(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken);
    }

    private sealed class HandlerWrapper<T> : IHandlerWrapper
        where T : IDomainEvent
    {
        public Task Handle(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken) =>
            ((IDomainEventHandler<T>)handler).Handle((T)domainEvent, cancellationToken);
    }
}
