using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    asset_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    plate_no = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    item_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    is_kit = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    reorder_level = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    unit_value = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                schema: "dbo",
                columns: table => new
                {
                    workflow_key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.workflow_key);
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    purpose = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    issued_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_requests_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "dbo",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requests_users_requester_user_id",
                        column: x => x.requester_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_logs",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    movement_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_delta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ref_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ref_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    meta_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_logs_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "dbo",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "dbo",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approvals",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    workflow_key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    current_step = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_approvals_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approvals_workflows_workflow_key",
                        column: x => x.workflow_key,
                        principalSchema: "dbo",
                        principalTable: "workflows",
                        principalColumn: "workflow_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    workflow_key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    step_order = table.Column<int>(type: "int", nullable: false),
                    required_role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_steps_workflows_workflow_key",
                        column: x => x.workflow_key,
                        principalSchema: "dbo",
                        principalTable: "workflows",
                        principalColumn: "workflow_key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_lines",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    qty_requested = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    qty_approved = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_lines_inventory_inventory_id",
                        column: x => x.inventory_id,
                        principalSchema: "dbo",
                        principalTable: "inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_request_lines_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "dbo",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_actions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    approval_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    step_order = table.Column<int>(type: "int", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    acted_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_actions_approvals_approval_id",
                        column: x => x.approval_id,
                        principalSchema: "dbo",
                        principalTable: "approvals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_approval_actions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "roles",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "InventoryOfficer" },
                    { 2, "Manager" },
                    { 3, "HeadOfFinance" },
                    { 4, "CEO" },
                    { 5, "Driver" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "workflows",
                columns: new[] { "workflow_key", "is_active", "name" },
                values: new object[,]
                {
                    { "ADJUSTMENT_APPROVAL", true, "Adjustment Approval" },
                    { "BORROW_APPROVAL", true, "Borrow Approval" },
                    { "MAINTENANCE_ISSUE_APPROVAL", true, "Maintenance Issue Approval" },
                    { "PO_APPROVAL", true, "PO Approval" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "workflow_steps",
                columns: new[] { "id", "created_at", "required_role", "step_order", "workflow_key" },
                values: new object[,]
                {
                    { new Guid("1b6e7c02-8132-4f2f-b53b-8db168a5e407"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "Manager", 1, "PO_APPROVAL" },
                    { new Guid("1cf3b1a4-f741-4e0c-9c64-57f2a9aa1c02"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "Manager", 2, "MAINTENANCE_ISSUE_APPROVAL" },
                    { new Guid("39d4f82f-4ed3-4bfa-91a9-ffcb28e1a103"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "InventoryOfficer", 1, "BORROW_APPROVAL" },
                    { new Guid("5d4e1f9f-8e90-46a6-9d91-566a1e3b1d01"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "InventoryOfficer", 1, "MAINTENANCE_ISSUE_APPROVAL" },
                    { new Guid("6d49fedd-f2af-4fd0-89ae-1e1bb9c8c204"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "Manager", 2, "BORROW_APPROVAL" },
                    { new Guid("8e6f8b8e-6a64-4f4f-8b78-5d7dd6eb8706"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "Manager", 2, "ADJUSTMENT_APPROVAL" },
                    { new Guid("a9f0f9ce-c8c9-47aa-8a71-427f1a6d2b08"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "HeadOfFinance", 2, "PO_APPROVAL" },
                    { new Guid("b9bb7d7d-b2f0-4bb2-91b5-50f2c8188909"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "CEO", 3, "PO_APPROVAL" },
                    { new Guid("f2eb4a0f-3195-40cd-9050-5b7b9352e705"), new DateTime(2026, 2, 17, 0, 0, 0, 0, DateTimeKind.Utc), "InventoryOfficer", 1, "ADJUSTMENT_APPROVAL" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_actor_user_id",
                schema: "dbo",
                table: "approval_actions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_approval_id",
                schema: "dbo",
                table: "approval_actions",
                column: "approval_id");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_created_by",
                schema: "dbo",
                table: "approvals",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_entity_type_entity_id",
                schema: "dbo",
                table: "approvals",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approvals_status",
                schema: "dbo",
                table: "approvals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_workflow_key",
                schema: "dbo",
                table: "approvals",
                column: "workflow_key");

            migrationBuilder.CreateIndex(
                name: "IX_assets_asset_code",
                schema: "dbo",
                table: "assets",
                column: "asset_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_item_type",
                schema: "dbo",
                table: "inventory",
                column: "item_type");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_name",
                schema: "dbo",
                table: "inventory",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_request_lines_inventory_id",
                schema: "dbo",
                table: "request_lines",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_lines_request_id",
                schema: "dbo",
                table: "request_lines",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_requests_asset_id",
                schema: "dbo",
                table: "requests",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_requests_request_type_status",
                schema: "dbo",
                table: "requests",
                columns: new[] { "request_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_requester_user_id",
                schema: "dbo",
                table: "requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "dbo",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_actor_user_id",
                schema: "dbo",
                table: "stock_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_inventory_id_created_at",
                schema: "dbo",
                table: "stock_logs",
                columns: new[] { "inventory_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_ref_type_ref_id",
                schema: "dbo",
                table: "stock_logs",
                columns: new[] { "ref_type", "ref_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "dbo",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                schema: "dbo",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_workflow_key_step_order",
                schema: "dbo",
                table: "workflow_steps",
                columns: new[] { "workflow_key", "step_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_actions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "request_lines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "stock_logs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "workflow_steps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "approvals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "requests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "inventory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "workflows",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "users",
                schema: "dbo");
        }
    }
}
