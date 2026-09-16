namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// One problem found in a content document. <paramref name="Path"/> is the field it belongs to, in camelCase dot
/// notation, so the Api can answer 422 with detail per field (HU-001 contracts).
/// </summary>
public sealed record SchemaValidationError(string Path, string Message);
