using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.AgentRuntime.Composition;

/// <summary>
/// The worker has no author yet: agent identities (<c>author_type = agent</c>) arrive with S4. Declaring it here
/// keeps the host explicit — a run that tries to write an artifact today says why instead of writing it nameless.
/// </summary>
internal sealed class AgentAuthorNotAvailable : IArtifactAuthorContext
{
    public AuthorType AuthorType => AuthorType.Agent;

    public Guid AuthorId =>
        throw new InvalidOperationException(
            "The worker has no agent identity yet: artifact authorship for agents arrives with S4 (sprint-01 HU-001 is human-authored).");
}
