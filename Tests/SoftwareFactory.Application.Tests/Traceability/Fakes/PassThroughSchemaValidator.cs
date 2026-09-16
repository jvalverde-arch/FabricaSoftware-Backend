using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

/// <summary>
/// Stands in for the real JSON Schema library: the service is tested for what it does with the verdict, not for the
/// validator's own logic (that is covered against the real schemas in the Infrastructure suite).
/// </summary>
internal sealed class PassThroughSchemaValidator : IJsonSchemaValidator
{
    public List<SchemaValidationError> NextErrors { get; } = [];

    public List<(string Schema, string Content)> Calls { get; } = [];

    public IReadOnlyList<SchemaValidationError> Validate(string schemaJson, string contentJson)
    {
        Calls.Add((schemaJson, contentJson));
        var errors = NextErrors.ToList();
        NextErrors.Clear();
        return errors;
    }
}
