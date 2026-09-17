using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SoftwareFactory.Infrastructure.Persistence.Initialization;
using SoftwareFactory.Infrastructure.Persistence.Security;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecisionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_decision_tenant_id_project_id",
                table: "decision");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decision_role",
                table: "decision");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "role",
                table: "decision");

            migrationBuilder.CreateTable(
                name: "artifact_type_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artifact_type_role", x => x.id);
                    table.CheckConstraint("ck_artifact_type_role_role", "role IN ('admin', 'functional', 'architect', 'qa', 'compliance', 'reader')");
                    table.ForeignKey(
                        name: "fk_artifact_type_role_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decision_artifact",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_artifact", x => x.id);
                    table.ForeignKey(
                        name: "fk_decision_artifact_artifact_id",
                        column: x => x.artifact_id,
                        principalTable: "artifact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_artifact_decision_id",
                        column: x => x.decision_id,
                        principalTable: "decision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_decision_artifact_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decision_author_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_author_role", x => x.id);
                    table.CheckConstraint("ck_decision_author_role_role", "role IN ('admin', 'functional', 'architect', 'qa', 'compliance', 'reader')");
                    table.ForeignKey(
                        name: "fk_decision_author_role_decision_id",
                        column: x => x.decision_id,
                        principalTable: "decision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_decision_author_role_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decision_competent_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_competent_role", x => x.id);
                    table.CheckConstraint("ck_decision_competent_role_role", "role IN ('admin', 'functional', 'architect', 'qa', 'compliance', 'reader')");
                    table.ForeignKey(
                        name: "fk_decision_competent_role_decision_id",
                        column: x => x.decision_id,
                        principalTable: "decision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_decision_competent_role_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_decision_tenant_id_project_id_state",
                table: "decision",
                columns: new[] { "tenant_id", "project_id", "state" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed', 'artifact_created', 'artifact_updated', 'artifact_state_changed', 'artifact_deleted', 'relation_created', 'relation_deleted', 'decision_recorded', 'decision_ratified', 'decision_reverted')");

            migrationBuilder.CreateIndex(
                name: "ux_artifact_type_role_tenant_id_artifact_type_role",
                table: "artifact_type_role",
                columns: new[] { "tenant_id", "artifact_type", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_artifact_artifact_id",
                table: "decision_artifact",
                column: "artifact_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_artifact_tenant_id_artifact_id",
                table: "decision_artifact",
                columns: new[] { "tenant_id", "artifact_id" });

            migrationBuilder.CreateIndex(
                name: "ux_decision_artifact_decision_id_artifact_id",
                table: "decision_artifact",
                columns: new[] { "decision_id", "artifact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_author_role_tenant_id_decision_id",
                table: "decision_author_role",
                columns: new[] { "tenant_id", "decision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_decision_author_role_decision_id_role",
                table: "decision_author_role",
                columns: new[] { "decision_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_competent_role_tenant_id_role",
                table: "decision_competent_role",
                columns: new[] { "tenant_id", "role" });

            migrationBuilder.CreateIndex(
                name: "ux_decision_competent_role_decision_id_role",
                table: "decision_competent_role",
                columns: new[] { "decision_id", "role" },
                unique: true);

            // Seed first, isolation second: forced row-level security applies to the owner too, so once the policy is
            // on, a session without app.tenant_id cannot write a row for every tenant in one statement.
            migrationBuilder.Sql(ArtifactTypeRoleSeedSql.ForEveryTenant());

            migrationBuilder.Sql(RowLevelSecurity.EnableDecisionLogSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RowLevelSecurity.DisableDecisionLogSql());

            migrationBuilder.DropTable(
                name: "artifact_type_role");

            migrationBuilder.DropTable(
                name: "decision_artifact");

            migrationBuilder.DropTable(
                name: "decision_author_role");

            migrationBuilder.DropTable(
                name: "decision_competent_role");

            migrationBuilder.DropIndex(
                name: "ix_decision_tenant_id_project_id_state",
                table: "decision");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "decision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_decision_tenant_id_project_id",
                table: "decision",
                columns: new[] { "tenant_id", "project_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_decision_role",
                table: "decision",
                sql: "role IN ('admin', 'functional', 'architect', 'qa', 'compliance', 'reader')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_action",
                table: "audit_event",
                sql: "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed', 'artifact_created', 'artifact_updated', 'artifact_state_changed', 'artifact_deleted', 'relation_created', 'relation_deleted')");
        }
    }
}
