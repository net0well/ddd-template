using Application.Abstractions.Messaging;

namespace Application.Abstractions.BackgroundJobs;

/// <summary>
/// Runs a command in the background, with retries, through its regular handler. Implemented with Hangfire
/// in the Jobs project.
/// </summary>
/// <remarks>
/// Use it for slow or unreliable side effects (e-mail, external APIs). The command is serialized: keep it
/// to ids and values. A failed result counts as a job failure and is retried.
/// </remarks>
public interface IJobScheduler
{
    /// <returns>The job id.</returns>
    string Enqueue<TCommand>(TCommand command)
        where TCommand : ICommand;

    /// <returns>The job id.</returns>
    string Schedule<TCommand>(TCommand command, TimeSpan delay)
        where TCommand : ICommand;
}
