namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>
/// Context of one run handed to a handler: what to do, which attempt this is, and how to report progress so the
/// stream shows real phases instead of a spinner (estandar-frontend.md §3).
/// </summary>
public sealed class JobExecution(ClaimedJob job, Func<string, int?, CancellationToken, Task> reportProgress)
{
    public Guid JobId => job.JobId;

    public Guid TenantId => job.TenantId;

    public string Type => job.Type;

    /// <summary>JSON document with the arguments of the run.</summary>
    public string Payload => job.Payload;

    public int Attempt => job.Attempt;

    public Task ReportProgressAsync(string phase, int? percent, CancellationToken cancellationToken) =>
        reportProgress(phase, percent, cancellationToken);
}
