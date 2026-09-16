namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// One published version of the content schema of an artifact type (HU-001). Schemas ship with the application as
/// embedded resources, so the code and the schemas it validates against deploy together.
/// </summary>
public sealed record ArtifactSchema(string Type, int Version, string Json);
