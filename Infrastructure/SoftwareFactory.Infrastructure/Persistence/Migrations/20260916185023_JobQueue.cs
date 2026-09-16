using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SoftwareFactory.Infrastructure.Persistence.Security;

#nullable disable

namespace SoftwareFactory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_job_pending_created_at",
                table: "job");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "available_at",
                table: "job",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "job",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "job",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progress_percent",
                table: "job",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_claimable",
                table: "job",
                columns: new[] { "available_at", "created_at" },
                filter: "state IN ('pending', 'running')");

            // Existing rows were queued before this column existed: they are available since they were created.
            migrationBuilder.Sql("UPDATE job SET available_at = created_at WHERE available_at IS NULL OR available_at < created_at;");

            // Claim of the queue across tenants without breaking row-level security (T-007).
            migrationBuilder.Sql(RowLevelSecurity.EnableJobQueueSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RowLevelSecurity.DisableJobQueueSql());

            migrationBuilder.DropIndex(
                name: "ix_job_claimable",
                table: "job");

            migrationBuilder.DropColumn(
                name: "available_at",
                table: "job");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "job");

            migrationBuilder.DropColumn(
                name: "phase",
                table: "job");

            migrationBuilder.DropColumn(
                name: "progress_percent",
                table: "job");

            migrationBuilder.CreateIndex(
                name: "ix_job_pending_created_at",
                table: "job",
                column: "created_at",
                filter: "state = 'pending'");
        }
    }
}
