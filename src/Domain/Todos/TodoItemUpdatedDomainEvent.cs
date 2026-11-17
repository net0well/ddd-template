using SharedKernel.Events;

namespace Domain.Todos;

public sealed record TodoItemUpdatedDomainEvent(Guid TodoItemId) : IDomainEvent;
