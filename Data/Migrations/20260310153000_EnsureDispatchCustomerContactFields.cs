using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    [DbContext(typeof(InventoryDbContext))]
    [Migration("20260310153000_EnsureDispatchCustomerContactFields")]
    public partial class EnsureDispatchCustomerContactFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[dispatch_customers]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[dispatch_customers]', N'contact_email') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[dispatch_customers] ADD [contact_email] nvarchar(200) NULL;
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[dispatch_customers]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[dispatch_customers]', N'contact_person') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[dispatch_customers] ADD [contact_person] nvarchar(200) NULL;
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[dispatch_customers]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[dispatch_customers]', N'phone') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[dispatch_customers] ADD [phone] nvarchar(50) NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op for safety in forward-only stabilization.
        }
    }
}
