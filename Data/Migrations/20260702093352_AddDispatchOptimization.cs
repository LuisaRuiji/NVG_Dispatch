using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "last_latitude",
                schema: "dbo",
                table: "dispatch_trucks",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_location_at",
                schema: "dbo",
                table: "dispatch_trucks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "last_longitude",
                schema: "dbo",
                table: "dispatch_trucks",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                schema: "dbo",
                table: "dispatch_trip_stops",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                schema: "dbo",
                table: "dispatch_trip_stops",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dispatch_optimization_plans",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    scheduled_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    algorithm_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_complete_solution = table.Column<bool>(type: "bit", nullable: false),
                    search_iterations = table.Column<int>(type: "int", nullable: false),
                    max_iterations = table.Column<int>(type: "int", nullable: false),
                    max_candidates_per_state = table.Column<int>(type: "int", nullable: false),
                    max_execution_seconds = table.Column<int>(type: "int", nullable: false),
                    total_estimated_distance_km = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_estimated_empty_mileage_km = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_estimated_travel_minutes = table.Column<int>(type: "int", nullable: false),
                    total_lateness_risk = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    final_state_cost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_optimization_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_plans_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_plans_users_generated_by_user_id",
                        column: x => x.generated_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "optimization_weight_settings",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    empty_travel_time_weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.30m),
                    added_route_time_weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.25m),
                    lateness_risk_weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.25m),
                    workload_balance_weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.10m),
                    priority_urgency_weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.10m),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_optimization_weight_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_optimization_weight_settings_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_optimization_routes",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_truck_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dispatch_driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sequence_number = table.Column<int>(type: "int", nullable: false),
                    estimated_distance_km = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    estimated_empty_mileage_km = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    estimated_travel_minutes = table.Column<int>(type: "int", nullable: false),
                    workload_score = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    route_cost = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_optimization_routes", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_routes_dispatch_drivers_dispatch_driver_id",
                        column: x => x.dispatch_driver_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_routes_dispatch_optimization_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_optimization_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_routes_dispatch_trucks_dispatch_truck_id",
                        column: x => x.dispatch_truck_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trucks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_optimization_route_stops",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    route_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stop_order = table.Column<int>(type: "int", nullable: false),
                    estimated_arrival_at_pickup = table.Column<DateTime>(type: "datetime2", nullable: true),
                    estimated_arrival_at_dropoff = table.Column<DateTime>(type: "datetime2", nullable: true),
                    empty_travel_minutes_to_pickup = table.Column<int>(type: "int", nullable: false),
                    loaded_travel_minutes_to_dropoff = table.Column<int>(type: "int", nullable: false),
                    lateness_risk_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    priority_urgency_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    assignment_score = table.Column<decimal>(type: "decimal(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_optimization_route_stops", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_route_stops_dispatch_optimization_routes_route_id",
                        column: x => x.route_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_optimization_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_optimization_route_stops_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_plans_approved_by_user_id",
                schema: "dbo",
                table: "dispatch_optimization_plans",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_plans_generated_by_user_id",
                schema: "dbo",
                table: "dispatch_optimization_plans",
                column: "generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_route_stops_route_id",
                schema: "dbo",
                table: "dispatch_optimization_route_stops",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_route_stops_trip_id",
                schema: "dbo",
                table: "dispatch_optimization_route_stops",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_routes_dispatch_driver_id",
                schema: "dbo",
                table: "dispatch_optimization_routes",
                column: "dispatch_driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_routes_dispatch_truck_id",
                schema: "dbo",
                table: "dispatch_optimization_routes",
                column: "dispatch_truck_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_optimization_routes_plan_id",
                schema: "dbo",
                table: "dispatch_optimization_routes",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_optimization_weight_settings_created_by_user_id",
                schema: "dbo",
                table: "optimization_weight_settings",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_optimization_route_stops",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "optimization_weight_settings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_optimization_routes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_optimization_plans",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "last_latitude",
                schema: "dbo",
                table: "dispatch_trucks");

            migrationBuilder.DropColumn(
                name: "last_location_at",
                schema: "dbo",
                table: "dispatch_trucks");

            migrationBuilder.DropColumn(
                name: "last_longitude",
                schema: "dbo",
                table: "dispatch_trucks");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "dbo",
                table: "dispatch_trip_stops");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "dbo",
                table: "dispatch_trip_stops");
        }
    }
}
