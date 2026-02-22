using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_requests_status_created_at",
                schema: "dbo",
                table: "requests",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_loans_status_issued_at",
                schema: "dbo",
                table: "loans",
                columns: new[] { "status", "issued_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_requests_status_created_at",
                schema: "dbo",
                table: "requests");

            migrationBuilder.DropIndex(
                name: "IX_loans_status_issued_at",
                schema: "dbo",
                table: "loans");
        }
    }
}
