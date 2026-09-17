using System.Globalization;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seed of the competence map (HU-003 §3) for tenants that already exist when the migration runs. Tenants created
/// afterwards get theirs from <see cref="DatabaseSeeder"/>, which is the only path that creates a tenant today.
/// The values are the frozen literals of <see cref="ArtifactTypeRoleSeed"/>, never anything a caller supplies.
/// </summary>
internal static class ArtifactTypeRoleSeedSql
{
    public static string ForEveryTenant()
    {
        var rows = string.Join(
            $",{Environment.NewLine}        ",
            ArtifactTypeRoleSeed.Base.Select(pair =>
                string.Create(CultureInfo.InvariantCulture, $"('{pair.ArtifactType}', '{EnumText<Role>.ToText(pair.Role)}')")));

        // The identifier is a v4 here and a v7 everywhere else: the migration has no application code to generate one,
        // and this table is configuration nobody orders by time.
        return $"""
            INSERT INTO artifact_type_role (id, tenant_id, artifact_type, role)
            SELECT gen_random_uuid(), t.id, m.artifact_type, m.role
            FROM tenant t
            CROSS JOIN (VALUES
                {rows}
            ) AS m(artifact_type, role)
            ON CONFLICT (tenant_id, artifact_type, role) DO NOTHING;
            """;
    }
}
