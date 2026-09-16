using System.Globalization;
using System.Text.Json;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Field-level difference between two content documents (HU-001 contracts). Paths keep the document's own keys
/// (<c>acceptance_criteria[1]</c>) so a reader can find the value in the JSON it is looking at.
/// </summary>
public static class ArtifactDiff
{
    public static IReadOnlyList<ArtifactFieldChange> Between(string fromContent, string toContent)
    {
        var before = Flatten(fromContent);
        var after = Flatten(toContent);

        List<ArtifactFieldChange> changes = [];

        foreach (var (path, value) in before)
        {
            if (!after.TryGetValue(path, out var updated))
            {
                changes.Add(new ArtifactFieldChange(path, value, null, ArtifactChangeKind.Removed));
            }
            else if (!string.Equals(value, updated, StringComparison.Ordinal))
            {
                changes.Add(new ArtifactFieldChange(path, value, updated, ArtifactChangeKind.Modified));
            }
        }

        changes.AddRange(after
            .Where(entry => !before.ContainsKey(entry.Key))
            .Select(entry => new ArtifactFieldChange(entry.Key, null, entry.Value, ArtifactChangeKind.Added)));

        return [.. changes.OrderBy(change => change.Path, StringComparer.Ordinal)];
    }

    private static Dictionary<string, string?> Flatten(string json)
    {
        using var document = JsonDocument.Parse(json);
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        Walk(document.RootElement, string.Empty, values);
        return values;
    }

    private static void Walk(JsonElement element, string path, Dictionary<string, string?> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", values);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]"), values);
                    index++;
                }

                break;

            case JsonValueKind.Null:
                values[path] = null;
                break;

            default:
                values[path] = element.ToString();
                break;
        }
    }
}
