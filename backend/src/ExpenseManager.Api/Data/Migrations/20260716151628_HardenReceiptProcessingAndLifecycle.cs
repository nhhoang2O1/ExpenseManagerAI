using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260716151628_HardenReceiptProcessingAndLifecycle")]
public sealed class HardenReceiptProcessingAndLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "processing_attempts",
            table: "receipts",
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(
            name: "processing_started_at",
            table: "receipts",
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "lease_expires_at",
            table: "receipts",
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "next_retry_at",
            table: "receipts",
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "last_error",
            table: "receipts",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "receipts",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.Sql("""
            UPDATE receipts
            SET status = 'QUEUED',
                next_retry_at = CURRENT_TIMESTAMP,
                processing_started_at = NULL,
                lease_expires_at = NULL,
                updated_at = CURRENT_TIMESTAMP
            WHERE status = 'PROCESSING';
            """);

        migrationBuilder.CreateIndex(
            name: "ix_receipts_status_next_retry_at_lease_expires_at_created_at",
            table: "receipts",
            columns: new[] { "status", "next_retry_at", "lease_expires_at", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_receipts_status_next_retry_at_lease_expires_at_created_at",
            table: "receipts");
        migrationBuilder.DropColumn(name: "processing_attempts", table: "receipts");
        migrationBuilder.DropColumn(name: "processing_started_at", table: "receipts");
        migrationBuilder.DropColumn(name: "lease_expires_at", table: "receipts");
        migrationBuilder.DropColumn(name: "next_retry_at", table: "receipts");
        migrationBuilder.DropColumn(name: "last_error", table: "receipts");
        migrationBuilder.DropColumn(name: "version", table: "receipts");
    }
}
