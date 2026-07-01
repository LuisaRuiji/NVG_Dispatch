using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMustChangePasswordAndShipmentRequestReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "dbo",
                table: "users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "booking_number",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "container_number",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipping_line",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "booking_number",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "container_number",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "shipping_line",
                schema: "dbo",
                table: "shipment_requests");
        }
    }
}
