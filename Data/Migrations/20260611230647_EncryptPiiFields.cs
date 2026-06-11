using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncryptPiiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_encrypted",
                schema: "dbo",
                table: "suppliers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email_encrypted",
                schema: "dbo",
                table: "suppliers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone_encrypted",
                schema: "dbo",
                table: "suppliers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_encrypted",
                schema: "dbo",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "contact_email_encrypted",
                schema: "dbo",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "contact_phone_encrypted",
                schema: "dbo",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "address_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "contact_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "contact_email_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "contact_person_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "phone_encrypted",
                schema: "dbo",
                table: "dispatch_customers");
        }
    }
}
