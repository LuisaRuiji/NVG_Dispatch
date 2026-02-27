using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "module_settings",
                schema: "dbo",
                columns: table => new
                {
                    module_key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    updated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_settings", x => x.module_key);
                    table.ForeignKey(
                        name: "FK_module_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_module_settings_updated_by_user_id",
                schema: "dbo",
                table: "module_settings",
                column: "updated_by_user_id");

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "module_settings",
                columns: new[] { "module_key", "is_enabled" },
                values: new object[,]
                {
                    { "auth", true },
                    { "users", true },
                    { "assets", true },
                    { "inventory", true },
                    { "inventory-adjustments", true },
                    { "loans", true },
                    { "purchase-orders", true },
                    { "reports", true },
                    { "requests", true },
                    { "suppliers", true },
                    { "approvals", true },
                    { "dispatch", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_settings",
                schema: "dbo");
        }
    }
}
