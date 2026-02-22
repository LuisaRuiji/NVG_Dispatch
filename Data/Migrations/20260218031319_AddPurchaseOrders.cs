using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    supplier_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_ordered = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    qty_received = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "dbo",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipts",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    received_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipts_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "dbo",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipts_users_received_by_user_id",
                        column: x => x.received_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipt_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "IX_purchase_order_lines_inventory_id",
                schema: "dbo",
                table: "purchase_order_lines",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_purchase_order_id",
                schema: "dbo",
                table: "purchase_order_lines",
                column: "purchase_order_id");

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
                name: "IX_purchase_order_receipts_purchase_order_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_received_by_user_id",
                schema: "dbo",
                table: "purchase_order_receipts",
                column: "received_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_created_by_user_id",
                schema: "dbo",
                table: "purchase_orders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_status",
                schema: "dbo",
                table: "purchase_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_status_created_at",
                schema: "dbo",
                table: "purchase_orders",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_receipt_lines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "purchase_order_receipts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "dbo");
        }
    }
}
