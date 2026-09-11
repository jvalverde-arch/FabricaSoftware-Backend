using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SoftwareFactory.Infrastructure.Persistence.Security;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    actor_type = table.Column<string>(type: "text", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                    table.CheckConstraint("ck_audit_event_action", "action IN ('login_succeeded', 'login_failed', 'lockout', 'refresh_reuse_detected', 'logout', 'password_changed')");
                    table.CheckConstraint("ck_audit_event_actor_type", "actor_type IS NULL OR actor_type IN ('human', 'agent')");
                    table.ForeignKey(
                        name: "fk_audit_event_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_token_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_actor_id",
                table: "audit_event",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_tenant_id_occurred_at",
                table: "audit_event",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_family_id",
                table: "refresh_token",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_user_id",
                table: "refresh_token",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_token_tenant_id_token_hash",
                table: "refresh_token",
                columns: new[] { "tenant_id", "token_hash" },
                unique: true);

            // Row-level security on the new tables, append-only audit, and the sign-in lookup policy (T-004).
            migrationBuilder.Sql(RowLevelSecurity.EnableAuthSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RowLevelSecurity.DisableAuthSql());

            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "refresh_token");
        }
    }
}
