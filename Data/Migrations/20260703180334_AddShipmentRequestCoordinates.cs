using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    public partial class AddShipmentRequestCoordinates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddOrConvertCoordinateColumn(migrationBuilder, "dropoff_latitude");
            AddOrConvertCoordinateColumn(migrationBuilder, "dropoff_longitude");
            AddOrConvertCoordinateColumn(migrationBuilder, "pickup_latitude");
            AddOrConvertCoordinateColumn(migrationBuilder, "pickup_longitude");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "dropoff_latitude", schema: "dbo", table: "shipment_requests");
            migrationBuilder.DropColumn(name: "dropoff_longitude", schema: "dbo", table: "shipment_requests");
            migrationBuilder.DropColumn(name: "pickup_latitude", schema: "dbo", table: "shipment_requests");
            migrationBuilder.DropColumn(name: "pickup_longitude", schema: "dbo", table: "shipment_requests");
        }

        private static void AddOrConvertCoordinateColumn(MigrationBuilder migrationBuilder, string column)
        {
            migrationBuilder.Sql($"""
                IF COL_LENGTH(N'dbo.shipment_requests', N'{column}') IS NULL
                    ALTER TABLE [dbo].[shipment_requests] ADD [{column}] decimal(9,6) NULL;
                ELSE
                    ALTER TABLE [dbo].[shipment_requests] ALTER COLUMN [{column}] decimal(9,6) NULL;
                """);
        }
    }
}
