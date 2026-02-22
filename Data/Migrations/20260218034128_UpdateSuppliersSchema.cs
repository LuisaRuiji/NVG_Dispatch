using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSuppliersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_suppliers_supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropTable(
                name: "purchase_order_receipt_lines",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "remarks",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.RenameColumn(
                name: "purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                newName: "PurchaseOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_order_receipts_purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                newName: "IX_purchase_order_receipts_PurchaseOrderId");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                schema: "dbo",
                table: "purchase_orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "dbo",
                table: "purchase_orders",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    contact_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    contact_phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    contact_email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "qty_received_increment",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                schema: "dbo",
                table: "purchase_order_lines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_purchase_order_id_inventory_id",
                schema: "dbo",
                table: "purchase_order_lines",
                columns: new[] { "purchase_order_id", "inventory_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_name",
                schema: "dbo",
                table: "suppliers",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_receipts_purchase_order_lines_purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "purchase_order_line_id",
                principalSchema: "dbo",
                principalTable: "purchase_order_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "PurchaseOrderId",
                principalSchema: "dbo",
                principalTable: "purchase_orders",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_suppliers_supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                column: "supplier_id",
                principalSchema: "dbo",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_suppliers_supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_receipts_purchase_order_lines_purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_receipts_purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_lines_purchase_order_id_inventory_id",
                schema: "dbo",
                table: "purchase_order_lines");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropColumn(
                name: "qty_received_increment",
                schema: "dbo",
                table: "purchase_order_receipts");

            migrationBuilder.DropColumn(
                name: "unit_price",
                schema: "dbo",
                table: "purchase_order_lines");

            migrationBuilder.RenameColumn(
                name: "PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                newName: "purchase_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_order_receipts_PurchaseOrderId",
                schema: "dbo",
                table: "purchase_order_receipts",
                newName: "IX_purchase_order_receipts_purchase_order_id");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.AddColumn<int>(
                name: "supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                schema: "dbo",
                table: "purchase_order_receipts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipt_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_received = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_receipt_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_lines_purchase_order_lines_purchase_order_line_id",
                        column: x => x.purchase_order_line_id,
                        principalSchema: "dbo",
                        principalTable: "purchase_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_lines_purchase_order_receipts_purchase_order_receipt_id",
                        column: x => x.purchase_order_receipt_id,
                        principalSchema: "dbo",
                        principalTable: "purchase_order_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_lines_purchase_order_line_id",
                schema: "dbo",
                table: "purchase_order_receipt_lines",
                column: "purchase_order_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_lines_purchase_order_receipt_id",
                schema: "dbo",
                table: "purchase_order_receipt_lines",
                column: "purchase_order_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_name",
                schema: "dbo",
                table: "suppliers",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_receipts_purchase_orders_purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "purchase_order_id",
                principalSchema: "dbo",
                principalTable: "purchase_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_suppliers_supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                column: "supplier_id",
                principalSchema: "dbo",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
