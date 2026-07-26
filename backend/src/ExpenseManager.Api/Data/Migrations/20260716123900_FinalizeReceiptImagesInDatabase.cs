using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ExpenseManager.Api.Data;

namespace ExpenseManager.Api.Data.Migrations;

/// <summary>
/// Removes the legacy filesystem pointer only after the startup migrator has
/// copied every legacy file into receipt_images.data.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260716123900_FinalizeReceiptImagesInDatabase")]
public partial class FinalizeReceiptImagesInDatabase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM receipts AS r
                    LEFT JOIN receipt_images AS i ON i.receipt_id = r.id
                    WHERE i.receipt_id IS NULL
                       OR i.data IS NULL
                       OR octet_length(i.data) = 0
                ) THEN
                    RAISE EXCEPTION 'Cannot finalize receipt image migration: one or more receipts have no image bytes';
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<byte[]>(
            name: "data",
            table: "receipt_images",
            type: "bytea",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "file_path",
            table: "receipts");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A rollback cannot reconstruct the old filesystem paths. Keep the
        // column nullable so a restored legacy volume can be re-associated by
        // an operator rather than fabricating paths.
        migrationBuilder.AddColumn<string>(
            name: "file_path",
            table: "receipts",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AlterColumn<byte[]>(
            name: "data",
            table: "receipt_images",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");
    }
}
