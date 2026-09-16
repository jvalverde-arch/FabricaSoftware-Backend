using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeClientContext(AuditClient client) : IClientContext
{
    public AuditClient Client { get; } = client;
}
