using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class ControlledDispatchLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finance_clearance_reason",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finance_clearance_status",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "finance_cleared_at",
                schema: "dbo",
                table: "shipment_requests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "finance_exception_at",
                schema: "dbo",
                table: "shipment_requests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finance_exception_reason",
                schema: "dbo",
                table: "shipment_requests",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quoted_amount",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "required_deposit_amount",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "verified_deposit_amount",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "verified_payment_amount",
                schema: "dbo",
                table: "shipment_requests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "carrier",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiry_date",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terminal_or_depot",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_state",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_role",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "related_attachment_id",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "container_condition",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "direction",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "document_event_at",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiry_date",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_proof_of_delivery",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terminal_or_depot",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "account_requested_at",
                schema: "dbo",
                table: "dispatch_customers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "account_reviewed_at",
                schema: "dbo",
                table: "dispatch_customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "account_status",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "account_status_reason",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_limit_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "credit_reviewed_at",
                schema: "dbo",
                table: "dispatch_customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_status",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "credit_terms_reason",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_overdue_balance",
                schema: "dbo",
                table: "dispatch_customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "outstanding_balance_encrypted",
                schema: "dbo",
                table: "dispatch_customers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_customers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "dbo",
                table: "audit_logs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                schema: "dbo",
                table: "audit_logs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "related_attachment_id",
                schema: "dbo",
                table: "audit_logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_finance_history",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shipment_request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    from_status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    payment_method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    reference_number = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    changed_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_finance_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_finance_history_shipment_requests_shipment_request_id",
                        column: x => x.shipment_request_id,
                        principalSchema: "dbo",
                        principalTable: "shipment_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_finance_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_container_quality_inspections",
                schema: "dbo",
                columns: table => new
                {
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    has_dents = table.Column<bool>(type: "bit", nullable: false),
                    has_holes = table.Column<bool>(type: "bit", nullable: false),
                    has_rust = table.Column<bool>(type: "bit", nullable: false),
                    has_odor = table.Column<bool>(type: "bit", nullable: false),
                    has_residue = table.Column<bool>(type: "bit", nullable: false),
                    has_stains = table.Column<bool>(type: "bit", nullable: false),
                    has_insects = table.Column<bool>(type: "bit", nullable: false),
                    is_clean = table.Column<bool>(type: "bit", nullable: false),
                    food_grade_required = table.Column<bool>(type: "bit", nullable: false),
                    food_grade_passed = table.Column<bool>(type: "bit", nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    inspected_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inspected_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_container_quality_inspections", x => x.trip_id);
                    table.ForeignKey(
                        name: "FK_dispatch_container_quality_inspections_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_container_quality_inspections_users_inspected_by_user_id",
                        column: x => x.inspected_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_container_quality_inspections_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_customer_account_history",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    from_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    from_credit_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    to_credit_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    changed_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_customer_account_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_customer_account_history_dispatch_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_customer_account_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_document_rules",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    milestone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    document_code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    alternative_group = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    is_required = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_document_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_document_rules_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_trip_operational_events",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    event_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    reference_number = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    related_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_trip_operational_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_operational_events_dispatch_trip_documents_related_document_id",
                        column: x => x.related_document_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trip_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_operational_events_dispatch_trips_trip_id",
                        column: x => x.trip_id,
                        principalSchema: "dbo",
                        principalTable: "dispatch_trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispatch_trip_operational_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_finance_clearance_status",
                schema: "dbo",
                table: "shipment_requests",
                column: "finance_clearance_status");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "finance_cleared_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_requests_finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "finance_exception_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_request_documents_verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customers_account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                column: "account_reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customers_account_status",
                schema: "dbo",
                table: "dispatch_customers",
                column: "account_status");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customers_credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                column: "credit_reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customers_credit_status",
                schema: "dbo",
                table: "dispatch_customers",
                column: "credit_status");

            migrationBuilder.CreateIndex(
                name: "IX_booking_finance_history_actor_user_id",
                schema: "dbo",
                table: "booking_finance_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_finance_history_shipment_request_id_changed_at",
                schema: "dbo",
                table: "booking_finance_history",
                columns: new[] { "shipment_request_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_container_quality_inspections_inspected_by_user_id",
                schema: "dbo",
                table: "dispatch_container_quality_inspections",
                column: "inspected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_container_quality_inspections_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_container_quality_inspections",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customer_account_history_actor_user_id",
                schema: "dbo",
                table: "dispatch_customer_account_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_customer_account_history_customer_id_changed_at",
                schema: "dbo",
                table: "dispatch_customer_account_history",
                columns: new[] { "customer_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_document_rules_trip_type_milestone_is_active",
                schema: "dbo",
                table: "dispatch_document_rules",
                columns: new[] { "trip_type", "milestone", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_document_rules_updated_by_user_id",
                schema: "dbo",
                table: "dispatch_document_rules",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_operational_events_actor_user_id",
                schema: "dbo",
                table: "dispatch_trip_operational_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_operational_events_related_document_id",
                schema: "dbo",
                table: "dispatch_trip_operational_events",
                column: "related_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_operational_events_trip_id_event_at",
                schema: "dbo",
                table: "dispatch_trip_operational_events",
                columns: new[] { "trip_id", "event_at" });

            // Preserve existing production records while introducing the controlled
            // account, finance, planning, and closure lanes. New records receive
            // their explicit initial states in the application services.
            migrationBuilder.Sql("""
                UPDATE dbo.dispatch_customers
                SET account_status = 'ActivePrepaid',
                    credit_status = 'NotGranted',
                    account_requested_at = CASE
                        WHEN created_at IS NULL THEN SYSUTCDATETIME()
                        ELSE created_at
                    END
                WHERE account_status = '';

                UPDATE dbo.shipment_requests
                SET finance_clearance_status = CASE
                        WHEN status IN ('APPROVED', 'CONVERTED', 'CONVERTED_TO_TRIP') THEN 'Cleared'
                        ELSE 'NotRequested'
                    END,
                    status = CASE
                        WHEN status = 'APPROVED' THEN 'CLEARED_FOR_PLANNING'
                        WHEN status = 'CONVERTED' THEN 'CONVERTED_TO_TRIP'
                        ELSE status
                    END
                WHERE finance_clearance_status = '';

                UPDATE dbo.dispatch_trips
                SET status = CASE
                        WHEN status = 'DELIVERED' THEN 'DELIVERY_COMPLETED'
                        WHEN status = 'CLOSED' THEN 'OPERATIONALLY_CLOSED'
                        ELSE status
                    END,
                    hold_previous_status = CASE
                        WHEN hold_previous_status = 'DELIVERED' THEN 'DELIVERY_COMPLETED'
                        WHEN hold_previous_status = 'CLOSED' THEN 'OPERATIONALLY_CLOSED'
                        ELSE hold_previous_status
                    END;

                UPDATE dbo.dispatch_trip_status_history
                SET from_status = CASE
                        WHEN from_status = 'DELIVERED' THEN 'DELIVERY_COMPLETED'
                        WHEN from_status = 'CLOSED' THEN 'OPERATIONALLY_CLOSED'
                        ELSE from_status
                    END,
                    to_status = CASE
                        WHEN to_status = 'DELIVERED' THEN 'DELIVERY_COMPLETED'
                        WHEN to_status = 'CLOSED' THEN 'OPERATIONALLY_CLOSED'
                        ELSE to_status
                    END;
                """);

            SeedDocumentRules(migrationBuilder);

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_customers_users_account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                column: "account_reviewed_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_customers_users_credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers",
                column: "credit_reviewed_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_request_documents_users_verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents",
                column: "verified_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_requests_users_finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "finance_cleared_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_requests_users_finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests",
                column: "finance_exception_by_user_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void SeedDocumentRules(MigrationBuilder migrationBuilder)
        {
            var rules = new (string TripType, string Milestone, string Code, string Scope, string Direction, string Alternative)[]
            {
                ("EXPORT_EMPTY_PICKUP", "PreDispatch", "BOOKING_REFERENCE", "BOOKING", "NotApplicable", null),
                ("EXPORT_EMPTY_PICKUP", "PreDispatch", "ATW", "SHIPMENT_OR_TRIP", "NotApplicable", "CARRIER_RELEASE"),
                ("EXPORT_EMPTY_PICKUP", "PreDispatch", "RELEASE_CONFIRMATION", "SHIPMENT", "NotApplicable", "CARRIER_RELEASE"),
                ("EXPORT_EMPTY_PICKUP", "OperationalClose", "EIR", "TRIP", "GateOut", null),
                ("EXPORT_EMPTY_PICKUP", "OperationalClose", "DTR", "TRIP", "NotApplicable", null),
                ("EXPORT_EMPTY_PICKUP", "OperationalClose", "CONTAINER_INSPECTION", "INSPECTION", "NotApplicable", null),
                ("EXPORT_EMPTY_PICKUP", "OperationalClose", "DR", "TRIP", "NotApplicable", "DELIVERY_PROOF"),
                ("EXPORT_EMPTY_PICKUP", "OperationalClose", "POD", "TRIP", "NotApplicable", "DELIVERY_PROOF"),
                ("EXPORT_LADEN_TO_TERMINAL", "PreDispatch", "BOOKING_REFERENCE", "BOOKING", "NotApplicable", "BOOKING_RELEASE"),
                ("EXPORT_LADEN_TO_TERMINAL", "PreDispatch", "BOOKING_CONFIRMATION", "SHIPMENT", "NotApplicable", "BOOKING_RELEASE"),
                ("EXPORT_LADEN_TO_TERMINAL", "PreDispatch", "RELEASE_CONFIRMATION", "SHIPMENT", "NotApplicable", "BOOKING_RELEASE"),
                ("EXPORT_LADEN_TO_TERMINAL", "OperationalClose", "EIR", "TRIP", "GateIn", null),
                ("EXPORT_LADEN_TO_TERMINAL", "OperationalClose", "DTR", "TRIP", "NotApplicable", null),
                ("EXPORT_LADEN_TO_TERMINAL", "OperationalClose", "GATE_EVIDENCE", "TRIP", "NotApplicable", null),
                ("IMPORT_LADEN_DELIVERY", "PreDispatch", "DELIVERY_ORDER", "SHIPMENT", "NotApplicable", "IMPORT_RELEASE"),
                ("IMPORT_LADEN_DELIVERY", "PreDispatch", "CRO", "SHIPMENT", "NotApplicable", "IMPORT_RELEASE"),
                ("IMPORT_LADEN_DELIVERY", "PreDispatch", "WEB_CRO", "SHIPMENT", "NotApplicable", "IMPORT_RELEASE"),
                ("IMPORT_LADEN_DELIVERY", "PreDispatch", "RELEASE_CONFIRMATION", "SHIPMENT", "NotApplicable", "IMPORT_RELEASE"),
                ("IMPORT_LADEN_DELIVERY", "OperationalClose", "EIR", "TRIP", "GateOut", null),
                ("IMPORT_LADEN_DELIVERY", "OperationalClose", "DTR", "TRIP", "NotApplicable", null),
                ("IMPORT_LADEN_DELIVERY", "OperationalClose", "DR", "TRIP", "NotApplicable", "DELIVERY_PROOF"),
                ("IMPORT_LADEN_DELIVERY", "OperationalClose", "POD", "TRIP", "NotApplicable", "DELIVERY_PROOF"),
                ("EMPTY_RETURN", "PreDispatch", "RETURN_DEPOT_AUTHORIZATION", "SHIPMENT", "NotApplicable", "RETURN_AUTH"),
                ("EMPTY_RETURN", "PreDispatch", "RETURN_INSTRUCTION", "SHIPMENT", "NotApplicable", "RETURN_AUTH"),
                ("EMPTY_RETURN", "OperationalClose", "EIR", "TRIP", "GateIn", null),
                ("EMPTY_RETURN", "OperationalClose", "DTR", "TRIP", "NotApplicable", null),
                ("EMPTY_RETURN", "OperationalClose", "RETURN_EVIDENCE", "TRIP", "NotApplicable", null)
            };

            foreach (var rule in rules)
            {
                migrationBuilder.InsertData(
                    schema: "dbo",
                    table: "dispatch_document_rules",
                    columns: new[] { "id", "trip_type", "milestone", "document_code", "scope", "direction", "alternative_group", "is_required", "is_active" },
                    values: new object[] { Guid.NewGuid(), rule.TripType, rule.Milestone, rule.Code, rule.Scope, rule.Direction, rule.Alternative, true, true });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_customers_users_account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_customers_users_credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropForeignKey(
                name: "FK_shipment_request_documents_users_verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_shipment_requests_users_finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shipment_requests_users_finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropTable(
                name: "booking_finance_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_container_quality_inspections",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_customer_account_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_document_rules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "dispatch_trip_operational_events",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_shipment_requests_finance_clearance_status",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropIndex(
                name: "IX_shipment_requests_finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropIndex(
                name: "IX_shipment_requests_finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropIndex(
                name: "IX_shipment_request_documents_verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_customers_account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_customers_account_status",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_customers_credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_customers_credit_status",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "finance_clearance_reason",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_clearance_status",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_cleared_at",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_cleared_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_exception_at",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_exception_by_user_id",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "finance_exception_reason",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "quoted_amount",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "required_deposit_amount",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "verified_deposit_amount",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "verified_payment_amount",
                schema: "dbo",
                table: "shipment_requests");

            migrationBuilder.DropColumn(
                name: "carrier",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "expiry_date",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "reference_number",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "terminal_or_depot",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "verification_state",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                schema: "dbo",
                table: "shipment_request_documents");

            migrationBuilder.DropColumn(
                name: "actor_role",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropColumn(
                name: "reference_number",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropColumn(
                name: "related_attachment_id",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropColumn(
                name: "carrier",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "container_condition",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "direction",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "document_event_at",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "expiry_date",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "is_proof_of_delivery",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "original_file_name",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "reference_number",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "terminal_or_depot",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "account_requested_at",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "account_reviewed_at",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "account_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "account_status",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "account_status_reason",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "credit_limit_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "credit_reviewed_at",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "credit_reviewed_by_user_id",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "credit_status",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "credit_terms_reason",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "has_overdue_balance",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "outstanding_balance_encrypted",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_customers");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "reference_number",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "related_attachment_id",
                schema: "dbo",
                table: "audit_logs");
        }
    }
}
