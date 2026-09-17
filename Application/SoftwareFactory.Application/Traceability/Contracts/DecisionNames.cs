namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Wire names of the decision log (HU-003); the same snake_case the database stores.</summary>
public static class DecisionNames
{
    public const string Decision = "decision";
    public const string OutOfRoleNote = "out_of_role_note";
    public const string Ratification = "ratification";
    public const string Reversion = "reversion";

    public const string Recorded = "recorded";
    public const string Pending = "pending";
    public const string Ratified = "ratified";
    public const string Reverted = "reverted";
}
