using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelationAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Relations are audited like everything else that changes the model (HU-002 §5); the constraint is the
            // list of actions the table accepts, so it grows with each module that starts recording.
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed', 'artifact_created', 'artifact_updated', 'artifact_state_changed', 'artifact_deleted', 'relation_created', 'relation_deleted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed', 'artifact_created', 'artifact_updated', 'artifact_state_changed', 'artifact_deleted')");
        }
    }
}
