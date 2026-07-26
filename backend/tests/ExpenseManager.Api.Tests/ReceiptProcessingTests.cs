using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExpenseManager.Api.Tests;

public sealed class ReceiptProcessingTests
{
    [Fact]
    public async Task Enqueue_is_fast_state_transition_and_processing_completes_in_background()
    {
        await using var db = TestSupport.CreateDb();
        var receipt = await AddReceiptAsync(db);
        var ocr = new StubOcrClient(SuccessResponse());
        var service = CreateService(db, ocr);

        var queued = await service.EnqueueAsync(
            receipt.Id,
            receipt.UserId,
            explicitRetry: false,
            CancellationToken.None);

        Assert.NotNull(queued);
        Assert.Equal(ReceiptStatus.QUEUED, queued!.Status);
        Assert.Equal(0, ocr.CallCount);

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));

        var completed = await db.Receipts.AsNoTracking()
            .Include(x => x.OcrResult)
            .SingleAsync(x => x.Id == receipt.Id);
        Assert.Equal(ReceiptStatus.REVIEW_REQUIRED, completed.Status);
        Assert.Equal(1, completed.ProcessingAttempts);
        Assert.Null(completed.LeaseExpiresAt);
        Assert.Null(completed.LastError);
        Assert.Equal("Test Store", completed.OcrResult!.StoreName);
        Assert.Equal(123_000, completed.OcrResult.TotalAmount);
        Assert.Equal(1, ocr.CallCount);
    }

    [Fact]
    public async Task Failed_attempts_retry_and_eventually_become_ocr_failed()
    {
        await using var db = TestSupport.CreateDb();
        var receipt = await AddReceiptAsync(db);
        var ocr = new StubOcrClient(new HttpRequestException("OCR unavailable"));
        var service = CreateService(db, ocr, new ReceiptProcessingOptions
        {
            MaxAttempts = 2,
            InitialRetryDelaySeconds = 0,
            MaxRetryDelaySeconds = 0
        });
        await service.EnqueueAsync(
            receipt.Id,
            receipt.UserId,
            explicitRetry: false,
            CancellationToken.None);

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));
        var retrying = await db.Receipts.AsNoTracking().SingleAsync(x => x.Id == receipt.Id);
        Assert.Equal(ReceiptStatus.QUEUED, retrying.Status);
        Assert.Equal(1, retrying.ProcessingAttempts);
        Assert.NotNull(retrying.NextRetryAt);
        Assert.Contains("OCR unavailable", retrying.LastError);

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));
        var failed = await db.Receipts.AsNoTracking().SingleAsync(x => x.Id == receipt.Id);
        Assert.Equal(ReceiptStatus.OCR_FAILED, failed.Status);
        Assert.Equal(2, failed.ProcessingAttempts);
        Assert.Null(failed.NextRetryAt);
        Assert.Null(failed.LeaseExpiresAt);
        Assert.Equal(2, ocr.CallCount);
    }

    [Fact]
    public async Task Expired_processing_lease_is_reclaimed_after_worker_restart()
    {
        await using var db = TestSupport.CreateDb();
        var receipt = await AddReceiptAsync(db);
        receipt.Status = ReceiptStatus.PROCESSING;
        receipt.ProcessingAttempts = 1;
        receipt.ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-10);
        receipt.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var service = CreateService(db, new StubOcrClient(SuccessResponse()));

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));
        var completed = await db.Receipts.AsNoTracking().SingleAsync(x => x.Id == receipt.Id);
        Assert.Equal(ReceiptStatus.REVIEW_REQUIRED, completed.Status);
        Assert.Equal(2, completed.ProcessingAttempts);
        Assert.Null(completed.LeaseExpiresAt);
    }

    private static ReceiptProcessingService CreateService(
        Data.AppDbContext db,
        IOcrClient ocrClient,
        ReceiptProcessingOptions? options = null) =>
        new(
            db,
            ocrClient,
            Options.Create(options ?? new ReceiptProcessingOptions()),
            NullLogger<ReceiptProcessingService>.Instance);

    private static async Task<Receipt> AddReceiptAsync(Data.AppDbContext db)
    {
        var user = new User
        {
            Name = "Receipt Worker User",
            Email = $"receipt-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash"
        };
        var receipt = new Receipt
        {
            UserId = user.Id,
            User = user,
            OriginalFileName = "receipt.jpg",
            ContentType = "image/jpeg",
            FileSize = 3,
            Image = new ReceiptImage { Data = new byte[] { 0xFF, 0xD8, 0xFF } }
        };
        db.AddRange(user, receipt);
        await db.SaveChangesAsync();
        return receipt;
    }

    private static OcrServiceResponse SuccessResponse() =>
        new(
            ReceiptClassification.SUPPORTED,
            "REVIEW_REQUIRED",
            "TEST STORE 123000",
            [],
            new OcrFields("Test Store", new DateOnly(2026, 7, 16), 123_000, null),
            0.95m,
            "test-model",
            "test-parser",
            [],
            25);

    private sealed class StubOcrClient : IOcrClient
    {
        private readonly OcrServiceResponse? _response;
        private readonly Exception? _exception;

        public StubOcrClient(OcrServiceResponse response) => _response = response;
        public StubOcrClient(Exception exception) => _exception = exception;

        public int CallCount { get; private set; }

        public Task<OcrServiceResponse> ProcessAsync(
            byte[] imageData,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_exception is not null)
                return Task.FromException<OcrServiceResponse>(_exception);
            return Task.FromResult(_response!);
        }
    }
}
