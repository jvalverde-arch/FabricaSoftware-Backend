namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Wire names of the relation types (sprint-01, R1 catalog); the same snake_case the database stores.</summary>
public static class RelationNames
{
    public const string Implements = "implements";
    public const string DependsOn = "depends_on";
    public const string Validates = "validates";
    public const string Affects = "affects";
    public const string BelongsTo = "belongs_to";
    public const string DerivesFrom = "derives_from";
    public const string Exposes = "exposes";
    public const string Consumes = "consumes";

    /// <summary>Direction of a relation seen from the artifact that was asked about.</summary>
    public const string Outgoing = "outgoing";

    public const string Incoming = "incoming";
}
