namespace SoftwareFactory.Application.Common.Jobs;

/// <summary>The worker establishes the run at the start of the scope, like the middleware does with the tenant.</summary>
public interface ICurrentJobWriter
{
    void Establish(Guid jobId);
}
