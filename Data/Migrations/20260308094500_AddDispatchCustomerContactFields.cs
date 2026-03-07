using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchCustomerContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "contact_person",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "dbo",
                table: "dispatch_customers");
        }
    }
}
