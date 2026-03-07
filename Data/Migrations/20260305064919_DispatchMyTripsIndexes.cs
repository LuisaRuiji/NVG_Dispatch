using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class DispatchMyTripsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_driver_user_id_status",
                schema: "dbo",
                table: "dispatch_trips",
                columns: new[] { "driver_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_stops_trip_id_stop_type_scheduled_at",
                schema: "dbo",
                table: "dispatch_trip_stops",
                columns: new[] { "trip_id", "stop_type", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_status_history_trip_id_event_at",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                columns: new[] { "trip_id", "event_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dispatch_trips_driver_user_id_status",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_stops_trip_id_stop_type_scheduled_at",
                schema: "dbo",
                table: "dispatch_trip_stops");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_status_history_trip_id_event_at",
                schema: "dbo",
                table: "dispatch_trip_status_history");
        }
    }
}
