using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SoftwareFactory.Domain.Common;

/// <summary>Invariant checks shared by entity constructors and methods.</summary>
public static class Guard
{
    public static void NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The identifier must not be empty.", paramName);
        }
    }

    public static string NotBlank(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }

    public static string ValidJson(string json, [CallerArgumentExpression(nameof(json))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json, paramName);

        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The value must be a valid JSON document.", paramName, exception);
        }

        return json;
    }

    public static void InRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, paramName);
    }
}
