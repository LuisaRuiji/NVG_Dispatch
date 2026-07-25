using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "last_latitude",
                schema: "dbo",
                table: "dispatch_trips",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_location_at",
                schema: "dbo",
                table: "dispatch_trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "last_longitude",
                schema: "dbo",
                table: "dispatch_trips",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "location_tracking_sessions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_truck_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ended_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    start_latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    start_longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    end_latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    end_longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_tracking_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_location_tracking_sessions_dispatch_drivers_dispatch_driver_id",
                        column: x => x.dispatch_driver_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_location_tracking_sessions_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_location_tracking_sessions_dispatch_trucks_dispatch_truck_id",
                        column: x => x.dispatch_truck_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trucks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "driver_location_updates",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tracking_session_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_truck_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    accuracy_meters = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    speed_kph = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    heading = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_location_updates", x => x.id);
                    table.ForeignKey(
                        name: "FK_driver_location_updates_dispatch_drivers_dispatch_driver_id",
                        column: x => x.dispatch_driver_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_driver_location_updates_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_driver_location_updates_dispatch_trucks_dispatch_truck_id",
                        column: x => x.dispatch_truck_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trucks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_driver_location_updates_location_tracking_sessions_tracking_session_id",
                        column: x => x.tracking_session_id,
                        principalSchema: "dbo",
                        principalTable: "location_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_driver_location_updates_dispatch_driver_id_recorded_at",
                schema: "dbo",
                table: "driver_location_updates",
                columns: new[] { "dispatch_driver_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_location_updates_dispatch_truck_id_recorded_at",
                schema: "dbo",
                table: "driver_location_updates",
                columns: new[] { "dispatch_truck_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_location_updates_tracking_session_id",
                schema: "dbo",
                table: "driver_location_updates",
                column: "tracking_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_driver_location_updates_trip_id_recorded_at",
                schema: "dbo",
                table: "driver_location_updates",
                columns: new[] { "trip_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_location_tracking_sessions_dispatch_driver_id_status",
                schema: "dbo",
                table: "location_tracking_sessions",
                columns: new[] { "dispatch_driver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_location_tracking_sessions_dispatch_truck_id",
                schema: "dbo",
                table: "location_tracking_sessions",
                column: "dispatch_truck_id");

            migrationBuilder.CreateIndex(
                name: "IX_location_tracking_sessions_trip_id_status",
                schema: "dbo",
                table: "location_tracking_sessions",
                columns: new[] { "trip_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_location_updates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "location_tracking_sessions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "last_latitude",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "last_location_at",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "last_longitude",
                schema: "dbo",
                table: "dispatch_trips");
        }
    }
}
