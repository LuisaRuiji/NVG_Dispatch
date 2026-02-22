using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameLoanReturnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_loan_line_returns_users_actor_user_id",
                schema: "dbo",
                table: "loan_line_returns");

            migrationBuilder.DropColumn(
                name: "remarks",
                schema: "dbo",
                table: "loan_line_returns");

            migrationBuilder.RenameColumn(
                name: "qty_returned_increment",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "qty_returned");

            migrationBuilder.RenameColumn(
                name: "actor_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "received_by_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_loan_line_returns_actor_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "IX_loan_line_returns_received_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_loan_line_returns_users_received_by_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                column: "received_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_loan_line_returns_users_received_by_user_id",
                schema: "dbo",
                table: "loan_line_returns");

            migrationBuilder.RenameColumn(
                name: "received_by_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "actor_user_id");

            migrationBuilder.RenameColumn(
                name: "qty_returned",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "qty_returned_increment");

            migrationBuilder.RenameIndex(
                name: "IX_loan_line_returns_received_by_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                newName: "IX_loan_line_returns_actor_user_id");

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                schema: "dbo",
                table: "loan_line_returns",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_loan_line_returns_users_actor_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                column: "actor_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
