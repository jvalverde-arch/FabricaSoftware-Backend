using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Brain;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class SourceDocumentConfiguration : TenantScopedConfiguration<SourceDocument>
{
    protected override void ConfigureEntity(EntityTypeBuilder<SourceDocument> builder)
    {
        builder.ToTable("source_document", table =>
        {
            table.HasCheckConstraint("ck_source_document_state", CheckConstraints.EnumIn<SourceDocumentState>("state"));
            table.HasCheckConstraint("ck_source_document_size_bytes", "size_bytes >= 0");
        });

        builder.Property(document => document.Title).IsRequired();
        builder.Property(document => document.ContentType).IsRequired();
        builder.Property(document => document.StorageKey).IsRequired();
        builder.Property(document => document.State).HasConversion<SnakeCaseEnumConverter<SourceDocumentState>>().IsRequired();
        builder.HasIndex(document => document.TenantId);
        builder.UseXminConcurrencyToken();
    }
}
