using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Api.Composition;

/// <summary>What the Api writes is written by the signed-in person; agents get their own identity in S4.</summary>
internal sealed class ArtifactAuthorFromCurrentUser(ICurrentUser currentUser) : IArtifactAuthorContext
{
    public AuthorType AuthorType => AuthorType.Human;

    public Guid AuthorId => currentUser.UserId;

    /// <summary>Every role of the token: the decision log subtracts all of them, not one chosen for the person.</summary>
    public IReadOnlyCollection<Role> Roles => currentUser.Roles;
}
