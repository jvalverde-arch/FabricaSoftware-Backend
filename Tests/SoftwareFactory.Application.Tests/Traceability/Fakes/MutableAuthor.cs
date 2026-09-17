using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

/// <summary>An author whose identity and hats can change between one call and the next, which is what the decision
/// log has to survive: the roles are read again when somebody tries to settle a note (HU-003 §4).</summary>
internal sealed class MutableAuthor(Guid id, params Role[] roles) : IArtifactAuthorContext
{
    public AuthorType AuthorType { get; set; } = AuthorType.Human;

    public Guid AuthorId { get; set; } = id;

    public IReadOnlyCollection<Role> Roles { get; set; } = roles;
}
