using SharedKernel.Events;

namespace Domain.Users;

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email) : IDomainEvent;
