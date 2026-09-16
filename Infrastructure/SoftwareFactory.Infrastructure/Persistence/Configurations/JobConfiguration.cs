using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class JobConfiguration : TenantScopedConfiguration<Job>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("job", table => table.HasCheckConstraint("ck_job_state", CheckConstraints.EnumIn<JobState>("state")));
        builder.Property(job => job.Type).IsRequired();
        builder.Property(job => job.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(job => job.State).HasConversion<SnakeCaseEnumConverter<JobState>>().IsRequired();
        builder.HasIndex(job => job.TenantId);

        // Hot-state partial index for the worker's FOR UPDATE SKIP LOCKED poll (estandar-backend.md §4): the claim
        // orders by available_at and only pending or lease-expired rows are candidates.
        builder.HasIndex(job => new { job.AvailableAt, job.CreatedAt })
            .HasDatabaseName("ix_job_claimable")
            .HasFilter($"state IN ('{EnumText<JobState>.ToText(JobState.Pending)}', '{EnumText<JobState>.ToText(JobState.Running)}')");

        builder.UseXminConcurrencyToken();
    }
}
