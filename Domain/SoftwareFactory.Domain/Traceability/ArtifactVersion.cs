using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>Immutable snapshot of an artifact's content; nothing is ever overwritten.</summary>
public sealed class ArtifactVersion : TenantScopedEntity
{
    private ArtifactVersion()
    {
    }

    public ArtifactVersion(Guid tenantId, Guid artifactId, int number, string content, AuthorType authorType, Guid authorId)
        : base(tenantId)
    {
        Guard.NotEmpty(artifactId);
        Guard.NotEmpty(authorId);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArtifactId = artifactId;
        Number = number;
        Content = Guard.ValidJson(content);
        AuthorType = authorType;
        AuthorId = authorId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ArtifactId { get; private set; }

    public int Number { get; private set; }

    /// <summary>JSON document validated against the schema of the artifact type.</summary>
    public string Content { get; private set; } = string.Empty;

    public AuthorType AuthorType { get; private set; }

    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
