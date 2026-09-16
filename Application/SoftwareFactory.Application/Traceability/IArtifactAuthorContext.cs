using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Who is writing right now: the signed-in person, or the agent acting in their place. An agent writes through the
/// same door as a human and its authorship is recorded the same way (doc 01, principles 6-7).
/// </summary>
public interface IArtifactAuthorContext
{
    AuthorType AuthorType { get; }

    Guid AuthorId { get; }
}
