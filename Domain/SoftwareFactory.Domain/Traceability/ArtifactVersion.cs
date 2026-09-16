using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Immutable snapshot of an artifact's content (HU-001 §2); nothing is ever overwritten. It records the schema
/// version the content was written against, which is what lets an old version still be read after the type's schema
/// evolves — reading interprets content with its own schema and never re-validates it against a newer one.
/// </summary>
public sealed class ArtifactVersion : TenantScopedEntity
{
    private ArtifactVersion()
    {
    }

    public ArtifactVersion(
        Guid tenantId,
        Guid artifactId,
        int number,
        string content,
        int schemaVersion,
        AuthorType authorType,
        Guid authorId)
        : base(tenantId)
    {
        Guard.NotEmpty(artifactId);
        Guard.NotEmpty(authorId);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);

        ArtifactId = artifactId;
        Number = number;
        Content = Guard.ValidJson(content);
        SchemaVersion = schemaVersion;
        AuthorType = authorType;
        AuthorId = authorId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ArtifactId { get; private set; }

    public int Number { get; private set; }

    /// <summary>JSON document validated against the schema of the artifact type at <see cref="SchemaVersion"/>.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Version of the type's JSON Schema this content conforms to.</summary>
    public int SchemaVersion { get; private set; }

    public AuthorType AuthorType { get; private set; }

    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
