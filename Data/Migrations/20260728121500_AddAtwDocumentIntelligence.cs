using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations;

public partial class AddAtwDocumentIntelligence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "analysis_error", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "analysis_status", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NOT_CONFIGURED");
        migrationBuilder.AddColumn<DateTime>(name: "analyzed_at", table: "shipment_request_documents", schema: "dbo", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "content_type", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "extracted_booking_number", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(60)", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "extracted_container_number", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "extracted_shipping_line", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "extraction_confidence", table: "shipment_request_documents", schema: "dbo", type: "decimal(5,4)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "original_file_name", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(name: "risk_flags_json", table: "shipment_request_documents", schema: "dbo", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<long>(name: "size_bytes", table: "shipment_request_documents", schema: "dbo", type: "bigint", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "analysis_error", "analysis_status", "analyzed_at", "content_type", "extracted_booking_number", "extracted_container_number", "extracted_shipping_line", "extraction_confidence", "original_file_name", "risk_flags_json", "size_bytes" })
        {
            migrationBuilder.DropColumn(column, "shipment_request_documents", "dbo");
        }
    }
}
