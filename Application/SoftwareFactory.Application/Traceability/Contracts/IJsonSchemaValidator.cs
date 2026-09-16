namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Validates a JSON document against a JSON Schema. The library lives in Infrastructure so swapping it is one file;
/// the schemas themselves ship with Application (HU-001 technical notes).
/// </summary>
public interface IJsonSchemaValidator
{
    IReadOnlyList<SchemaValidationError> Validate(string schemaJson, string contentJson);
}
