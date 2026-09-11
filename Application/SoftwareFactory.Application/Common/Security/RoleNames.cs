using System.Collections.Frozen;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Common.Security;

/// <summary>
/// Wire names of the roles (estandar-auth.md §4): <c>admin</c>, <c>functional</c>, <c>architect</c>, <c>qa</c>, <c>compliance</c>,
/// <c>reader</c>. Used for the <c>roles</c> claim, the authorization policies and the API responses.
/// </summary>
public static class RoleNames
{
    private static readonly FrozenDictionary<Role, string> _names = Enum.GetValues<Role>().ToFrozenDictionary(role => role, NameOf);

    private static readonly FrozenDictionary<string, Role> _roles = _names
        .ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<string> All => _names.Values;

    public static string Of(Role role) => _names[role];

    public static bool TryParse(string? name, out Role role)
    {
        if (name is not null && _roles.TryGetValue(name, out role))
        {
            return true;
        }

        role = default;
        return false;
    }

    private static string NameOf(Role role) => role switch
    {
        Role.Admin => "admin",
        Role.Functional => "functional",
        Role.Architect => "architect",
        Role.Qa => "qa",
        Role.Compliance => "compliance",
        Role.Reader => "reader",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Role without a wire name."),
    };
}
