using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchTripOperationalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "booking_number",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "container_number",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "container_size",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eir_number",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipping_line",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trip_type",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "waybill_number",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booking_number",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "container_number",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "container_size",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "eir_number",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "shipping_line",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "trip_type",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "waybill_number",
                schema: "dbo",
                table: "dispatch_trips");
        }
    }
}
