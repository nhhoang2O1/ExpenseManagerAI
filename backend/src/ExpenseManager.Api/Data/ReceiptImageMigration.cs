using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ExpenseManager.Api.Data;

/// <summary>
/// Imports images from the pre-PostgreSQL receipt volume before the final
/// migration removes receipts.file_path. The import is deliberately outside
/// the EF migration SQL because PostgreSQL cannot read the backend container's
/// filesystem.
/// </summary>
public static class ReceiptImageMigration
{
    public const string StageMigration = "20260716123800_StoreReceiptImagesInDatabase";
    public const string FinalMigration = "20260716123900_FinalizeReceiptImagesInDatabase";

    public static async Task ApplyAsync(
        AppDbContext db,
        ILogger logger,
        string? legacyRoot = null,
        CancellationToken cancellationToken = default)
    {
        // A brand-new database has no EF history table yet, so do not query it
        // before the first migration creates it.
        if (!await MigrationHistoryExistsAsync(db, cancellationToken))
        {
            await db.Database.GetService<IMigrator>()
                .MigrateAsync(StageMigration, cancellationToken);
        }
        else
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Apply all migrations up to the nullable staging schema first.
            // This leaves file_path available while old files are copied.
            if (!applied.Contains(StageMigration) && pending.Contains(StageMigration))
            {
                await db.Database.GetService<IMigrator>()
                    .MigrateAsync(StageMigration, cancellationToken);
            }
        }

        var appliedAfterStaging = (await db.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (appliedAfterStaging.Contains(StageMigration) &&
            !appliedAfterStaging.Contains(FinalMigration))
        {
            var imported = await ImportLegacyFilesAsync(
                db, legacyRoot, cancellationToken);
            logger.LogInformation(
                "Receipt image migration verified {ImportedCount} legacy image(s) in PostgreSQL",
                imported);
        }

        // The final migration itself verifies that no receipt is missing data
        // before it drops file_path and makes receipt_images.data NOT NULL.
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> MigrationHistoryExistsAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = current_schema()
                      AND table_name = '__EFMigrationsHistory'
                );
                """;
            return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<int> ImportLegacyFilesAsync(
        AppDbContext db,
        string? legacyRoot,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var legacyReceipts = new List<LegacyReceipt>();
            await using (var select = connection.CreateCommand())
            {
                select.CommandText = """
                    SELECT r.id, r.file_path, r.file_size
                    FROM receipts AS r
                    LEFT JOIN receipt_images AS i ON i.receipt_id = r.id
                    WHERE i.receipt_id IS NULL
                       OR i.data IS NULL
                       OR octet_length(i.data) = 0
                    ORDER BY r.id;
                    """;

                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    legacyReceipts.Add(new LegacyReceipt(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetInt64(2)));
                }
            }

            var imported = 0;
            foreach (var receipt in legacyReceipts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = ResolveLegacyPath(receipt.Path, legacyRoot);
                if (path is null)
                {
                    throw new InvalidOperationException(
                        $"Cannot migrate receipt {receipt.Id}: legacy image '{receipt.Path}' was not found. " +
                        "Keep the receipt volume mounted and restore the file before restarting.");
                }

                var data = await File.ReadAllBytesAsync(path, cancellationToken);
                if (data.LongLength == 0 || data.LongLength != receipt.Size)
                {
                    throw new InvalidOperationException(
                        $"Cannot migrate receipt {receipt.Id}: legacy image size is {data.LongLength} bytes, " +
                        $"expected {receipt.Size} bytes.");
                }

                await using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO receipt_images (receipt_id, data)
                    VALUES (@receipt_id, @data)
                    ON CONFLICT (receipt_id) DO UPDATE SET data = EXCLUDED.data;
                    """;
                var idParameter = insert.CreateParameter();
                idParameter.ParameterName = "@receipt_id";
                idParameter.Value = receipt.Id;
                insert.Parameters.Add(idParameter);
                var dataParameter = insert.CreateParameter();
                dataParameter.ParameterName = "@data";
                dataParameter.Value = data;
                insert.Parameters.Add(dataParameter);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                imported++;
            }

            return imported;
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static string? ResolveLegacyPath(string path, string? legacyRoot)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return path;
        if (string.IsNullOrWhiteSpace(legacyRoot) || string.IsNullOrWhiteSpace(path))
            return null;

        // Older host runs may have persisted a Windows absolute path. The
        // container only needs the generated filename under its mounted legacy
        // directory, so use the basename as a safe compatibility fallback.
        var fileName = Path.GetFileName(path.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        var candidate = Path.Combine(legacyRoot, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private sealed record LegacyReceipt(Guid Id, string Path, long Size);
}
