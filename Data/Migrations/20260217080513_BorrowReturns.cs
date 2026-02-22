using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class BorrowReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_line_returns",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_returned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    return_condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    returned_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_line_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_line_returns_request_lines_request_line_id",
                        column: x => x.request_line_id,
                        principalSchema: "dbo",
                        principalTable: "request_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_request_line_returns_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_line_returns_actor_user_id",
                schema: "dbo",
                table: "request_line_returns",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_line_returns_request_line_id",
                schema: "dbo",
                table: "request_line_returns",
                column: "request_line_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_line_returns",
                schema: "dbo");
        }
    }
}
