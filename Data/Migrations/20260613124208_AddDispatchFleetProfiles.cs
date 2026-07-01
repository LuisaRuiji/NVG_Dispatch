using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchFleetProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_drivers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    license_number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_drivers", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_drivers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trailers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trailer_code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    container_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trailers", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trailers_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "dbo",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trucks",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plate_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    container_capability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trucks", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trucks_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "dbo",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_drivers_license_number",
                schema: "dbo",
                table: "dispatch_drivers",
                column: "license_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_drivers_user_id",
                schema: "dbo",
                table: "dispatch_drivers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trailers_asset_id",
                schema: "dbo",
                table: "dispatch_trailers",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trailers_trailer_code",
                schema: "dbo",
                table: "dispatch_trailers",
                column: "trailer_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trucks_asset_id",
                schema: "dbo",
                table: "dispatch_trucks",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trucks_plate_number",
                schema: "dbo",
                table: "dispatch_trucks",
                column: "plate_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_drivers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trailers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trucks",
                schema: "dbo");
        }
    }
}
