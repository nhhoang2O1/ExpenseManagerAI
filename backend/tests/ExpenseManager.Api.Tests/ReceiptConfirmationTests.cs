using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

public sealed class ReceiptConfirmationTests
{
    [Fact]
    public async Task Confirm_is_idempotent_and_creates_only_one_transaction()
    {
        await using var db = TestSupport.CreateDb();
        var user = new User
        {
            Name = "Receipt User",
            Email = "receipt@example.com",
            PasswordHash = "hash"
        };
        var category = new Category
        {
            UserId = user.Id,
            User = user,
            Name = "Food",
            Type = TransactionType.EXPENSE
        };
        var receipt = new Receipt
        {
            UserId = user.Id,
            User = user,
            OriginalFileName = "receipt.jpg",
            ContentType = "image/jpeg",
            FileSize = 100,
            Status = ReceiptStatus.REVIEW_REQUIRED,
            Classification = ReceiptClassification.SUPPORTED,
            Image = new ReceiptImage { Data = new byte[] { 0xFF, 0xD8, 0xFF } }
        };
        db.AddRange(user, category, receipt);
        await db.SaveChangesAsync();
        var service = new ReceiptConfirmationService(db);
        var request = new ConfirmReceiptRequest(
            "Circle K",
            new DateOnly(2026, 7, 9),
            125_000,
            10_000,
            category.Id,
            "Lunch");

        var first = await service.ConfirmAsync(
            receipt.Id, user.Id, request, CancellationToken.None);
        var second = await service.ConfirmAsync(
            receipt.Id, user.Id, request, CancellationToken.None);

        Assert.Equal(ConfirmationOutcome.SUCCESS, first.Outcome);
        Assert.Equal(ConfirmationOutcome.SUCCESS, second.Outcome);
        Assert.NotNull(first.Transaction);
        Assert.Equal(first.Transaction!.Id, second.Transaction!.Id);
        Assert.Equal(1, await db.Transactions.CountAsync(x => x.ReceiptId == receipt.Id));
        Assert.Equal(
            ReceiptStatus.CONFIRMED,
            (await db.Receipts.SingleAsync(x => x.Id == receipt.Id)).Status);
    }

    [Fact]
    public async Task Ocr_failed_receipt_can_be_confirmed_with_valid_manual_values()
    {
        await using var db = TestSupport.CreateDb();
        var user = new User
        {
            Name = "Manual Receipt User",
            Email = "manual-receipt@example.com",
            PasswordHash = "hash"
        };
        var category = new Category
        {
            UserId = user.Id,
            User = user,
            Name = "Other expense",
            Type = TransactionType.EXPENSE
        };
        var receipt = new Receipt
        {
            UserId = user.Id,
            User = user,
            OriginalFileName = "unreadable.jpg",
            ContentType = "image/jpeg",
            FileSize = 3,
            Status = ReceiptStatus.OCR_FAILED,
            LastError = "OCR unavailable",
            Image = new ReceiptImage { Data = new byte[] { 0xFF, 0xD8, 0xFF } }
        };
        db.AddRange(user, category, receipt);
        await db.SaveChangesAsync();
        var service = new ReceiptConfirmationService(db);

        var result = await service.ConfirmAsync(
            receipt.Id,
            user.Id,
            new ConfirmReceiptRequest(
                "Entered manually",
                new DateOnly(2026, 7, 16),
                80_000,
                null,
                category.Id,
                null),
            CancellationToken.None);

        Assert.Equal(ConfirmationOutcome.SUCCESS, result.Outcome);
        Assert.Equal(80_000, result.Transaction!.Amount);
        Assert.Equal(ReceiptStatus.CONFIRMED, receipt.Status);
    }
}
