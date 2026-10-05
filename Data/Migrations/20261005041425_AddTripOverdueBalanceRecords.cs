using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripOverdueBalanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_trip_overdue_balance_records",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_overdue_balance_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_overdue_balance_records_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_overdue_balance_records_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_overdue_balance_records_customer_id_recorded_at",
                schema: "dbo",
                table: "dispatch_trip_overdue_balance_records",
                columns: new[] { "customer_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_overdue_balance_records_recorded_by_user_id",
                schema: "dbo",
                table: "dispatch_trip_overdue_balance_records",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_overdue_balance_records_trip_id",
                schema: "dbo",
                table: "dispatch_trip_overdue_balance_records",
                column: "trip_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_trip_overdue_balance_records",
                schema: "dbo");
        }
    }
}
