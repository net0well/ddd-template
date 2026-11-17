using Application.Abstractions.BackgroundJobs;
using Application.Users.SendWelcomeEmail;
using Domain.Users;
using SharedKernel.Events;

namespace Application.Users.Register;

// The e-mail goes through a background job: a slow or failing mail server must not fail the registration.
internal sealed class UserRegisteredDomainEventHandler(IJobScheduler jobScheduler)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        jobScheduler.Enqueue(new SendWelcomeEmailCommand(domainEvent.UserId, domainEvent.Email));

        return Task.CompletedTask;
    }
}
