namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// One relation of the card (HU-005 §2). The marks travel computed: whether following it leaves the module, whether
/// it leaves the project, and which project it lands in — so the UI can warn instead of changing context in silence.
/// </summary>
public sealed record CardRelationDto(
    Guid RelationId,
    string Type,
    Guid ArtifactId,
    string ArtifactType,
    string Title,
    string State,
    int? Score,
    Guid ProjectId,
    string ProjectName,
    Guid? ModuleId,
    string? ModuleTitle,
    bool CrossesModule,
    bool CrossesProject);

/// <summary>Relations of one kind, in one direction. Grouping is by relation type, which is how the panel reads.</summary>
public sealed record CardRelationGroup(string Type, IReadOnlyList<CardRelationDto> Items);

/// <summary>
/// Everything the artifact card needs, in one call (HU-005 contracts). The schema travels with it because no other
/// endpoint exposes it and the content cannot be drawn without it.
///
/// <para>
/// <c>Version</c> is the one being shown — the current one unless the caller asked for another — and <c>Schema</c>
/// is that version's schema, not the current one of the type. Drawing old content against a newer schema would
/// label fields with names that did not exist when it was written, and say nothing about it.
/// </para>
/// </summary>
public sealed record ArtifactCardDto(
    ArtifactDto Artifact,
    int Version,
    string Content,
    int SchemaVersion,
    ArtifactSchemaDto Schema,
    ArtifactAuthor Author,
    IReadOnlyList<CardRelationGroup> Upstream,
    IReadOnlyList<CardRelationGroup> Downstream,
    IReadOnlyList<ArtifactVersionDto> Versions,
    IReadOnlyList<DecisionDto> Decisions);

/// <summary>The JSON Schema document itself, as the frontend needs it to draw the content.</summary>
public sealed record ArtifactSchemaDto(string Type, int Version, string Json);

/// <summary>Asks for the card of an artifact, optionally at a version other than the current one.</summary>
public sealed record ArtifactCardQuery(Guid ArtifactId)
{
    public int? Version { get; init; }
}
