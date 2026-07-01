using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedWaybills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_waybills",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    waybill_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    generated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    waybill_data_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_waybills", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_waybills_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_generated_waybills_users_generated_by_user_id",
                        column: x => x.generated_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_waybills_generated_by_user_id",
                schema: "dbo",
                table: "generated_waybills",
                column: "generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_generated_waybills_trip_id",
                schema: "dbo",
                table: "generated_waybills",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_generated_waybills_trip_id_is_active",
                schema: "dbo",
                table: "generated_waybills",
                columns: new[] { "trip_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_waybills_waybill_number",
                schema: "dbo",
                table: "generated_waybills",
                column: "waybill_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_waybills",
                schema: "dbo");
        }
    }
}
