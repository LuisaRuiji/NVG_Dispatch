using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncryptFinancialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allowance_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_amount_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_price_per_liter_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "official_receipt_number_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payroll_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rate_encrypted",
                schema: "dbo",
                table: "dispatch_trips",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allowance_encrypted",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "fuel_amount_encrypted",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "fuel_price_per_liter_encrypted",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "official_receipt_number_encrypted",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "payroll_encrypted",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "rate_encrypted",
                schema: "dbo",
                table: "dispatch_trips");
        }
    }
}
