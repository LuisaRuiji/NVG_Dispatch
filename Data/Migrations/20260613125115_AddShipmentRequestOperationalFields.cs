using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentRequestOperationalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "container_size",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_remarks",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trip_type",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "container_size",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "rejection_remarks",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "trip_type",
                schema: "dbo",
                table: "shipment_requests");
        }
    }
}
