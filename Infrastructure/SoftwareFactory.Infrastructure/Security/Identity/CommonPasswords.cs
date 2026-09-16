using System.Collections.Frozen;
using System.Reflection;

namespace SoftwareFactory.Infrastructure.Security.Identity;

/// <summary>
/// Embedded list of the most used passwords of 12+ characters, taken from the SecLists «xato-net-10-million-passwords»
/// ranking (MIT licence). Compared case-insensitively. Loaded once per process.
/// </summary>
internal static class CommonPasswords
{
    private const string ResourceName = "SoftwareFactory.Infrastructure.Security.Identity.Resources.common-passwords.txt";

    private static readonly Lazy<FrozenSet<string>> _passwords = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static int Count => _passwords.Value.Count;

    public static bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _passwords.Value.Contains(password.Trim());
    }

    private static FrozenSet<string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
