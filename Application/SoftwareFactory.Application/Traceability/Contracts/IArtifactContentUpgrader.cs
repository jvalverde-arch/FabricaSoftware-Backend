namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Moves the content of one artifact type from schema version <see cref="FromVersion"/> to the next one (HU-001).
/// Additive schema changes need no upgrader: old content still validates. A breaking change (a new required field, a
/// rename, a narrowed type) must publish one, or edits of older artifacts are refused instead of silently corrupted.
/// Upgraders are pure functions over the document: no database, no clock, no tenant.
/// </summary>
public interface IArtifactContentUpgrader
{
    string ArtifactType { get; }

    /// <summary>Version this upgrader reads; it produces <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    string Upgrade(string content);
}
