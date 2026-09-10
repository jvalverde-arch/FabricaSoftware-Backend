using System.Globalization;
using System.Text;

namespace SoftwareFactory.Infrastructure.Persistence.Security;

/// <summary>
/// SQL for tenant isolation (estandar-backend.md §4, doc 03 §5). Every table gets ENABLE + FORCE ROW LEVEL SECURITY with a
/// policy on <c>tenant_id</c> (the tenant table on <c>id</c>). The application connects as a member of
/// <see cref="ApplicationRole"/>, a NOLOGIN group without BYPASSRLS; only migrations run with the owner account.
/// The session tenant is read from the <c>app.tenant_id</c> setting: unset or empty means no rows.
/// </summary>
internal static class RowLevelSecurity
{
    public const string TenantSetting = "app.tenant_id";
    public const string ApplicationRole = "softwarefactory_app";
    public const string TenantTable = "tenant";
    public const string PolicyName = "tenant_isolation";
    public const string CurrentTenantFunction = "app_current_tenant_id";

    public static IReadOnlyList<string> TenantScopedTables { get; } =
    [
        "app_user",
        "user_role",
        "project",
        "artifact",
        "artifact_version",
        "relation",
        "decision",
        "job",
        "llm_call",
        "source_document",
        "chunk",
    ];

    public static IReadOnlyList<string> AllTables { get; } = [TenantTable, .. TenantScopedTables];

    public static string EnableSql()
    {
        var sql = new StringBuilder();

        sql.AppendLine($$"""
            CREATE OR REPLACE FUNCTION {{CurrentTenantFunction}}() RETURNS uuid
            LANGUAGE sql STABLE AS $$
                SELECT NULLIF(current_setting('{{TenantSetting}}', true), '')::uuid
            $$;

            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{{ApplicationRole}}') THEN
                    CREATE ROLE {{ApplicationRole}} NOLOGIN NOBYPASSRLS NOSUPERUSER NOCREATEDB NOCREATEROLE;
                END IF;
            END
            $$;

            GRANT USAGE ON SCHEMA public TO {{ApplicationRole}};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {{ApplicationRole}};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {{ApplicationRole}};
            """);

        sql.AppendLine(PolicySql(TenantTable, "id"));

        foreach (var table in TenantScopedTables)
        {
            sql.AppendLine(PolicySql(table, "tenant_id"));
        }

        return sql.ToString();
    }

    public static string DisableSql()
    {
        var sql = new StringBuilder();

        foreach (var table in AllTables)
        {
            sql.AppendLine(CultureInfo.InvariantCulture, $"DROP POLICY IF EXISTS {PolicyName} ON {table};");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
        }

        sql.AppendLine(CultureInfo.InvariantCulture, $"DROP FUNCTION IF EXISTS {CurrentTenantFunction}();");
        return sql.ToString();
    }

    private static string PolicySql(string table, string tenantColumn) => $"""
        ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY {PolicyName} ON {table}
            USING ({tenantColumn} = {CurrentTenantFunction}())
            WITH CHECK ({tenantColumn} = {CurrentTenantFunction}());
        """;
}
