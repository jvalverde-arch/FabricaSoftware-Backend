using Microsoft.Extensions.Logging;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The only way to write artifacts (HU-001). Every path through here validates the content against the schema of its
/// type, versions what changed, and leaves an audit trail; the Api and the agents call this, never the tables.
/// </summary>
public sealed class ArtifactService(
    IArtifactRepository artifacts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ArtifactSchemaRegistry schemas,
    ArtifactContentMigrator migrator,
    IJsonSchemaValidator validator,
    ITenantContext tenantContext,
    IArtifactAuthorContext author,
    TimeProvider clock,
    ILogger<ArtifactService> logger) : IArtifactService
{
    public async Task<ArtifactDetailDto> CreateAsync(CreateArtifactCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = RequireTenant();
        var schemaVersion = schemas.CurrentVersionOf(command.Type);
        Validate(command.Type, schemaVersion, command.Content);

        var now = clock.GetUtcNow();
        var artifact = new Artifact(tenantId, command.ProjectId, command.Type, command.Title, ArtifactWireNames.Level(command.Level));
        artifacts.Add(artifact);

        var version = WriteVersion(artifact, command.Content, schemaVersion, now);
        Audit(AuditedAction.ArtifactCreated, artifact, now, $$"""{"type":"{{command.Type}}","version":1}""");
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.Created(artifact.Id, artifact.Type, artifact.ProjectId, schemaVersion);
        return Detail(artifact, version);
    }

    public async Task<ArtifactDetailDto> UpdateAsync(UpdateArtifactCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var artifact = await RequireArtifactAsync(command.ArtifactId, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var current = await CurrentVersionAsync(artifact, cancellationToken).ConfigureAwait(false);
        var changed = false;

        if (command.Title is { Length: > 0 } title && !string.Equals(title, artifact.Title, StringComparison.Ordinal))
        {
            artifact.Rename(title, now);
            changed = true;
        }

        if (command.Content is { Length: > 0 } content && !SameContent(current?.Content, content))
        {
            var schemaVersion = schemas.CurrentVersionOf(artifact.Type);
            Validate(artifact.Type, schemaVersion, content);
            var previousSchemaVersion = current?.SchemaVersion ?? schemaVersion;
            current = WriteVersion(artifact, content, schemaVersion, now);

            if (schemaVersion > previousSchemaVersion)
            {
                // The score was measured against the previous schema, so it is optimistic and would lie. S5 owns
                // scoring; here the artifact simply goes back to «sin evaluar» until it is measured again.
                artifact.SetScore(null, now);
                logger.ScoreInvalidated(artifact.Id, previousSchemaVersion, schemaVersion);
            }

            Audit(AuditedAction.ArtifactUpdated, artifact, now, $$"""{"version":{{current.Number}}}""");
            logger.Updated(artifact.Id, current.Number, schemaVersion);
            changed = true;
        }

        if (ArtifactWireNames.StateOrNull(command.State) is { } state && state != artifact.State)
        {
            if (!artifact.CanChangeStateTo(state))
            {
                throw new ArtifactStateTransitionException(artifact.Id, ArtifactWireNames.Of(artifact.State), ArtifactWireNames.Of(state));
            }

            var previous = artifact.State;
            artifact.ChangeState(state, now);
            Audit(AuditedAction.ArtifactStateChanged, artifact, now, $$"""{"from":"{{previous}}","to":"{{state}}"}""");
            logger.StateChanged(artifact.Id, previous, state);
            changed = true;
        }

        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Detail(artifact, current);
    }

    public async Task<ArtifactDetailDto> GetAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var current = await CurrentVersionAsync(artifact, cancellationToken).ConfigureAwait(false);

        // Faithful to history: the content comes back exactly as written, with the schema version it was written
        // against. It is never re-validated or reshaped against a newer schema.
        return Detail(artifact, current);
    }

    public async Task<ArtifactDetailDto> GetForEditingAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var current = await CurrentVersionAsync(artifact, cancellationToken).ConfigureAwait(false);

        if (current is null)
        {
            return Detail(artifact, current);
        }

        // Editing happens against the current shape of the type, so the stored content is walked forward through the
        // published upgraders. Nothing is written here: the upgrade is persisted only when the person saves.
        var upgraded = migrator.UpgradeToCurrent(artifact.Type, current.Content, current.SchemaVersion);

        return new ArtifactDetailDto(
            Map(artifact),
            upgraded.Content,
            upgraded.Version,
            new ArtifactAuthor(ArtifactWireNames.Of(current.AuthorType), current.AuthorId));
    }

    public async Task<ArtifactPage> SearchAsync(ArtifactFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        RequireTenant();

        var query = new ArtifactQuery(filter.ProjectId)
        {
            Type = filter.Type,
            State = ArtifactWireNames.StateOrNull(filter.State),
            ModuleId = filter.ModuleId,
            MinScore = filter.MinScore,
            MaxScore = filter.MaxScore,
            Title = filter.Title,
            Skip = filter.Skip,
            Take = filter.Take,
        };

        var items = await artifacts.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var total = await artifacts.CountAsync(query, cancellationToken).ConfigureAwait(false);

        return new ArtifactPage([.. items.Select(Map)], total, filter.Skip, filter.Take);
    }

    public async Task<IReadOnlyList<ArtifactVersionDto>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var versions = await artifacts.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);

        return
        [
            .. versions.Select(version => new ArtifactVersionDto(
                version.Number,
                version.SchemaVersion,
                new ArtifactAuthor(ArtifactWireNames.Of(version.AuthorType), version.AuthorId),
                version.CreatedAt)),
        ];
    }

    public async Task<ArtifactDiffDto> GetDiffAsync(Guid artifactId, int fromVersion, int toVersion, CancellationToken cancellationToken)
    {
        await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);

        var from = await RequireVersionAsync(artifactId, fromVersion, cancellationToken).ConfigureAwait(false);
        var to = await RequireVersionAsync(artifactId, toVersion, cancellationToken).ConfigureAwait(false);

        return new ArtifactDiffDto(fromVersion, toVersion, ArtifactDiff.Between(from.Content, to.Content));
    }

    public async Task DeleteAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var relations = await artifacts.GetActiveRelationsAsync(artifactId, cancellationToken).ConfigureAwait(false);

        if (relations.Count > 0)
        {
            logger.DeleteBlocked(artifactId, relations.Count);
            throw new ArtifactHasRelationsException(
                artifactId,
                [.. relations.Select(relation => new BlockingRelation(
                    relation.RelationId,
                    relation.Type,
                    relation.OtherArtifactId,
                    relation.OtherArtifactTitle,
                    relation.Incoming ? "incoming" : "outgoing"))]);
        }

        var now = clock.GetUtcNow();
        artifact.Delete(now);
        Audit(AuditedAction.ArtifactDeleted, artifact, now, details: null);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.Deleted(artifactId);
    }

    public async Task<ArtifactDto> SetScoreAsync(Guid artifactId, int? score, CancellationToken cancellationToken)
    {
        var artifact = await RequireArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);

        artifact.SetScore(score, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Map(artifact);
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("An artifact needs a tenant in context.");

    private async Task<Artifact> RequireArtifactAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        RequireTenant();

        return await artifacts.GetAsync(artifactId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArtifactNotFoundException(artifactId);
    }

    private async Task<ArtifactVersion> RequireVersionAsync(Guid artifactId, int number, CancellationToken cancellationToken) =>
        await artifacts.GetVersionAsync(artifactId, number, cancellationToken).ConfigureAwait(false)
            ?? throw new ArtifactNotFoundException(artifactId);

    private Task<ArtifactVersion?> CurrentVersionAsync(Artifact artifact, CancellationToken cancellationToken) =>
        artifact.CurrentVersion == 0
            ? Task.FromResult<ArtifactVersion?>(null)
            : artifacts.GetVersionAsync(artifact.Id, artifact.CurrentVersion, cancellationToken);

    private void Validate(string type, int schemaVersion, string content)
    {
        var errors = validator.Validate(schemas.Get(type, schemaVersion).Json, content);

        if (errors.Count > 0)
        {
            throw new ArtifactValidationException(type, errors);
        }
    }

    private ArtifactVersion WriteVersion(Artifact artifact, string content, int schemaVersion, DateTimeOffset now)
    {
        var version = new ArtifactVersion(
            artifact.TenantId,
            artifact.Id,
            artifact.AdvanceVersion(now),
            content,
            schemaVersion,
            author.AuthorType,
            author.AuthorId);

        artifacts.AddVersion(version);
        return version;
    }

    private void Audit(AuditedAction action, Artifact artifact, DateTimeOffset now, string? details) =>
        audit.Record(artifact.TenantId, action, author.AuthorType, author.AuthorId, now, details);

    private static bool SameContent(string? current, string candidate) =>
        current is not null && string.Equals(current, candidate, StringComparison.Ordinal);

    private static ArtifactDto Map(Artifact artifact) => new(
        artifact.Id,
        artifact.ProjectId,
        artifact.Type,
        artifact.Title,
        ArtifactWireNames.Of(artifact.State),
        ArtifactWireNames.Of(artifact.Level),
        artifact.Score,
        artifact.CurrentVersion,
        artifact.CreatedAt,
        artifact.UpdatedAt);

    private static ArtifactDetailDto Detail(Artifact artifact, ArtifactVersion? version) => new(
        Map(artifact),
        version?.Content ?? "{}",
        version?.SchemaVersion ?? 0,
        new ArtifactAuthor(ArtifactWireNames.Of(version?.AuthorType ?? AuthorType.Human), version?.AuthorId ?? Guid.Empty));
}
