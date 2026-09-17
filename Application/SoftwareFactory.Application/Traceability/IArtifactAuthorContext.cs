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

    /// <summary>
    /// Every role the author holds right now (plural: a person may wear several hats, a substitute agent wears
    /// exactly one — the role it stands in for). The decision log stores them as a snapshot and subtracts all of
    /// them when it works out who is competent (HU-003 §1-2).
    /// </summary>
    IReadOnlyCollection<Role> Roles { get; }
}
