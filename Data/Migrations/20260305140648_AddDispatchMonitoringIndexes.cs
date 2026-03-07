using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchMonitoringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_truck_asset_id_status",
                schema: "dbo",
                table: "dispatch_trips",
                columns: new[] { "truck_asset_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_stops_stop_type_scheduled_at",
                schema: "dbo",
                table: "dispatch_trip_stops",
                columns: new[] { "stop_type", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_is_active_state",
                schema: "dbo",
                table: "dispatch_trip_documents",
                columns: new[] { "trip_id", "doc_type", "is_active", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_state",
                schema: "dbo",
                table: "dispatch_trip_documents",
                columns: new[] { "trip_id", "doc_type", "state" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dispatch_trips_truck_asset_id_status",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_stops_stop_type_scheduled_at",
                schema: "dbo",
                table: "dispatch_trip_stops");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_is_active_state",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_state",
                schema: "dbo",
                table: "dispatch_trip_documents");
        }
    }
}
