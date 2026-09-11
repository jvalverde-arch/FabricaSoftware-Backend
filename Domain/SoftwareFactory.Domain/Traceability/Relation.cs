using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>Typed, directed n:m link between two artifacts (implements, depends_on, validates, affects, belongs_to, derives_from).</summary>
public sealed class Relation : TenantScopedEntity
{
    private Relation()
    {
    }

    public Relation(Guid tenantId, Guid sourceId, Guid targetId, string type, string? metadata, AuthorType createdByType, Guid createdBy)
        : base(tenantId)
    {
        Guard.NotEmpty(sourceId);
        Guard.NotEmpty(targetId);
        Guard.NotEmpty(createdBy);

        if (sourceId == targetId)
        {
            throw new ArgumentException("An artifact cannot be related to itself.", nameof(targetId));
        }

        SourceId = sourceId;
        TargetId = targetId;
        Type = Guard.NotBlank(type);
        Metadata = metadata is null ? null : Guard.ValidJson(metadata);
        CreatedByType = createdByType;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid SourceId { get; private set; }

    public Guid TargetId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    /// <summary>Optional JSON metadata of the relation.</summary>
    public string? Metadata { get; private set; }

    public AuthorType CreatedByType { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
