using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations;

/// <summary>
/// Supports the role, activity, and newest-first filters used by the audit
/// activity ledger without indexing the large JSON payload columns.
/// </summary>
[DbContext(typeof(InventoryDbContext))]
[Migration("20260804173000_OptimizeAuditLogQueries")]
public partial class OptimizeAuditLogQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_audit_logs_actor_user_id",
            table: "audit_logs");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_action_created_at",
            table: "audit_logs",
            columns: new[] { "action", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_actor_user_id_created_at",
            table: "audit_logs",
            columns: new[] { "actor_user_id", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_entity_type_created_at",
            table: "audit_logs",
            columns: new[] { "entity_type", "created_at" },
            descending: new[] { false, true });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_audit_logs_action_created_at",
            table: "audit_logs");

        migrationBuilder.DropIndex(
            name: "IX_audit_logs_actor_user_id_created_at",
            table: "audit_logs");

        migrationBuilder.DropIndex(
            name: "IX_audit_logs_entity_type_created_at",
            table: "audit_logs");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_actor_user_id",
            table: "audit_logs",
            column: "actor_user_id");
    }
}
