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

    /// <summary>Session setting read by the <see cref="LoginLookupPolicyName"/> policy: the one email a tenant-less session may look up.</summary>
    public const string LoginEmailSetting = "app.login_email";
    public const string LoginLookupPolicyName = "login_lookup";
    public const string UserTable = "app_user";
    public const string AuditTable = "audit_event";
    public const string JobTable = "job";

    /// <summary>
    /// Claim of the job queue (T-007). The worker has no tenant when it polls, so this function runs as its owner
    /// (SECURITY DEFINER) to look across tenants; it only locks and returns the next runnable row with
    /// FOR UPDATE SKIP LOCKED, and the caller then establishes that tenant for the rest of the run. It returns
    /// identifiers only, never payloads.
    /// </summary>
    public const string ClaimNextJobFunction = "app_claim_next_job";
    public const string ApplicationRole = "softwarefactory_app";
    public const string TenantTable = "tenant";
    public const string PolicyName = "tenant_isolation";
    public const string CurrentTenantFunction = "app_current_tenant_id";

    /// <summary>Tenant-scoped tables of the initial schema (T-003). Frozen: the InitialSchema migration iterates this list.</summary>
    public static IReadOnlyList<string> InitialTenantScopedTables { get; } =
    [
        UserTable,
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

    /// <summary>Tables added by the authentication migration (T-004). Frozen for the same reason.</summary>
    public static IReadOnlyList<string> AuthTables { get; } = ["refresh_token", AuditTable];

    /// <summary>Tables added by the decision log (HU-003). Frozen for the same reason.</summary>
    public static IReadOnlyList<string> DecisionLogTables { get; } = ["decision_artifact", "decision_author_role", "decision_competent_role", "artifact_type_role"];

    public static IReadOnlyList<string> TenantScopedTables { get; } = [.. InitialTenantScopedTables, .. AuthTables, .. DecisionLogTables];

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

        foreach (var table in InitialTenantScopedTables)
        {
            sql.AppendLine(PolicySql(table, "tenant_id"));
        }

        return sql.ToString();
    }

    public static string DisableSql()
    {
        var sql = new StringBuilder();

        foreach (var table in (string[])[TenantTable, .. InitialTenantScopedTables])
        {
            sql.AppendLine(CultureInfo.InvariantCulture, $"DROP POLICY IF EXISTS {PolicyName} ON {table};");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
        }

        sql.AppendLine(CultureInfo.InvariantCulture, $"DROP FUNCTION IF EXISTS {CurrentTenantFunction}();");
        return sql.ToString();
    }

    /// <summary>
    /// Authentication migration: isolation on the new tables, the append-only grant of the audit table, and the sign-in
    /// lookup: a session without tenant may SELECT the app_user rows whose email equals <see cref="LoginEmailSetting"/> — the
    /// one row a caller claims to be — and nothing else. Once a tenant is established the extra policy is inert.
    /// </summary>
    public static string EnableAuthSql()
    {
        var sql = new StringBuilder();

        foreach (var table in AuthTables)
        {
            sql.AppendLine(PolicySql(table, "tenant_id"));
        }

        sql.AppendLine(CultureInfo.InvariantCulture, $"REVOKE UPDATE, DELETE ON {AuditTable} FROM {ApplicationRole};");
        sql.AppendLine($"""
            CREATE POLICY {LoginLookupPolicyName} ON {UserTable} FOR SELECT
                USING ({CurrentTenantFunction}() IS NULL
                       AND normalized_email = NULLIF(current_setting('{LoginEmailSetting}', true), ''));
            """);

        return sql.ToString();
    }

    /// <summary>Isolation on the tables the decision log adds (HU-003): the same forced policy as every other table.</summary>
    public static string EnableDecisionLogSql()
    {
        var sql = new StringBuilder();

        foreach (var table in DecisionLogTables)
        {
            sql.AppendLine(PolicySql(table, "tenant_id"));
        }

        return sql.ToString();
    }

    public static string DisableDecisionLogSql()
    {
        var sql = new StringBuilder();

        foreach (var table in DecisionLogTables)
        {
            sql.AppendLine(CultureInfo.InvariantCulture, $"DROP POLICY IF EXISTS {PolicyName} ON {table};");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
        }

        return sql.ToString();
    }

    /// <summary>Job-queue claim function (T-007). Owned by the migration account, executable by the application role.</summary>
    public static string EnableJobQueueSql() => $$"""
        CREATE OR REPLACE FUNCTION {{ClaimNextJobFunction}}(p_now timestamptz)
        RETURNS TABLE (job_id uuid, job_tenant_id uuid)
        LANGUAGE sql VOLATILE SECURITY DEFINER
        SET search_path = public
        AS $fn$
            SELECT j.id, j.tenant_id
            FROM {{JobTable}} j
            WHERE (j.state = 'pending' AND j.available_at <= p_now)
               OR (j.state = 'running' AND j.locked_until IS NOT NULL AND j.locked_until < p_now)
            ORDER BY j.available_at, j.created_at
            FOR UPDATE SKIP LOCKED
            LIMIT 1
        $fn$;

        REVOKE ALL ON FUNCTION {{ClaimNextJobFunction}}(timestamptz) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION {{ClaimNextJobFunction}}(timestamptz) TO {{ApplicationRole}};
        """;

    public static string DisableJobQueueSql() =>
        $"DROP FUNCTION IF EXISTS {ClaimNextJobFunction}(timestamptz);";

    public static string DisableAuthSql()
    {
        var sql = new StringBuilder();
        sql.AppendLine(CultureInfo.InvariantCulture, $"DROP POLICY IF EXISTS {LoginLookupPolicyName} ON {UserTable};");
        sql.AppendLine(CultureInfo.InvariantCulture, $"GRANT UPDATE, DELETE ON {AuditTable} TO {ApplicationRole};");

        foreach (var table in AuthTables)
        {
            sql.AppendLine(CultureInfo.InvariantCulture, $"DROP POLICY IF EXISTS {PolicyName} ON {table};");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
            sql.AppendLine(CultureInfo.InvariantCulture, $"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
        }

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
