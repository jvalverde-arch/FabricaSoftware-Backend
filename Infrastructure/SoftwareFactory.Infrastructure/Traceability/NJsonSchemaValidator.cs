using System.Globalization;
using NJsonSchema;
using NJsonSchema.Validation;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Infrastructure.Traceability;

/// <summary>
/// JSON Schema validation with NJsonSchema (MIT). Schemas are compiled once per text and cached: the same schema is
/// validated against on every write of its type. Errors come back with the field path the Api reports per field.
/// </summary>
internal sealed class NJsonSchemaValidator : IJsonSchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _compiled = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<SchemaValidationError> Validate(string schemaJson, string contentJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentJson);

        var schema = Compile(schemaJson);

        return [.. schema.Validate(contentJson).Select(Describe)];
    }

    private JsonSchema Compile(string schemaJson)
    {
        lock (_gate)
        {
            if (_compiled.TryGetValue(schemaJson, out var cached))
            {
                return cached;
            }

            var schema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();
            _compiled[schemaJson] = schema;
            return schema;
        }
    }

    private static SchemaValidationError Describe(ValidationError error) =>
        new(PathOf(error), string.Create(CultureInfo.InvariantCulture, $"{error.Kind}"));

    /// <summary>Turns «#/acceptance_criteria/0» into «acceptanceCriteria[0]», which is how the Api names fields.</summary>
    private static string PathOf(ValidationError error)
    {
        var path = error.Path?.TrimStart('#', '/') ?? string.Empty;

        if (path.Length == 0)
        {
            return "content";
        }

        var builder = new System.Text.StringBuilder();

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                builder.Append(CultureInfo.InvariantCulture, $"[{index}]");
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('.');
            }

            builder.Append(ToCamelCase(segment));
        }

        return builder.ToString();
    }

    private static string ToCamelCase(string segment)
    {
        var parts = segment.Split('_', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => segment,
            1 => parts[0],
            _ => string.Concat(parts[0], string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]))),
        };
    }
}
