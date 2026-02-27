using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchHistoryEventTypeAndAuditTraceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_type",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "STATUS_CHANGE");

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                schema: "dbo",
                table: "audit_logs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "event_type",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropColumn(
                name: "trace_id",
                schema: "dbo",
                table: "audit_logs");
        }
    }
}
