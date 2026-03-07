using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogContextFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_role",
                schema: "dbo",
                table: "audit_logs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trip_id",
                schema: "dbo",
                table: "audit_logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_trip_id",
                schema: "dbo",
                table: "audit_logs",
                column: "trip_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_trip_id",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_role",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "trip_id",
                schema: "dbo",
                table: "audit_logs");
        }
    }
}
