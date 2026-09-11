namespace SoftwareFactory.Domain.Brain;

/// <summary>Embedding dimensions of the knowledge base. Fixed by the HNSW index: changing it requires a migration and re-indexing.</summary>
public static class EmbeddingVector
{
    public const int Dimensions = 1536;
}
