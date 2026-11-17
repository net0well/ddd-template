using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Messaging;
using Hangfire;
using SharedKernel.DependencyInjection;

namespace Jobs.Commands;

internal sealed class HangfireJobScheduler(IBackgroundJobClient backgroundJobs) : IJobScheduler, IInjectSingleton
{
    // CancellationToken.None is a placeholder: Hangfire passes the job's own token at execution time.
    public string Enqueue<TCommand>(TCommand command)
        where TCommand : ICommand =>
        backgroundJobs.Enqueue<CommandJob<TCommand>>(job => job.ExecuteAsync(command, CancellationToken.None));

    public string Schedule<TCommand>(TCommand command, TimeSpan delay)
        where TCommand : ICommand =>
        backgroundJobs.Schedule<CommandJob<TCommand>>(job => job.ExecuteAsync(command, CancellationToken.None), delay);
}
