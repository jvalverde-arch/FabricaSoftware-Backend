using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Brain;
using SoftwareFactory.Domain.Finops;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence;

/// <summary>Unit of work over the platform database (estandar-backend.md §2): one transaction per service operation.</summary>
public sealed class SoftwareFactoryDbContext(DbContextOptions<SoftwareFactoryDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<SoftwareProject> Projects => Set<SoftwareProject>();

    public DbSet<Artifact> Artifacts => Set<Artifact>();

    public DbSet<ArtifactVersion> ArtifactVersions => Set<ArtifactVersion>();

    public DbSet<Relation> Relations => Set<Relation>();

    public DbSet<Decision> Decisions => Set<Decision>();

    public DbSet<DecisionArtifact> DecisionArtifacts => Set<DecisionArtifact>();

    public DbSet<DecisionAuthorRole> DecisionAuthorRoles => Set<DecisionAuthorRole>();

    public DbSet<DecisionCompetentRole> DecisionCompetentRoles => Set<DecisionCompetentRole>();

    public DbSet<ArtifactTypeRole> ArtifactTypeRoles => Set<ArtifactTypeRole>();

    public DbSet<LlmCall> LlmCalls => Set<LlmCall>();

    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();

    public DbSet<Chunk> Chunks => Set<Chunk>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoftwareFactoryDbContext).Assembly);
        modelBuilder.ApplySnakeCaseNaming();
    }
}
