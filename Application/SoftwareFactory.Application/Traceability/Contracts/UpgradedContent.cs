namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Content ready to be written, with the schema version it now conforms to.</summary>
public sealed record UpgradedContent(string Content, int Version, bool Changed);
