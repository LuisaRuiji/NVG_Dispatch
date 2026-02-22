using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "roles",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 6, "Admin" },
                    { 7, "SuperAdmin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "roles",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "roles",
                keyColumn: "id",
                keyValue: 7);
        }
    }
}
