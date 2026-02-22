using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "supplier_name",
                schema: "dbo",
                table: "purchase_orders");

            migrationBuilder.AddColumn<int>(
                name: "supplier_id",
                schema: "dbo",
                table: "purchase_orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                schema: "dbo",
                table: "purchase_orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
