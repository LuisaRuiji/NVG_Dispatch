using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    public partial class AddStockLogIssueIdempotencyIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_ref_type_ref_id_inventory_id_movement_type",
                schema: "dbo",
                table: "stock_logs",
                columns: new[] { "ref_type", "ref_id", "inventory_id", "movement_type" },
                unique: true,
                filter: "[ref_type] = 'request' AND [movement_type] IN ('OUT', 'BORROW')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_logs_ref_type_ref_id_inventory_id_movement_type",
                schema: "dbo",
                table: "stock_logs");
        }
    }
}
