using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoansAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_line_returns",
                schema: "dbo");

            migrationBuilder.CreateTable(
                name: "loans",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    borrower_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    due_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.id);
                    table.ForeignKey(
                        name: "FK_loans_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "dbo",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loans_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "dbo",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loans_users_borrower_user_id",
                        column: x => x.borrower_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    loan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_issued = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    qty_returned = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_lines_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_lines_loans_loan_id",
                        column: x => x.loan_id,
                        principalSchema: "dbo",
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_line_returns",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    loan_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_returned_increment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    return_condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    missing_components_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    returned_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_line_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_line_returns_loan_lines_loan_line_id",
                        column: x => x.loan_line_id,
                        principalSchema: "dbo",
                        principalTable: "loan_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_line_returns_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_ref_type_ref_id_inventory_id_movement_type",
                schema: "dbo",
                table: "stock_logs",
                columns: new[] { "ref_type", "ref_id", "inventory_id", "movement_type" },
                unique: true,
                filter: "[ref_type] = 'request' AND [movement_type] IN ('OUT', 'BORROW')");

            migrationBuilder.CreateIndex(
                name: "IX_loan_line_returns_actor_user_id",
                schema: "dbo",
                table: "loan_line_returns",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_line_returns_loan_line_id",
                schema: "dbo",
                table: "loan_line_returns",
                column: "loan_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_lines_inventory_id",
                schema: "dbo",
                table: "loan_lines",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_lines_loan_id",
                schema: "dbo",
                table: "loan_lines",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loans_asset_id",
                schema: "dbo",
                table: "loans",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_loans_borrower_user_id",
                schema: "dbo",
                table: "loans",
                column: "borrower_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_loans_request_id",
                schema: "dbo",
                table: "loans",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loans_status",
                schema: "dbo",
                table: "loans",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_line_returns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "loan_lines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "loans",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_stock_logs_ref_type_ref_id_inventory_id_movement_type",
                schema: "dbo",
                table: "stock_logs");

            migrationBuilder.CreateTable(
                name: "request_line_returns",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    return_condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    qty_returned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
    }
}
