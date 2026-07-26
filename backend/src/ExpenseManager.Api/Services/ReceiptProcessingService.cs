using System.Text.Json;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExpenseManager.Api.Services;

public sealed class ReceiptProcessingOptions
{
    public int PollIntervalMilliseconds { get; set; } = 1_000;
    public int LeaseSeconds { get; set; } = 180;
    public int MaxAttempts { get; set; } = 3;
    public int InitialRetryDelaySeconds { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 300;

    internal TimeSpan PollInterval =>
        TimeSpan.FromMilliseconds(Math.Clamp(PollIntervalMilliseconds, 100, 60_000));

    internal TimeSpan LeaseDuration =>
        TimeSpan.FromSeconds(Math.Clamp(LeaseSeconds, 30, 3_600));

    internal int EffectiveMaxAttempts => Math.Clamp(MaxAttempts, 1, 20);

    internal TimeSpan RetryDelay(int attempt)
    {
        var initial = Math.Clamp(InitialRetryDelaySeconds, 0, 3_600);
        var maximum = Math.Clamp(MaxRetryDelaySeconds, initial, 86_400);
        var exponent = Math.Clamp(attempt - 1, 0, 20);
        var seconds = Math.Min(maximum, initial * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(seconds);
    }
}

public interface IReceiptProcessingService
{
    Task<Receipt?> EnqueueAsync(
        Guid receiptId,
        Guid userId,
        bool explicitRetry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims and processes at most one due receipt. Returns false
    /// when there is currently no work, allowing the hosted worker to back off.
    /// </summary>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

public sealed class ReceiptProcessingService(
    AppDbContext db,
    IOcrClient ocrClient,
    IOptions<ReceiptProcessingOptions> options,
    ILogger<ReceiptProcessingService> logger) : IReceiptProcessingService
{
    private const int MaxStoredErrorLength = 2_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ReceiptProcessingOptions _options = options.Value;

    public async Task<Receipt?> EnqueueAsync(
        Guid receiptId,
        Guid userId,
        bool explicitRetry,
        CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.Include(x => x.OcrResult).SingleOrDefaultAsync(
            x => x.Id == receiptId && x.UserId == userId,
            cancellationToken);
        if (receipt is null)
            return null;

        // Calling process/retry repeatedly is safe. A running job keeps its
        // lease; a queued job is merely made immediately eligible again.
        if (receipt.Status is ReceiptStatus.CONFIRMED or ReceiptStatus.PROCESSING)
            return receipt;

        receipt.Status = ReceiptStatus.QUEUED;
        receipt.ProcessingStartedAt = null;
        receipt.LeaseExpiresAt = null;
        receipt.NextRetryAt = DateTime.UtcNow;
        if (explicitRetry)
        {
            receipt.ProcessingAttempts = 0;
            receipt.LastError = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return receipt;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var receiptId = await ClaimNextAsync(cancellationToken);
        if (receiptId is null)
            return false;

        try
        {
            await ProcessClaimedAsync(receiptId.Value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown should make the job immediately reclaimable.
            // A hard crash is covered by LeaseExpiresAt instead.
            await MarkFailureAsync(
                receiptId.Value,
                "Receipt processing was interrupted while the worker was stopping.",
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Receipt OCR attempt failed for {ReceiptId}", receiptId);
            await MarkFailureAsync(receiptId.Value, SafeError(ex), CancellationToken.None);
        }

        return true;
    }

    private async Task<Guid?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        Receipt? receipt;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // PostgreSQL row locking prevents two workers from claiming the
            // same job, while SKIP LOCKED lets additional workers continue.
            var candidates = await db.Receipts.FromSqlInterpolated($"""
                SELECT *
                FROM receipts
                WHERE (
                    status = 'QUEUED'
                    AND (next_retry_at IS NULL OR next_retry_at <= {now})
                ) OR (
                    status = 'PROCESSING'
                    AND lease_expires_at IS NOT NULL
                    AND lease_expires_at <= {now}
                )
                ORDER BY COALESCE(next_retry_at, lease_expires_at, created_at), created_at, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken);

            receipt = candidates.SingleOrDefault();
            if (receipt is not null)
            {
                MarkClaimed(receipt, now);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            // EF's in-memory provider is used only by unit tests. Production
            // always follows the PostgreSQL row-locking branch above.
            receipt = await db.Receipts
                .Where(x =>
                    (x.Status == ReceiptStatus.QUEUED &&
                     (x.NextRetryAt == null || x.NextRetryAt <= now)) ||
                    (x.Status == ReceiptStatus.PROCESSING &&
                     x.LeaseExpiresAt != null && x.LeaseExpiresAt <= now))
                .OrderBy(x => x.NextRetryAt ?? x.LeaseExpiresAt ?? x.CreatedAt)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (receipt is not null)
            {
                MarkClaimed(receipt, now);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var receiptId = receipt?.Id;
        db.ChangeTracker.Clear();
        return receiptId;
    }

    private void MarkClaimed(Receipt receipt, DateTime now)
    {
        receipt.Status = ReceiptStatus.PROCESSING;
        receipt.ProcessingAttempts = checked(receipt.ProcessingAttempts + 1);
        receipt.ProcessingStartedAt = now;
        receipt.LeaseExpiresAt = now.Add(_options.LeaseDuration);
        receipt.NextRetryAt = null;
    }

    private async Task ProcessClaimedAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts
            .Include(x => x.Image)
            .Include(x => x.OcrResult)
            .SingleOrDefaultAsync(
                x => x.Id == receiptId && x.Status == ReceiptStatus.PROCESSING,
                cancellationToken);
        if (receipt is null)
            return; // The owner may have deleted the receipt after it was claimed.
        if (receipt.Image is null || receipt.Image.Data.Length == 0)
            throw new InvalidDataException("The receipt image is missing from the database.");

        var response = await ocrClient.ProcessAsync(
            receipt.Image.Data,
            receipt.OriginalFileName,
            receipt.ContentType,
            cancellationToken);

        receipt.Classification = response.Classification;
        receipt.Status = ReceiptStatus.REVIEW_REQUIRED;
        receipt.ProcessingStartedAt = null;
        receipt.LeaseExpiresAt = null;
        receipt.NextRetryAt = null;
        receipt.LastError = null;

        var result = receipt.OcrResult ?? new OcrResult
        {
            ReceiptId = receipt.Id,
            RawText = string.Empty,
            LinesJson = "[]",
            ModelVersion = string.Empty,
            ParserVersion = string.Empty,
            WarningsJson = "[]"
        };
        result.RawText = response.RawText ?? string.Empty;
        result.LinesJson = JsonSerializer.Serialize(response.Lines ?? [], JsonOptions);
        result.StoreName = response.Fields?.StoreName;
        result.ReceiptDate = response.Fields?.ReceiptDate;
        result.TotalAmount = response.Fields?.TotalAmount;
        result.VatAmount = response.Fields?.VatAmount;
        result.OverallConfidence = response.OverallConfidence;
        result.ModelVersion = response.ModelVersion ?? string.Empty;
        result.ParserVersion = response.ParserVersion ?? string.Empty;
        result.WarningsJson = JsonSerializer.Serialize(response.Warnings ?? [], JsonOptions);
        result.ProcessingTimeMs = response.ProcessingTimeMs;
        result.CreatedAt = DateTime.UtcNow;
        if (receipt.OcrResult is null)
        {
            receipt.OcrResult = result;
            db.OcrResults.Add(result);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailureAsync(
        Guid receiptId,
        string error,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var receipt = await db.Receipts.SingleOrDefaultAsync(
            x => x.Id == receiptId,
            cancellationToken);
        if (receipt is null || receipt.Status != ReceiptStatus.PROCESSING)
            return;

        var exhausted = receipt.ProcessingAttempts >= _options.EffectiveMaxAttempts;
        receipt.Status = exhausted ? ReceiptStatus.OCR_FAILED : ReceiptStatus.QUEUED;
        receipt.ProcessingStartedAt = null;
        receipt.LeaseExpiresAt = null;
        receipt.NextRetryAt = exhausted
            ? null
            : DateTime.UtcNow.Add(_options.RetryDelay(receipt.ProcessingAttempts));
        receipt.LastError = error.Length <= MaxStoredErrorLength
            ? error
            : error[..MaxStoredErrorLength];

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deletion by the owner is a valid lifecycle transition.
        }
    }

    private static string SafeError(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        return string.IsNullOrWhiteSpace(message)
            ? "Receipt OCR processing failed."
            : message;
    }
}
