using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArtifactContentSchemaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            // Content written before this column conforms to v1 of its type's schema, which is the only one published
            // so far; recording it is what lets those rows be read after a type evolves (HU-001).
            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                table: "artifact_version",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed', 'artifact_created', 'artifact_updated', 'artifact_state_changed', 'artifact_deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_artifact_version_schema_version",
                table: "artifact_version",
                sql: "schema_version >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_artifact_alive",
                table: "artifact",
                columns: new[] { "project_id", "state" },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_artifact_version_schema_version",
                table: "artifact_version");

            migrationBuilder.DropIndex(
                name: "ix_artifact_alive",
                table: "artifact");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "artifact_version");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed')");
        }
    }
}
