using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelationTraversalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_relation_source_id",
                table: "relation");

            migrationBuilder.DropIndex(
                name: "ix_relation_tenant_id",
                table: "relation");

            migrationBuilder.CreateIndex(
                name: "ix_relation_tenant_id_source_id",
                table: "relation",
                columns: new[] { "tenant_id", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_relation_tenant_id_target_id",
                table: "relation",
                columns: new[] { "tenant_id", "target_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_relation_tenant_id_source_id",
                table: "relation");

            migrationBuilder.DropIndex(
                name: "ix_relation_tenant_id_target_id",
                table: "relation");

            migrationBuilder.CreateIndex(
                name: "ix_relation_source_id",
                table: "relation",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_relation_tenant_id",
                table: "relation",
                column: "tenant_id");
        }
    }
}
