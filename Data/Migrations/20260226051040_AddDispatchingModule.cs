using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_customers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    contact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trips",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    truck_asset_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    pod_pending = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    hold_previous_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trips", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trips_assets_truck_asset_id",
                        column: x => x.truck_asset_id,
                        principalSchema: "dbo",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trips_dispatch_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trips_users_driver_user_id",
                        column: x => x.driver_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trip_documents",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doc_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    state = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    storage_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    verified_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_documents_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_documents_users_rejected_by_user_id",
                        column: x => x.rejected_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_documents_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_documents_users_verified_by_user_id",
                        column: x => x.verified_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trip_status_history",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    from_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_status_history_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_status_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trip_stops",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stop_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    location_text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    actual_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_stops", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_stops_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customers_name",
                schema: "dbo",
                table: "dispatch_customers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_rejected_by_user_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "rejected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type",
                schema: "dbo",
                table: "dispatch_trip_documents",
                columns: new[] { "trip_id", "doc_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_uploaded_by_user_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_verified_by_user_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_status_history_actor_user_id",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_status_history_trip_id",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_stops_trip_id",
                schema: "dbo",
                table: "dispatch_trip_stops",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_customer_id",
                schema: "dbo",
                table: "dispatch_trips",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_driver_user_id",
                schema: "dbo",
                table: "dispatch_trips",
                column: "driver_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_status",
                schema: "dbo",
                table: "dispatch_trips",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trips_truck_asset_id",
                schema: "dbo",
                table: "dispatch_trips",
                column: "truck_asset_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_trip_documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trip_status_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trip_stops",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trips",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_customers",
                schema: "dbo");
        }
    }
}
