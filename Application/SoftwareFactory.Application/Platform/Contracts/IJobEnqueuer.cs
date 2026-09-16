namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Enqueues work for the agents (doc 03, D6). The tenant comes from the context, never from the caller.</summary>
public interface IJobEnqueuer
{
    /// <summary>Queues a run and returns its id.</summary>
    /// <param name="type">Value of <c>job.type</c>; a handler must serve it.</param>
    /// <param name="payload">JSON document with the arguments of the run.</param>
    /// <param name="cancellationToken">Cancellation of the caller.</param>
    Task<Guid> EnqueueAsync(string type, string payload, CancellationToken cancellationToken);
}
