using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripTrailerAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips",
                column: "trailer_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_trailer_asset_id_status",
                schema: "dbo",
                table: "dispatch_trips",
                columns: new[] { "trailer_asset_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_trips_assets_trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips",
                column: "trailer_asset_id",
                principalSchema: "dbo",
                principalTable: "assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_trips_assets_trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trips_trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trips_trailer_asset_id_status",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "trailer_asset_id",
                schema: "dbo",
                table: "dispatch_trips");
        }
    }
}
