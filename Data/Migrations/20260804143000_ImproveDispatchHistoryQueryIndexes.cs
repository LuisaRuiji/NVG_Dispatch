using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations;

/// <summary>
/// Keeps the operational trip queue and newest-first audit log queries fast as
/// the retained history grows.
/// </summary>
[DbContext(typeof(InventoryDbContext))]
[Migration("20260804143000_ImproveDispatchHistoryQueryIndexes")]
public partial class ImproveDispatchHistoryQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_created_at",
            table: "audit_logs",
            column: "created_at",
            descending: new[] { true });

        migrationBuilder.CreateIndex(
            name: "IX_dispatch_trips_status_updated_at",
            table: "dispatch_trips",
            columns: new[] { "status", "updated_at" },
            descending: new[] { false, true });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_audit_logs_created_at",
            table: "audit_logs");

        migrationBuilder.DropIndex(
            name: "IX_dispatch_trips_status_updated_at",
            table: "dispatch_trips");
    }
}
