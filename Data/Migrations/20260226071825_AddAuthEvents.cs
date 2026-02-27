using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_events",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    reason_code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    roles_snapshot_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    auth_method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    mfa_performed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    mfa_method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    session_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    token_jti = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    correlation_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    client_app = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    environment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_events_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_events_created_at",
                schema: "dbo",
                table: "auth_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_auth_events_event_type",
                schema: "dbo",
                table: "auth_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_auth_events_outcome",
                schema: "dbo",
                table: "auth_events",
                column: "outcome");

            migrationBuilder.CreateIndex(
                name: "IX_auth_events_user_id",
                schema: "dbo",
                table: "auth_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_auth_events_username",
                schema: "dbo",
                table: "auth_events",
                column: "username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_events",
                schema: "dbo");
        }
    }
}
