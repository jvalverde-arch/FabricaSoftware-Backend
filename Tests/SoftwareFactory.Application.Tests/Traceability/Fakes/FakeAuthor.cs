using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

internal sealed class FakeAuthor(AuthorType type, Guid id) : IArtifactAuthorContext
{
    public AuthorType AuthorType { get; } = type;

    public Guid AuthorId { get; } = id;
}
