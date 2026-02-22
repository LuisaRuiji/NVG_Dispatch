using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_adjustments",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rejection_reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_adjustment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_delta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_inventory_adjustments_inventory_adjustment_id",
                        column: x => x.inventory_adjustment_id,
                        principalSchema: "dbo",
                        principalTable: "inventory_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_inventory_adjustment_id",
                schema: "dbo",
                table: "inventory_adjustment_lines",
                column: "inventory_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_inventory_adjustment_id_inventory_id",
                schema: "dbo",
                table: "inventory_adjustment_lines",
                columns: new[] { "inventory_adjustment_id", "inventory_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_inventory_id",
                schema: "dbo",
                table: "inventory_adjustment_lines",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_created_by_user_id",
                schema: "dbo",
                table: "inventory_adjustments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_status",
                schema: "dbo",
                table: "inventory_adjustments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_status_created_at",
                schema: "dbo",
                table: "inventory_adjustments",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_adjustment_lines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "inventory_adjustments",
                schema: "dbo");
        }
    }
}
