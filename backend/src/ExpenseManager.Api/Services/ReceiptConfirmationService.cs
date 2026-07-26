using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ExpenseManager.Api.Services;

public enum ConfirmationOutcome
{
    SUCCESS,
    RECEIPT_NOT_FOUND,
    CATEGORY_NOT_FOUND,
    INVALID_RECEIPT_STATE
}

public sealed record ConfirmationResult(
    ConfirmationOutcome Outcome,
    Domain.Transaction? Transaction = null);

public interface IReceiptConfirmationService
{
    Task<ConfirmationResult> ConfirmAsync(
        Guid receiptId,
        Guid userId,
        ConfirmReceiptRequest request,
        CancellationToken cancellationToken);
}

public sealed class ReceiptConfirmationService(AppDbContext db) : IReceiptConfirmationService
{
    public async Task<ConfirmationResult> ConfirmAsync(
        Guid receiptId,
        Guid userId,
        ConfirmReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await ExistingTransaction(receiptId, userId, cancellationToken);
        if (existing is not null)
            return new ConfirmationResult(ConfirmationOutcome.SUCCESS, existing);

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            existing = await ExistingTransaction(receiptId, userId, cancellationToken);
            if (existing is not null)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return new ConfirmationResult(ConfirmationOutcome.SUCCESS, existing);
            }

            var receipt = await db.Receipts.SingleOrDefaultAsync(
                x => x.Id == receiptId && x.UserId == userId, cancellationToken);
            if (receipt is null)
                return new ConfirmationResult(ConfirmationOutcome.RECEIPT_NOT_FOUND);
            if (receipt.Status is
                ReceiptStatus.UPLOADED or ReceiptStatus.QUEUED or ReceiptStatus.PROCESSING)
                return new ConfirmationResult(ConfirmationOutcome.INVALID_RECEIPT_STATE);

            var category = await db.Categories.SingleOrDefaultAsync(
                x => x.Id == request.CategoryId && x.UserId == userId, cancellationToken);
            if (category is null)
                return new ConfirmationResult(ConfirmationOutcome.CATEGORY_NOT_FOUND);
            if (category.Type != TransactionType.EXPENSE)
                return new ConfirmationResult(ConfirmationOutcome.CATEGORY_NOT_FOUND);

            var created = new Domain.Transaction
            {
                UserId = userId,
                CategoryId = category.Id,
                Category = category,
                ReceiptId = receipt.Id,
                Receipt = receipt,
                Amount = request.TotalAmount,
                Type = TransactionType.EXPENSE,
                TransactionDate = request.ReceiptDate,
                StoreName = request.StoreName.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
            };
            db.Transactions.Add(created);
            receipt.Status = ReceiptStatus.CONFIRMED;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return new ConfirmationResult(ConfirmationOutcome.SUCCESS, created);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            existing = await ExistingTransaction(receiptId, userId, cancellationToken);
            if (existing is not null)
                return new ConfirmationResult(ConfirmationOutcome.SUCCESS, existing);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private Task<Domain.Transaction?> ExistingTransaction(
        Guid receiptId,
        Guid userId,
        CancellationToken cancellationToken) =>
        db.Transactions.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.ReceiptId == receiptId && x.UserId == userId, cancellationToken);
}
