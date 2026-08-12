using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Tests;

public sealed class ReceiptControllerTests
{
    [Fact]
    public async Task Process_returns_accepted_without_running_ocr_in_the_request()
    {
        await using var db = TestSupport.CreateDb();
        var owner = NewUser("receipt-process@example.com");
        var receipt = NewReceipt(owner, new byte[] { 1, 2, 3 });
        db.AddRange(owner, receipt);
        await db.SaveChangesAsync();
        var processor = new CapturingReceiptProcessor(receipt);
        var controller = new ReceiptsController(
            new ReceiptImageReader(),
            new ReceiptsApplicationService(
                db,
                new TestUserContext(owner.Id),
                processor,
                new ReceiptConfirmationService(db),
                new CategorySuggestionService(db)));

        var response = await controller.Process(receipt.Id, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedAtActionResult>(response.Result);
        Assert.Equal(nameof(ReceiptsController.Get), accepted.ActionName);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Equal(receipt.Id, processor.ReceiptId);
        Assert.False(processor.ExplicitRetry);
        Assert.Equal(0, processor.ProcessNextCalls);
    }

    [Fact]
    public async Task List_and_image_endpoint_never_expose_another_users_receipt()
    {
        await using var db = TestSupport.CreateDb();
        var owner = NewUser("receipt-owner@example.com");
        var other = NewUser("receipt-other@example.com");
        var ownReceipt = NewReceipt(owner, new byte[] { 1, 2, 3 });
        var otherReceipt = NewReceipt(other, new byte[] { 9, 8, 7 });
        db.AddRange(owner, other, ownReceipt, otherReceipt);
        await db.SaveChangesAsync();
        var controller = new ReceiptsController(
            new ReceiptImageReader(),
            new ReceiptsApplicationService(
                db,
                new TestUserContext(owner.Id),
                new NoopReceiptProcessor(),
                new ReceiptConfirmationService(db),
                new CategorySuggestionService(db)));

        var listResponse = await controller.List(
            page: 1,
            pageSize: 20,
            cancellationToken: CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(listResponse.Result);
        var page = Assert.IsType<PagedResponse<ReceiptResponse>>(listOk.Value);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(ownReceipt.Id, Assert.Single(page.Items).Id);

        var ownImage = Assert.IsType<FileStreamResult>(
            await controller.GetImage(ownReceipt.Id, CancellationToken.None));
        await using var buffer = new MemoryStream();
        await ownImage.FileStream.CopyToAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());

        Assert.IsType<NotFoundResult>(
            await controller.GetImage(otherReceipt.Id, CancellationToken.None));
    }

    private static User NewUser(string email) => new()
    {
        Name = email,
        Email = email,
        PasswordHash = "hash"
    };

    private static Receipt NewReceipt(User user, byte[] image) => new()
    {
        UserId = user.Id,
        User = user,
        OriginalFileName = "receipt.jpg",
        ContentType = "image/jpeg",
        FileSize = image.LongLength,
        Image = new ReceiptImage { Data = image }
    };

    private sealed class NoopReceiptProcessor : IReceiptProcessingService
    {
        public Task<Receipt?> EnqueueAsync(
            Guid receiptId,
            Guid userId,
            bool explicitRetry,
            CancellationToken cancellationToken) => Task.FromResult<Receipt?>(null);

        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class CapturingReceiptProcessor(Receipt receipt) : IReceiptProcessingService
    {
        public Guid? ReceiptId { get; private set; }
        public bool ExplicitRetry { get; private set; }
        public int ProcessNextCalls { get; private set; }

        public Task<Receipt?> EnqueueAsync(
            Guid receiptId,
            Guid userId,
            bool explicitRetry,
            CancellationToken cancellationToken)
        {
            ReceiptId = receiptId;
            ExplicitRetry = explicitRetry;
            receipt.Status = ReceiptStatus.QUEUED;
            return Task.FromResult<Receipt?>(receipt);
        }

        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
        {
            ProcessNextCalls++;
            return Task.FromResult(false);
        }
    }
}
