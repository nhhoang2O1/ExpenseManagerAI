using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ExpenseManager.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSQLIntegration")]
public sealed class PostgreSqlReceiptIntegrationTests(
    PostgreSqlIntegrationFixture fixture)
{
    [PostgreSqlFact]
    public async Task LegacyImportCopiesExactBytesThenDropsFilePath()
    {
        await using var db = await fixture.CreateEmptyDbAsync();
        await db.Database.GetService<IMigrator>()
            .MigrateAsync("20260711000249_AddPlanningEntities");

        var bytes = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index * 31 % 256))
            .Concat(new byte[] { 0, 255, 0, 128, 13, 10 })
            .ToArray();
        var legacyRoot = Path.Combine(Path.GetTempPath(), $"receipt-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(legacyRoot);
        var fileName = "legacy-receipt.bin";
        var filePath = Path.Combine(legacyRoot, fileName);
        await File.WriteAllBytesAsync(filePath, bytes);

        var userId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        try
        {
            await InsertLegacyReceiptAsync(db, userId, receiptId,
                $"/old/container/receipt-storage/{fileName}", bytes.LongLength);

            await ReceiptImageMigration.ApplyAsync(
                db, NullLogger.Instance, legacyRoot);

            var stored = await db.ReceiptImages.AsNoTracking()
                .SingleAsync(image => image.ReceiptId == receiptId);
            Assert.Equal(bytes, stored.Data);
            Assert.Equal(bytes.Length, await db.Database.SqlQueryRaw<int>(
                "SELECT octet_length(data) AS \"Value\" FROM receipt_images WHERE receipt_id = {0}",
                receiptId).SingleAsync());

            var filePathStillExists = await db.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'receipts'
                      AND column_name = 'file_path'
                ) AS "Value"
                """).SingleAsync();
            Assert.False(filePathStillExists);
            Assert.Contains(ReceiptImageMigration.FinalMigration,
                await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            Directory.Delete(legacyRoot, recursive: true);
        }
    }

    [PostgreSqlFact]
    public async Task FinalMigrationRefusesToDropPathWhenImageIsMissing()
    {
        await using var db = await fixture.CreateEmptyDbAsync();
        await db.Database.GetService<IMigrator>()
            .MigrateAsync(ReceiptImageMigration.StageMigration);

        await InsertLegacyReceiptAsync(db, Guid.NewGuid(), Guid.NewGuid(),
            "/missing/receipt.jpg", 123);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.GetService<IMigrator>()
                .MigrateAsync(ReceiptImageMigration.FinalMigration));
        Assert.Equal("P0001", exception.SqlState);

        var filePathStillExists = await db.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'receipts'
                  AND column_name = 'file_path'
            ) AS "Value"
            """).SingleAsync();
        Assert.True(filePathStillExists);
    }

    [PostgreSqlFact]
    public async Task DeletingReceiptCascadesToImageAndOcrResult()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var receipt = CreateReceiptWithGraph();
        db.Add(receipt);
        await db.SaveChangesAsync();
        var receiptId = receipt.Id;

        db.Remove(receipt);
        await db.SaveChangesAsync();

        Assert.False(await db.ReceiptImages.AnyAsync(x => x.ReceiptId == receiptId));
        Assert.False(await db.OcrResults.AnyAsync(x => x.ReceiptId == receiptId));
    }

    [PostgreSqlFact]
    public async Task StaleReceiptUpdateRaisesConcurrencyError()
    {
        await using (var seed = await fixture.ResetAndCreateDbAsync())
        {
            seed.Add(CreateReceiptWithGraph());
            await seed.SaveChangesAsync();
        }

        await using var first = fixture.CreateDb();
        await using var stale = fixture.CreateDb();
        var firstReceipt = await first.Receipts.SingleAsync();
        var staleReceipt = await stale.Receipts.SingleAsync();

        firstReceipt.Status = ReceiptStatus.QUEUED;
        await first.SaveChangesAsync();
        Assert.Equal(2, firstReceipt.Version);

        staleReceipt.Status = ReceiptStatus.OCR_FAILED;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => stale.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task CustomFormatBackupRestorePreservesImageBytes()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var receipt = CreateReceiptWithGraph();
        db.Add(receipt);
        await db.SaveChangesAsync();
        var expected = receipt.Image.Data;
        var receiptId = receipt.Id;

        var dumpPath = $"/tmp/expense-{Guid.NewGuid():N}.dump";
        var restoreDatabase = $"expense_restore_{Guid.NewGuid():N}";
        var dump = await fixture.Container.ExecAsync([
            "pg_dump", "-Fc", "-U", "expense_test", "-d",
            "expense_manager_integration", "-f", dumpPath
        ]);
        Assert.True(dump.ExitCode == 0, dump.Stderr);

        var create = await fixture.Container.ExecAsync([
            "createdb", "-U", "expense_test", restoreDatabase
        ]);
        Assert.True(create.ExitCode == 0, create.Stderr);

        try
        {
            var restore = await fixture.Container.ExecAsync([
                "pg_restore", "--no-owner", "--exit-on-error", "-U",
                "expense_test", "-d", restoreDatabase, dumpPath
            ]);
            Assert.True(restore.ExitCode == 0, restore.Stderr);

            var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = restoreDatabase
            };
            await using var restored = fixture.CreateDb(builder.ConnectionString);
            var restoredBytes = await restored.ReceiptImages.AsNoTracking()
                .Where(image => image.ReceiptId == receiptId)
                .Select(image => image.Data)
                .SingleAsync();
            Assert.Equal(expected, restoredBytes);
        }
        finally
        {
            await fixture.Container.ExecAsync([
                "dropdb", "--force", "-U", "expense_test", restoreDatabase
            ]);
            await fixture.Container.ExecAsync(["rm", "-f", dumpPath]);
        }
    }

    private static Receipt CreateReceiptWithGraph()
    {
        var receipt = new Receipt
        {
            OriginalFileName = "receipt.jpg",
            ContentType = "image/jpeg",
            FileSize = 8,
            Status = ReceiptStatus.UPLOADED,
            User = new User
            {
                Name = "PostgreSQL Integration",
                Email = $"postgres-{Guid.NewGuid():N}@example.test",
                PasswordHash = "not-used"
            },
            Image = new ReceiptImage
            {
                Data = [0, 255, 1, 254, 2, 253, 0, 128]
            }
        };
        receipt.OcrResult = new OcrResult
        {
            Receipt = receipt,
            RawText = "integration",
            LinesJson = "[]",
            OverallConfidence = 0.99m,
            ModelVersion = "unchanged",
            ParserVersion = "unchanged",
            WarningsJson = "[]"
        };
        return receipt;
    }

    private static async Task InsertLegacyReceiptAsync(
        AppDbContext db,
        Guid userId,
        Guid receiptId,
        string filePath,
        long fileSize)
    {
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, name, email, password_hash, created_at)
            VALUES (@user_id, 'Legacy User', @email, 'not-used', now());
            INSERT INTO receipts (
                id, user_id, original_file_name, content_type, file_path,
                file_size, status, classification, created_at, updated_at)
            VALUES (
                @receipt_id, @user_id, 'legacy.jpg', 'image/jpeg', @file_path,
                @file_size, 'UPLOADED', NULL, now(), now());
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("email", $"legacy-{userId:N}@example.test");
        command.Parameters.AddWithValue("receipt_id", receiptId);
        command.Parameters.AddWithValue("file_path", filePath);
        command.Parameters.AddWithValue("file_size", fileSize);
        await command.ExecuteNonQueryAsync();
    }
}
