using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEnumStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.requests
                SET request_type = CASE request_type
                    WHEN 'maintenance_issue' THEN 'MAINTENANCE_ISSUE'
                    WHEN 'borrow' THEN 'BORROW'
                    WHEN 'adjustment_damage_loss' THEN 'ADJUSTMENT_DAMAGE_LOSS'
                    ELSE request_type
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE dbo.requests
                SET status = CASE status
                    WHEN 'draft' THEN 'DRAFT'
                    WHEN 'submitted' THEN 'SUBMITTED'
                    WHEN 'pending_io' THEN 'PENDING_IO'
                    WHEN 'pending_manager' THEN 'PENDING_MANAGER'
                    WHEN 'approved' THEN 'APPROVED'
                    WHEN 'issued' THEN 'ISSUED'
                    WHEN 'closed' THEN 'CLOSED'
                    WHEN 'rejected' THEN 'REJECTED'
                    ELSE status
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.requests
                SET status = CASE status
                    WHEN 'DRAFT' THEN 'draft'
                    WHEN 'SUBMITTED' THEN 'submitted'
                    WHEN 'PENDING_IO' THEN 'pending_io'
                    WHEN 'PENDING_MANAGER' THEN 'pending_manager'
                    WHEN 'APPROVED' THEN 'approved'
                    WHEN 'ISSUED' THEN 'issued'
                    WHEN 'CLOSED' THEN 'closed'
                    WHEN 'REJECTED' THEN 'rejected'
                    ELSE status
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE dbo.requests
                SET request_type = CASE request_type
                    WHEN 'MAINTENANCE_ISSUE' THEN 'maintenance_issue'
                    WHEN 'BORROW' THEN 'borrow'
                    WHEN 'ADJUSTMENT_DAMAGE_LOSS' THEN 'adjustment_damage_loss'
                    ELSE request_type
                END;
                """);
        }
    }
}
