using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Finops;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class LlmCallConfiguration : TenantScopedConfiguration<LlmCall>
{
    protected override void ConfigureEntity(EntityTypeBuilder<LlmCall> builder)
    {
        builder.ToTable("llm_call", table => table.HasCheckConstraint(
            "ck_llm_call_non_negative",
            "input_tokens >= 0 AND output_tokens >= 0 AND cache_read_tokens >= 0 AND cache_write_tokens >= 0 AND latency_ms >= 0 AND cost >= 0"));

        builder.Property(call => call.Provider).IsRequired();
        builder.Property(call => call.Model).IsRequired();
        builder.Property(call => call.Cost).HasPrecision(12, 6);
        builder.HasOne<Job>().WithMany().HasForeignKey(call => call.JobId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(call => call.JobId);
        builder.HasIndex(call => new { call.TenantId, call.CreatedAt });
    }
}
