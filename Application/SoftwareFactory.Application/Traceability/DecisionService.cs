using Microsoft.Extensions.Logging;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The only way to write the decision log (HU-003). Two things are computed here and never accepted from the caller:
/// what kind of entry this is, and who may close it. That is what keeps the log honest when the author is an agent —
/// it cannot name its own ratifier — and when the author is a person wearing several hats.
/// </summary>
public sealed class DecisionService(
    IDecisionRepository decisions,
    IArtifactRepository artifacts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IArtifactAuthorContext author,
    TimeProvider clock,
    ILogger<DecisionService> logger) : IDecisionService
{
    public async Task<DecisionDto> RecordAsync(RecordDecisionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = RequireTenant();
        var artifactIds = Distinct(command.ArtifactIds);
        var touched = await RequireArtifactsAsync(artifactIds, command.ProjectId, cancellationToken).ConfigureAwait(false);

        var authorRoles = author.Roles.Distinct().ToList();
        var competentRoles = await CompetentRolesAsync(touched, authorRoles, cancellationToken).ConfigureAwait(false);

        var decision = Decision.Record(tenantId, command.ProjectId, author.AuthorType, author.AuthorId, command.Justification, competentRoles);
        var type = DecisionWireNames.Of(decision.Type);
        decisions.Add(decision);

        foreach (var artifactId in artifactIds)
        {
            decisions.AddArtifact(new DecisionArtifact(tenantId, decision.Id, artifactId));
        }

        Snapshot(tenantId, decision.Id, authorRoles, competentRoles);

        audit.Record(
            tenantId,
            AuditedAction.DecisionRecorded,
            author.AuthorType,
            author.AuthorId,
            clock.GetUtcNow(),
            $$"""{"decision":"{{decision.Id}}","type":"{{type}}","artifacts":{{artifactIds.Count}}}""");

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.DecisionRecorded(decision.Id, type, competentRoles.Count);

        return Map(decision, artifactIds, authorRoles, competentRoles);
    }

    public Task<DecisionDto> RatifyAsync(CloseDecisionCommand command, CancellationToken cancellationToken) =>
        CloseAsync(command, DecisionType.Ratification, AuditedAction.DecisionRatified, cancellationToken);

    public Task<DecisionDto> RevertAsync(CloseDecisionCommand command, CancellationToken cancellationToken) =>
        CloseAsync(command, DecisionType.Reversion, AuditedAction.DecisionReverted, cancellationToken);

    public async Task<DecisionPage> SearchAsync(DecisionFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        RequireTenant();

        var query = new DecisionQuery(filter.ProjectId)
        {
            ArtifactId = filter.ArtifactId,
            PendingRole = DecisionWireNames.RoleOrNull(filter.PendingRole),
            Skip = filter.Skip,
            Take = filter.Take,
        };

        var items = await decisions.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var total = await decisions.CountAsync(query, cancellationToken).ConfigureAwait(false);

        return new DecisionPage([.. items.Select(Map)], total, filter.Skip, filter.Take);
    }

    public async Task<IReadOnlyList<ArtifactTypeRoleDto>> GetCompetenceMapAsync(CancellationToken cancellationToken)
    {
        RequireTenant();

        var map = await decisions.GetCompetenceMapAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. map
                .GroupBy(entry => entry.ArtifactType, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new ArtifactTypeRoleDto(
                    group.Key,
                    [.. group.Select(entry => entry.Role).Order().Select(RoleNames.Of)])),
        ];
    }

    private async Task<DecisionDto> CloseAsync(
        CloseDecisionCommand command,
        DecisionType outcome,
        AuditedAction action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = RequireTenant();
        var note = await decisions.GetAsync(command.DecisionId, cancellationToken).ConfigureAwait(false)
            ?? throw new DecisionNotFoundException(command.DecisionId);

        if (!note.IsPending)
        {
            throw new DecisionNotPendingException(note.Id, DecisionWireNames.Of(note.State));
        }

        var competentRoles = await decisions.GetCompetentRolesAsync(note.Id, cancellationToken).ConfigureAwait(false);
        var closerRoles = author.Roles.Distinct().ToList();

        if (!closerRoles.Intersect(competentRoles).Any())
        {
            logger.DecisionCloseRefused(note.Id, "role");
            throw new DecisionRoleNotCompetentException(note.Id, [.. competentRoles.Select(RoleNames.Of)]);
        }

        // Second condition of HU-003 §4, and not a redundant one: the snapshot could never contain the author on the
        // day the note was born, but this runs against the roles in force today, and an author who has since gained
        // the competent role would have cleared the check above.
        if (note.AuthorId == author.AuthorId)
        {
            logger.DecisionCloseRefused(note.Id, "self");
            throw new SelfRatificationException(note.Id);
        }

        var child = note.Close(outcome, author.AuthorType, author.AuthorId, command.Justification);
        var settled = DecisionWireNames.Of(note.State);
        decisions.Add(child);

        // The child bears on the same artifacts as the note it settles, so asking an artifact for its decisions
        // returns the whole story and not just its opening half (HU-003 §5).
        var artifactIds = await decisions.GetArtifactIdsAsync(note.Id, cancellationToken).ConfigureAwait(false);

        foreach (var artifactId in artifactIds)
        {
            decisions.AddArtifact(new DecisionArtifact(tenantId, child.Id, artifactId));
        }

        Snapshot(tenantId, child.Id, closerRoles, []);

        audit.Record(
            tenantId,
            action,
            author.AuthorType,
            author.AuthorId,
            clock.GetUtcNow(),
            $$"""{"decision":"{{child.Id}}","note":"{{note.Id}}"}""");

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.DecisionClosed(note.Id, settled, child.Id);

        return Map(child, artifactIds, closerRoles, []);
    }

    /// <summary>
    /// Roles competent for what the decision touches, minus every role the author holds (HU-003 §2). Taking out all
    /// of them and not just one is what makes a note land on somebody else: a person with two hats is competent with
    /// either, and leaving one in would let them settle their own note from the other.
    /// </summary>
    private async Task<IReadOnlyList<Role>> CompetentRolesAsync(
        IReadOnlyList<Artifact> touched,
        IReadOnlyList<Role> authorRoles,
        CancellationToken cancellationToken)
    {
        var types = touched.Select(artifact => artifact.Type).Distinct(StringComparer.Ordinal).ToList();
        var map = await decisions.GetCompetenceMapAsync(types, cancellationToken).ConfigureAwait(false);

        var unmapped = types
            .Where(type => !map.Any(entry => string.Equals(entry.ArtifactType, type, StringComparison.Ordinal)))
            .ToList();

        if (unmapped.Count > 0)
        {
            logger.CompetenceMapIncomplete(string.Join(", ", unmapped));
            throw new ArtifactTypeWithoutCompetentRoleException(unmapped);
        }

        return [.. map.Select(entry => entry.Role).Distinct().Except(authorRoles).Order()];
    }

    private void Snapshot(Guid tenantId, Guid decisionId, IReadOnlyList<Role> authorRoles, IReadOnlyList<Role> competentRoles)
    {
        foreach (var role in authorRoles)
        {
            decisions.AddAuthorRole(new DecisionAuthorRole(tenantId, decisionId, role));
        }

        foreach (var role in competentRoles)
        {
            decisions.AddCompetentRole(new DecisionCompetentRole(tenantId, decisionId, role));
        }
    }

    private static IReadOnlyList<Guid> Distinct(IReadOnlyList<Guid> artifactIds)
    {
        if (artifactIds is null || artifactIds.Count == 0)
        {
            // A decision that bears on nothing is not a decision: the log exists to tie a judgement to what it moved.
            throw new ArtifactValidationException(
                DecisionNames.Decision,
                [new SchemaValidationError("artifactIds", "A decision has to bear on at least one artifact.")]);
        }

        return [.. artifactIds.Distinct()];
    }

    private async Task<IReadOnlyList<Artifact>> RequireArtifactsAsync(
        IReadOnlyList<Guid> artifactIds,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        List<Artifact> touched = [];

        foreach (var artifactId in artifactIds)
        {
            var artifact = await artifacts.GetAsync(artifactId, cancellationToken).ConfigureAwait(false)
                ?? throw new ArtifactNotFoundException(artifactId);

            if (artifact.ProjectId != projectId && artifact.Level != ArtifactLevel.Global)
            {
                // The decision is filed under a project, so it can only bear on that project's artifacts or on what
                // the tenant shares across projects — the same line HU-002 §6 draws for relations.
                throw new ArtifactValidationException(
                    DecisionNames.Decision,
                    [new SchemaValidationError("artifactIds", $"Artifact {artifactId} belongs to another project and is not global.")]);
            }

            touched.Add(artifact);
        }

        return touched;
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("A decision needs a tenant in context.");

    private static DecisionDto Map(Decision decision, IReadOnlyList<Guid> artifactIds, IReadOnlyList<Role> authorRoles, IReadOnlyList<Role> competentRoles) => new(
        decision.Id,
        decision.ProjectId,
        DecisionWireNames.Of(decision.Type),
        DecisionWireNames.Of(decision.State),
        new ArtifactAuthor(ArtifactWireNames.Of(decision.AuthorType), decision.AuthorId),
        [.. authorRoles.Order().Select(RoleNames.Of)],
        [.. competentRoles.Order().Select(RoleNames.Of)],
        artifactIds,
        decision.Justification,
        decision.ParentDecisionId,
        decision.CreatedAt);

    private static DecisionDto Map(DecisionRecord record) => new(
        record.Id,
        record.ProjectId,
        DecisionWireNames.Of(record.Type),
        DecisionWireNames.Of(record.State),
        new ArtifactAuthor(ArtifactWireNames.Of(record.AuthorType), record.AuthorId),
        [.. record.AuthorRoles.Select(RoleNames.Of)],
        [.. record.CompetentRoles.Select(RoleNames.Of)],
        record.ArtifactIds,
        record.Justification,
        record.ParentDecisionId,
        record.CreatedAt);
}
