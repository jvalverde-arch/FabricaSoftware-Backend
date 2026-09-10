namespace SoftwareFactory.Infrastructure.Persistence.Options;

/// <summary>Database settings. Connection strings live under ConnectionStrings (see <see cref="DatabaseConnectionStrings"/>).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Login role the application connects with; it is a member of the RLS-bound group role.</summary>
    public string AppRoleName { get; set; } = "softwarefactory_app_user";

    /// <summary>Password of the login role. In Development it may be omitted: the initializer then generates one per run.</summary>
    public string? AppRolePassword { get; set; }
}
