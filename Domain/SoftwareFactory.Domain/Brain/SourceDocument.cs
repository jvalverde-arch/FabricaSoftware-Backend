using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Brain;

/// <summary>Metadata of a knowledge document; the bytes live in object storage (doc 03, D8).</summary>
public sealed class SourceDocument : TenantScopedEntity
{
    private SourceDocument()
    {
    }

    public SourceDocument(Guid tenantId, string title, string contentType, string storageKey, long sizeBytes, string? sourceReference)
        : base(tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        Title = Guard.NotBlank(title);
        ContentType = Guard.NotBlank(contentType);
        StorageKey = Guard.NotBlank(storageKey);
        SizeBytes = sizeBytes;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        State = SourceDocumentState.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Title { get; private set; } = string.Empty;

    /// <summary>MIME type of the stored object.</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Key of the object in blob storage.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Where the document came from (URL, file name, system), for citations.</summary>
    public string? SourceReference { get; private set; }

    public SourceDocumentState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
