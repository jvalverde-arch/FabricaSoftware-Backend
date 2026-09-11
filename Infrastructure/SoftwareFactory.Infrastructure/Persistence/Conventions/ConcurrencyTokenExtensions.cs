using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>Uses PostgreSQL's system column <c>xmin</c> as optimistic concurrency token (estandar-backend.md §4): a stale update yields 409.</summary>
internal static class ConcurrencyTokenExtensions
{
    public static EntityTypeBuilder<TEntity> UseXminConcurrencyToken<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        return builder;
    }
}
