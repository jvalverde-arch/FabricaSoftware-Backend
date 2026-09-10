using System.Text;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>PascalCase to snake_case for database identifiers (estandar-backend.md §4).</summary>
internal static class SnakeCase
{
    public static string Convert(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                var previousIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var startsNewWordAfterAcronym = i > 0 && i + 1 < name.Length && char.IsUpper(name[i - 1]) && char.IsLower(name[i + 1]);

                if (previousIsLowerOrDigit || startsNewWordAfterAcronym)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
