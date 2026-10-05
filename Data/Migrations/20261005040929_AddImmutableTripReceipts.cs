using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutableTripReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_trip_receipts",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    receipt_number = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    container_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    booking_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    pickup_location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    dropoff_location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    base_charge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    discount_percent = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    discount_reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    tax_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    payment_reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    is_reversal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    reverses_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    correction_reason = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    generated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_receipts_dispatch_trip_receipts_reverses_receipt_id",
                        column: x => x.reverses_receipt_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trip_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_receipts_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_receipts_users_generated_by_user_id",
                        column: x => x.generated_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trip_receipt_charges",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    description = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_receipt_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_receipt_charges_dispatch_trip_receipts_trip_receipt_id",
                        column: x => x.trip_receipt_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trip_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_receipt_charges_trip_receipt_id",
                schema: "dbo",
                table: "dispatch_trip_receipt_charges",
                column: "trip_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_receipts_generated_by_user_id",
                schema: "dbo",
                table: "dispatch_trip_receipts",
                column: "generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_receipts_receipt_number",
                schema: "dbo",
                table: "dispatch_trip_receipts",
                column: "receipt_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_receipts_reverses_receipt_id",
                schema: "dbo",
                table: "dispatch_trip_receipts",
                column: "reverses_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_receipts_trip_id_generated_at",
                schema: "dbo",
                table: "dispatch_trip_receipts",
                columns: new[] { "trip_id", "generated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_trip_receipt_charges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trip_receipts",
                schema: "dbo");
        }
    }
}
