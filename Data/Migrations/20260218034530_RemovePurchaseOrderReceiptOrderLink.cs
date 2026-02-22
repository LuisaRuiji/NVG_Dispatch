using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePurchaseOrderReceiptOrderLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_receipts_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "PurchaseOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "PurchaseOrderId",
                principalSchema: "dbo",
                principalTable: "purchase_orders",
                principalColumn: "id");
        }
    }
}
