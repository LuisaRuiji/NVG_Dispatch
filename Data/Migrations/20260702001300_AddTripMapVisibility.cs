using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripMapVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "dropoff_latitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "dropoff_longitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pickup_latitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pickup_longitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "dbo",
                table: "dispatch_trip_stops",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "dbo",
                table: "dispatch_trip_stops",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dispatch_trip_location_pings",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    latitude = table.Column<double>(type: "float", nullable: false),
                    longitude = table.Column<double>(type: "float", nullable: false),
                    accuracy_meters = table.Column<double>(type: "float", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_location_pings", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_location_pings_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_location_pings_users_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_location_pings_driver_id_recorded_at",
                schema: "dbo",
                table: "dispatch_trip_location_pings",
                columns: new[] { "driver_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_location_pings_trip_id_recorded_at",
                schema: "dbo",
                table: "dispatch_trip_location_pings",
                columns: new[] { "trip_id", "recorded_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_trip_location_pings",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "dropoff_latitude",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "dropoff_longitude",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "pickup_latitude",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "pickup_longitude",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "dbo",
                table: "dispatch_trip_stops");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "dbo",
                table: "dispatch_trip_stops");
        }
    }
}
