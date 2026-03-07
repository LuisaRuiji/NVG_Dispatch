using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVGInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class DispatchLedgerDocumentVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                newName: "recorded_at");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_trips",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "event_at",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.Sql(
                "UPDATE dbo.dispatch_trip_status_history SET event_at = recorded_at WHERE event_at IS NULL OR event_at = '0001-01-01T00:00:00.0000000';");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_status_history_trip_id_recorded_at",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                columns: new[] { "trip_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "supersedes_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_is_active",
                schema: "dbo",
                table: "dispatch_trip_documents",
                columns: new[] { "trip_id", "doc_type", "is_active" });

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_trip_documents_dispatch_trip_documents_supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents",
                column: "supersedes_document_id",
                principalSchema: "dbo",
                principalTable: "dispatch_trip_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_trip_documents_dispatch_trip_documents_supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_status_history_trip_id_recorded_at",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_documents_supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type_is_active",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_trips");

            migrationBuilder.DropColumn(
                name: "event_at",
                schema: "dbo",
                table: "dispatch_trip_status_history");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.DropColumn(
                name: "supersedes_document_id",
                schema: "dbo",
                table: "dispatch_trip_documents");

            migrationBuilder.RenameColumn(
                name: "recorded_at",
                schema: "dbo",
                table: "dispatch_trip_status_history",
                newName: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_trip_documents_trip_id_doc_type",
                schema: "dbo",
                table: "dispatch_trip_documents",
                columns: new[] { "trip_id", "doc_type" },
                unique: true);
        }
    }
}
