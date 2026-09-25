using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

internal sealed class FakeArtifactCardRepository : IArtifactCardRepository
{
    public List<CardRelationRow> Rows { get; } = [];

    public Task<IReadOnlyList<CardRelationRow>> GetRelationsAsync(Guid artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CardRelationRow>>([.. Rows]);
}
