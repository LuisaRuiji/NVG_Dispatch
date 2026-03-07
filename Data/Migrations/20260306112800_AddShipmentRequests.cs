using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "dbo",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shipment_requests",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    pickup_location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    dropoff_location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    requested_pickup_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cargo_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    cargo_weight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    special_instructions = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    converted_trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_requests_dispatch_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_requests_dispatch_trips_converted_trip_id",
                        column: x => x.converted_trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_requests_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_requests_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_request_documents",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    storage_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_request_documents_shipment_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "dbo",
                        principalTable: "shipment_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shipment_request_documents_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "roles",
                columns: new[] { "id", "name" },
                values: new object[] { 9, "Customer" });

            migrationBuilder.CreateIndex(
                name: "IX_users_customer_id",
                schema: "dbo",
                table: "users",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_request_documents_request_id",
                schema: "dbo",
                table: "shipment_request_documents",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_request_documents_request_id_document_type",
                schema: "dbo",
                table: "shipment_request_documents",
                columns: new[] { "request_id", "document_type" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_request_documents_uploaded_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_approved_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_converted_trip_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "converted_trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_created_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_customer_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_customer_id_status",
                schema: "dbo",
                table: "shipment_requests",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_status",
                schema: "dbo",
                table: "shipment_requests",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_users_dispatch_customers_customer_id",
                schema: "dbo",
                table: "users",
                column: "customer_id",
                principalSchema: "dbo",
                principalTable: "dispatch_customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_dispatch_customers_customer_id",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropTable(
                name: "shipment_request_documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "shipment_requests",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_users_customer_id",
                schema: "dbo",
                table: "users");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "roles",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "dbo",
                table: "users");
        }
    }
}
