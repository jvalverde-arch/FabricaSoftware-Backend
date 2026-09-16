using SoftwareFactory.Application.Common.Jobs;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeCurrentJob : ICurrentJob, ICurrentJobWriter
{
    public Guid? JobId { get; private set; }

    public void Establish(Guid jobId) => JobId = jobId;
}
