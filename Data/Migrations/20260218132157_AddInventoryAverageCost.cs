using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAverageCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "average_cost",
                schema: "dbo",
                table: "inventory",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "last_cost",
                schema: "dbo",
                table: "inventory",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "average_cost",
                schema: "dbo",
                table: "inventory");

            migrationBuilder.DropColumn(
                name: "last_cost",
                schema: "dbo",
                table: "inventory");
        }
    }
}
