using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Brain;

/// <summary>Fragment of a source document with its embedding and citation, the unit of retrieval for agents.</summary>
public sealed class Chunk : TenantScopedEntity
{
    private Chunk()
    {
    }

    public Chunk(Guid tenantId, Guid sourceDocumentId, int sequence, string content, string? citation)
        : base(tenantId)
    {
        Guard.NotEmpty(sourceDocumentId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        SourceDocumentId = sourceDocumentId;
        Sequence = sequence;
        Content = Guard.NotBlank(content);
        Citation = string.IsNullOrWhiteSpace(citation) ? null : citation.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid SourceDocumentId { get; private set; }

    /// <summary>Zero-based position of the chunk inside its document.</summary>
    public int Sequence { get; private set; }

    public string Content { get; private set; } = string.Empty;

    /// <summary>Embedding of <see cref="EmbeddingVector.Dimensions"/> floats; null until indexed.</summary>
    public ReadOnlyMemory<float>? Embedding { get; private set; }

    /// <summary>Human-readable location inside the source (page, section) for citations.</summary>
    public string? Citation { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetEmbedding(ReadOnlyMemory<float> embedding)
    {
        if (embedding.Length != EmbeddingVector.Dimensions)
        {
            throw new ArgumentException($"The embedding must have exactly {EmbeddingVector.Dimensions} dimensions.", nameof(embedding));
        }

        Embedding = embedding;
    }
}
