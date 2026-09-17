using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Translation between the decision enums and the wire names of the module contract (HU-003). It lives inside the
/// module: the callers — the Api and the agents — only ever see the strings.
/// </summary>
internal static class DecisionWireNames
{
    public static string Of(DecisionType type) => type switch
    {
        DecisionType.Decision => DecisionNames.Decision,
        DecisionType.OutOfRoleNote => DecisionNames.OutOfRoleNote,
        DecisionType.Ratification => DecisionNames.Ratification,
        DecisionType.Reversion => DecisionNames.Reversion,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static string Of(DecisionState state) => state switch
    {
        DecisionState.Recorded => DecisionNames.Recorded,
        DecisionState.Pending => DecisionNames.Pending,
        DecisionState.Ratified => DecisionNames.Ratified,
        DecisionState.Reverted => DecisionNames.Reverted,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>The role of a <c>pending_role</c> filter; an unknown name is the caller's mistake, not an empty page.</summary>
    public static Role? RoleOrNull(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return RoleNames.TryParse(role, out var parsed)
            ? parsed
            : throw new ArtifactValidationException(
                DecisionNames.Decision,
                [new SchemaValidationError("pendingRole", $"'{role}' is not a role of the platform.")]);
    }
}
