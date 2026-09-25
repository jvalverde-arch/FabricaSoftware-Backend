using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

internal sealed class StubDecisionService : IDecisionService
{
    public List<DecisionDto> Decisions { get; } = [];

    public DecisionFilter? Asked { get; private set; }

    public Task<DecisionPage> SearchAsync(DecisionFilter filter, CancellationToken cancellationToken)
    {
        Asked = filter;
        return Task.FromResult(new DecisionPage([.. Decisions], Decisions.Count, 0, 100));
    }

    public Task<DecisionDto> RecordAsync(RecordDecisionCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<DecisionDto> RatifyAsync(CloseDecisionCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<DecisionDto> RevertAsync(CloseDecisionCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ArtifactTypeRoleDto>> GetCompetenceMapAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
