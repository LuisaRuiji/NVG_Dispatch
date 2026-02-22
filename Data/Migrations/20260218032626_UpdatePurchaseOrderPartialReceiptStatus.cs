using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePurchaseOrderPartialReceiptStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.purchase_orders SET status = 'PARTIALLY_RECEIVED' WHERE status = 'RECEIVED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.purchase_orders SET status = 'RECEIVED' WHERE status = 'PARTIALLY_RECEIVED'");
        }
    }
}
