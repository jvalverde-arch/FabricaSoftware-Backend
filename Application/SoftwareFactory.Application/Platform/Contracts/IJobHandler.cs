namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>
/// Executes one type of job. Handlers live in Application and run inside the tenant of the job, so they use the same
/// services a human request would (estandar-backend.md §2).
/// </summary>
public interface IJobHandler
{
    /// <summary>Value of <c>job.type</c> this handler answers to.</summary>
    string JobType { get; }

    Task HandleAsync(JobExecution execution, CancellationToken cancellationToken);
}
