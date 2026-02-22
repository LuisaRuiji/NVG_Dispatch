using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLogCostSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "unit_cost",
                schema: "dbo",
                table: "stock_logs",
                newName: "unit_cost_snapshot");

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost_snapshot",
                schema: "dbo",
                table: "stock_logs",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE dbo.stock_logs
                SET unit_cost_snapshot = NULL,
                    total_cost_snapshot = NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_cost_snapshot",
                schema: "dbo",
                table: "stock_logs");

            migrationBuilder.RenameColumn(
                name: "unit_cost_snapshot",
                schema: "dbo",
                table: "stock_logs",
                newName: "unit_cost");
        }
    }
}
