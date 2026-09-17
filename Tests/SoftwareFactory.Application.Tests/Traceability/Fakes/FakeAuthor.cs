using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

internal sealed class FakeAuthor(AuthorType type, Guid id, params Role[] roles) : IArtifactAuthorContext
{
    public AuthorType AuthorType { get; } = type;

    public Guid AuthorId { get; } = id;

    public IReadOnlyCollection<Role> Roles { get; } = roles;
}
