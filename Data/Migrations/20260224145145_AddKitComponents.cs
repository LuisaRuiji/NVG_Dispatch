using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKitComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kit_components",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    required_qty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    is_required = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kit_components", x => x.id);
                    table.ForeignKey(
                        name: "FK_kit_components_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kit_components_inventory_id",
                schema: "dbo",
                table: "kit_components",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_kit_components_inventory_id_name",
                schema: "dbo",
                table: "kit_components",
                columns: new[] { "inventory_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kit_components",
                schema: "dbo");
        }
    }
}
