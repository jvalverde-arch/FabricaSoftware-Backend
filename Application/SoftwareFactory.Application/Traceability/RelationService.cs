using System.Text.Json;
using Microsoft.Extensions.Logging;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The only way to write relations (HU-002). Relations are what turn loose artifacts into a traceability model, so
/// every path through here checks the compatibility matrix, the cross-project rule, and leaves an audit trail.
/// </summary>
public sealed class RelationService(
    IRelationRepository relations,
    IArtifactRepository artifacts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    RelationCompatibilityMatrix matrix,
    ITenantContext tenantContext,
    IArtifactAuthorContext author,
    TimeProvider clock,
    ILogger<RelationService> logger) : IRelationService
{
    /// <summary>Hops the neighborhood query accepts (HU-002 §3); more than three stops being a neighborhood.</summary>
    public const int MaxLevels = 3;

    public async Task<RelationDto> CreateAsync(CreateRelationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = RequireTenant();

        if (!matrix.Knows(command.Type))
        {
            throw new RelationTypeUnknownException(command.Type);
        }

        if (command.SourceId == command.TargetId)
        {
            throw new RelationSelfReferenceException(command.SourceId);
        }

        var metadata = ValidMetadata(command.Metadata);
        var source = await RequireArtifactAsync(command.SourceId, cancellationToken).ConfigureAwait(false);
        var target = await RequireArtifactAsync(command.TargetId, cancellationToken).ConfigureAwait(false);

        if (!matrix.Allows(source.Type, command.Type, target.Type))
        {
            logger.RelationRefused(source.Type, command.Type, target.Type);
            throw new RelationIncompatibleException(source.Type, command.Type, target.Type, matrix.AllowedTargets(source.Type, command.Type));
        }

        GuardProjects(source, target);

        if (await relations.ExistsAsync(source.Id, target.Id, command.Type, cancellationToken).ConfigureAwait(false))
        {
            throw new RelationAlreadyExistsException(source.Id, target.Id, command.Type);
        }

        var now = clock.GetUtcNow();
        var relation = new Relation(tenantId, source.Id, target.Id, command.Type, metadata, author.AuthorType, author.AuthorId);
        relations.Add(relation);

        audit.Record(
            tenantId,
            AuditedAction.RelationCreated,
            author.AuthorType,
            author.AuthorId,
            now,
            $$"""{"source":"{{source.Id}}","target":"{{target.Id}}","type":"{{command.Type}}"}""");

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.RelationCreated(relation.Id, source.Id, target.Id, command.Type);
        return Map(relation);
    }

    public async Task DeleteAsync(Guid artifactId, Guid relationId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var relation = await relations.GetAsync(relationId, cancellationToken).ConfigureAwait(false)
            ?? throw new RelationNotFoundException(relationId);

        if (relation.SourceId != artifactId && relation.TargetId != artifactId)
        {
            // Asking to delete somebody else's relation through this artifact is not a different error: from here
            // that relation simply is not there.
            throw new RelationNotFoundException(relationId);
        }

        relations.Remove(relation);

        // A relation leaves no tombstone — the traceability model reads what is there today — so the audit event is
        // the only record that it ever existed (HU-002 §5).
        audit.Record(
            tenantId,
            AuditedAction.RelationDeleted,
            author.AuthorType,
            author.AuthorId,
            clock.GetUtcNow(),
            $$"""{"source":"{{relation.SourceId}}","target":"{{relation.TargetId}}","type":"{{relation.Type}}"}""");

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.RelationDeleted(relationId, relation.Type);
    }

    public async Task<IReadOnlyList<RelationDto>> GetRelationsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var found = await relations.GetForArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);

        return [.. found.Select(Map)];
    }

    public async Task<NeighborhoodDto> GetNeighborhoodAsync(Guid artifactId, int levels, CancellationToken cancellationToken)
    {
        if (levels is < 1 or > MaxLevels)
        {
            // A bad number of hops is the caller asking for something the contract does not offer, not a bug: it
            // travels as a refusal of the module, like any other rule here.
            throw new RelationLevelsOutOfRangeException(levels, MaxLevels);
        }

        await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var neighborhood = await relations.GetNeighborhoodAsync(artifactId, levels, cancellationToken).ConfigureAwait(false);

        return new NeighborhoodDto(
            artifactId,
            levels,
            [.. neighborhood.Nodes.Select(node => new NeighborhoodNode(
                node.Id,
                node.ProjectId,
                node.Type,
                node.Title,
                ArtifactWireNames.Of(node.State),
                ArtifactWireNames.Of(node.Level),
                node.Score,
                node.Depth))],
            [.. neighborhood.Links.Select(link => new NeighborhoodEdge(link.Id, link.SourceId, link.TargetId, link.Type))]);
    }

    public async Task<IReadOnlyList<OrphanDto>> GetOrphansAsync(Guid projectId, string type, string missingRelation, CancellationToken cancellationToken)
    {
        RequireTenant();

        if (!matrix.Knows(missingRelation))
        {
            throw new RelationTypeUnknownException(missingRelation);
        }

        var orphans = await relations.GetOrphansAsync(projectId, type, missingRelation, cancellationToken).ConfigureAwait(false);

        return [.. orphans.Select(artifact => new OrphanDto(
            artifact.Id,
            artifact.Type,
            artifact.Title,
            ArtifactWireNames.Of(artifact.State),
            artifact.Score))];
    }

    /// <summary>
    /// Crossing projects is allowed only when at least one end is a tenant-global artifact (HU-002 §6): that is the
    /// boundary contract a module exposes and another consumes. Two project artifacts of different projects are a
    /// modelling mistake, not a shortcut. Crossing tenants is not checked here because it cannot happen: row-level
    /// security never shows the other tenant's artifact in the first place.
    /// </summary>
    private static void GuardProjects(Artifact source, Artifact target)
    {
        if (source.ProjectId == target.ProjectId)
        {
            return;
        }

        if (source.Level != ArtifactLevel.Global && target.Level != ArtifactLevel.Global)
        {
            throw new RelationCrossProjectException(source.Id, target.Id);
        }
    }

    private static string? ValidMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);
        }
        catch (JsonException exception)
        {
            throw new ArtifactValidationException(
                "relation",
                [new SchemaValidationError("metadata", exception.Message)]);
        }

        return metadata;
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("A relation needs a tenant in context.");

    private async Task<Artifact> RequireArtifactAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        RequireTenant();

        return await artifacts.GetAsync(artifactId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArtifactNotFoundException(artifactId);
    }

    private static RelationDto Map(Relation relation) => new(
        relation.Id,
        relation.SourceId,
        relation.TargetId,
        relation.Type,
        relation.Metadata,
        new ArtifactAuthor(ArtifactWireNames.Of(relation.CreatedByType), relation.CreatedBy),
        relation.CreatedAt);
}
