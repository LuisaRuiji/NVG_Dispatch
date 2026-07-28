using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NVGInventory.Data;

#nullable disable

namespace NVGInventory.Data.Migrations;

/// <summary>
/// Repairs development databases that recorded the dispatch-optimization migration
/// before its TOPSIS weight columns were introduced in the model.
/// </summary>
[DbContext(typeof(InventoryDbContext))]
[Migration("20260728123000_RepairOptimizationWeightSettings")]
public partial class RepairOptimizationWeightSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddDecimalColumnIfMissing(migrationBuilder, "deadhead_distance_weight", "0.25");
        AddDecimalColumnIfMissing(migrationBuilder, "cleaning_time_weight", "0.15");
        AddDecimalColumnIfMissing(migrationBuilder, "waiting_time_weight", "0.15");
        AddDecimalColumnIfMissing(migrationBuilder, "job_urgency_weight", "0.20");
        AddDecimalColumnIfMissing(migrationBuilder, "cargo_compatibility_weight", "0.15");
        AddDecimalColumnIfMissing(migrationBuilder, "asset_utilization_weight", "0.10");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[]
                 {
                     "deadhead_distance_weight",
                     "cleaning_time_weight",
                     "waiting_time_weight",
                     "job_urgency_weight",
                     "cargo_compatibility_weight",
                     "asset_utilization_weight"
                 })
        {
            migrationBuilder.Sql($"""
                IF COL_LENGTH(N'dbo.optimization_weight_settings', N'{column}') IS NOT NULL
                    ALTER TABLE [dbo].[optimization_weight_settings] DROP COLUMN [{column}];
                """);
        }
    }

    private static void AddDecimalColumnIfMissing(MigrationBuilder migrationBuilder, string column, string defaultValue)
    {
        migrationBuilder.Sql($"""
            IF COL_LENGTH(N'dbo.optimization_weight_settings', N'{column}') IS NULL
                ALTER TABLE [dbo].[optimization_weight_settings]
                    ADD [{column}] decimal(5,2) NOT NULL
                    CONSTRAINT [DF_optimization_weight_settings_{column}] DEFAULT ({defaultValue});
            """);
    }
}
