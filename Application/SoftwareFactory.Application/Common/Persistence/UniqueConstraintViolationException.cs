namespace SoftwareFactory.Application.Common.Persistence;

/// <summary>
/// A write lost against a unique index. It exists so the rule of <c>estandar-backend.md</c> §4 — the unique index
/// is the authority and the previous check is the courtesy — can be honoured without every service having to know
/// what a Postgres error code looks like: the unit of work translates, the service decides what it means.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string constraintName, Exception innerException)
        : base($"A unique constraint was violated: {constraintName}.", innerException)
    {
        ConstraintName = constraintName;
    }

    public UniqueConstraintViolationException()
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    /// <summary>Name of the index that refused the write, as the database reports it.</summary>
    public string ConstraintName { get; } = string.Empty;
}
