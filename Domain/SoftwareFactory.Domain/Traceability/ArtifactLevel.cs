namespace SoftwareFactory.Domain.Traceability;

/// <summary>Where a definition lives: inside one project or as a tenant-wide (global) asset inherited by projects.</summary>
public enum ArtifactLevel
{
    Project,
    Global,
}
