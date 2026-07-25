using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentRequestCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "dropoff_latitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "dropoff_longitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pickup_latitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pickup_longitude",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(9,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
