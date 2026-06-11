using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaAndStepUpAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "dbo",
                table: "users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "mfa_enabled_at",
                schema: "dbo",
                table: "users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "mfa_last_verified_at",
                schema: "dbo",
                table: "users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mfa_secret_key",
                schema: "dbo",
                table: "users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_mfa_secret_key",
                schema: "dbo",
                table: "users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mfa_challenges",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_mfa_challenges_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mfa_challenges_expires_at",
                schema: "dbo",
                table: "mfa_challenges",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_mfa_challenges_user_id_consumed_at",
                schema: "dbo",
                table: "mfa_challenges",
                columns: new[] { "user_id", "consumed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mfa_challenges",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_enabled_at",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_last_verified_at",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_secret_key",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_mfa_secret_key",
                schema: "dbo",
                table: "users");
        }
    }
}
