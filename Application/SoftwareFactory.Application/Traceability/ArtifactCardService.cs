using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The artifact card (HU-005): one call that brings what the page draws. It composes the module's own services
/// instead of reaching into repositories behind them, so what the card shows is what any other caller would get.
/// </summary>
public sealed class ArtifactCardService(
    IArtifactService artifacts,
    IDecisionService decisions,
    IArtifactCardRepository relations,
    ArtifactSchemaRegistry schemas,
    ITenantContext tenantContext) : IArtifactCardService
{
    public async Task<ArtifactCardDto> GetAsync(ArtifactCardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        RequireTenant();

        var detail = await artifacts.GetAsync(query.ArtifactId, cancellationToken).ConfigureAwait(false);
        var versions = await artifacts.GetVersionsAsync(query.ArtifactId, cancellationToken).ConfigureAwait(false);
        var shown = await ShownAsync(query, detail, cancellationToken).ConfigureAwait(false);

        // The schema of the version being shown, never the current one of the type: old content drawn against a new
        // schema gets labels for fields that did not exist when it was written, and nothing says so.
        var schema = schemas.Get(detail.Artifact.Type, shown.SchemaVersion);

        var rows = await relations.GetRelationsAsync(query.ArtifactId, cancellationToken).ConfigureAwait(false);
        var decided = await decisions
            .SearchAsync(new DecisionFilter(detail.Artifact.ProjectId) { ArtifactId = query.ArtifactId, Take = 100 }, cancellationToken)
            .ConfigureAwait(false);

        var thisModule = ModuleOf(rows, detail.Artifact);

        return new ArtifactCardDto(
            detail.Artifact,
            shown.Version,
            shown.Content,
            shown.SchemaVersion,
            new ArtifactSchemaDto(schema.Type, schema.Version, schema.Json),
            shown.Author,
            Group(rows, detail.Artifact, thisModule, upstream: true),
            Group(rows, detail.Artifact, thisModule, upstream: false),
            versions,
            decided.Items);
    }

    /// <summary>Content, schema version and author of the version asked for; the current one when none was.</summary>
    private async Task<ShownVersion> ShownAsync(ArtifactCardQuery query, ArtifactDetailDto detail, CancellationToken cancellationToken)
    {
        if (query.Version is not { } version || version == detail.Artifact.CurrentVersion)
        {
            return new ShownVersion(detail.Artifact.CurrentVersion, detail.Content, detail.SchemaVersion, detail.LastAuthor);
        }

        var older = await artifacts.GetVersionAsync(query.ArtifactId, version, cancellationToken).ConfigureAwait(false);

        return new ShownVersion(version, older.Content, older.SchemaVersion, older.LastAuthor);
    }

    /// <summary>
    /// The module this artifact belongs to, read from the relations already loaded: a <c>belongs_to</c> of its own
    /// towards a module. A module is its own module, so what hangs from it does not read as a crossing.
    /// </summary>
    private static Guid? ModuleOf(IReadOnlyList<CardRelationRow> rows, ArtifactDto artifact)
    {
        if (string.Equals(artifact.Type, ArtifactTypeCatalog.Module, StringComparison.Ordinal))
        {
            return artifact.Id;
        }

        return rows
            .Where(row => row.Upstream
                && string.Equals(row.RelationType, RelationNames.BelongsTo, StringComparison.Ordinal)
                && string.Equals(row.OtherType, ArtifactTypeCatalog.Module, StringComparison.Ordinal))
            .Select(row => (Guid?)row.OtherId)
            .FirstOrDefault();
    }

    private static IReadOnlyList<CardRelationGroup> Group(
        IReadOnlyList<CardRelationRow> rows,
        ArtifactDto artifact,
        Guid? thisModule,
        bool upstream) =>
    [
        .. rows
            .Where(row => row.Upstream == upstream)
            .GroupBy(row => row.RelationType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CardRelationGroup(
                group.Key,
                [.. group.Select(row => Map(row, artifact, thisModule))])),
    ];

    private static CardRelationDto Map(CardRelationRow row, ArtifactDto artifact, Guid? thisModule) => new(
        row.RelationId,
        row.RelationType,
        row.OtherId,
        row.OtherType,
        row.OtherTitle,
        ArtifactWireNames.Of(row.OtherState),
        row.OtherScore,
        row.OtherProjectId,
        row.OtherProjectName,
        row.OtherModuleId,
        row.OtherModuleTitle,
        // Unknown on either side is not a crossing: an artifact nobody filed under a module has no module to leave.
        CrossesModule: thisModule is not null && row.OtherModuleId is not null && thisModule != row.OtherModuleId,
        CrossesProject: row.OtherProjectId != artifact.ProjectId);

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("An artifact card needs a tenant in context.");

    private sealed record ShownVersion(int Version, string Content, int SchemaVersion, ArtifactAuthor Author);
}
