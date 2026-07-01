using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_recommendations",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    completed_trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    truck_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recommended_trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    proximity_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    availability_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    truck_match_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    aging_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    total_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    rank = table.Column<int>(type: "int", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    was_accepted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    was_ignored = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_recommendations_dispatch_trips_completed_trip_id",
                        column: x => x.completed_trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_recommendations_dispatch_trips_recommended_trip_id",
                        column: x => x.recommended_trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_recommendations_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_completed_trip_id",
                schema: "dbo",
                table: "dispatch_recommendations",
                column: "completed_trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_driver_id",
                schema: "dbo",
                table: "dispatch_recommendations",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_expires_at_was_accepted_was_ignored",
                schema: "dbo",
                table: "dispatch_recommendations",
                columns: new[] { "expires_at", "was_accepted", "was_ignored" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_generated_at",
                schema: "dbo",
                table: "dispatch_recommendations",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_recommended_trip_id",
                schema: "dbo",
                table: "dispatch_recommendations",
                column: "recommended_trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recommendations_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_recommendations",
                column: "reviewed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_recommendations",
                schema: "dbo");
        }
    }
}
