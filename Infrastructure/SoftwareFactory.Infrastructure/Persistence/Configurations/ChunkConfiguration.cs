using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Brain;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class ChunkConfiguration : TenantScopedConfiguration<Chunk>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunk", table => table.HasCheckConstraint("ck_chunk_sequence", "sequence >= 0"));

        builder.Property(chunk => chunk.Content).IsRequired();
        builder.Property(chunk => chunk.Embedding)
            .HasConversion<EmbeddingConverter>()
            .HasColumnType($"vector({EmbeddingVector.Dimensions})");

        builder.HasOne<SourceDocument>().WithMany().HasForeignKey(chunk => chunk.SourceDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(chunk => new { chunk.SourceDocumentId, chunk.Sequence }).IsUnique();
        builder.HasIndex(chunk => chunk.TenantId);

        // Approximate nearest-neighbour search for the hybrid retrieval of the knowledge base (estandar-backend.md §4).
        builder.HasIndex(chunk => chunk.Embedding)
            .HasDatabaseName("ix_chunk_embedding_hnsw")
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
