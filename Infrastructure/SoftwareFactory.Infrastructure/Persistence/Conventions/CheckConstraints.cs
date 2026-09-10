namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>SQL fragments for check constraints that back domain invariants.</summary>
internal static class CheckConstraints
{
    /// <summary>Restricts a text column to the snake_case values of an enum.</summary>
    public static string EnumIn<TEnum>(string column)
        where TEnum : struct, Enum
    {
        var values = string.Join(", ", EnumText<TEnum>.Values.Select(value => $"'{value}'"));
        return $"{column} IN ({values})";
    }
}
