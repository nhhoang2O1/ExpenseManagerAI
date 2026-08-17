using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface ITransactionsApplicationService
{
    Task<ApplicationServiceResult<PagedResponse<TransactionResponse>>> GetAllAsync(
        DateOnly? from, DateOnly? to, string? month, TransactionType? type,
        Guid? categoryId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<TransactionResponse>> CreateAsync(
        TransactionRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<TransactionResponse>> UpdateAsync(
        Guid id, TransactionRequest request, string? ifMatch, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken);
}

public sealed class TransactionsApplicationService(AppDbContext db, IUserContext userContext)
    : ITransactionsApplicationService
{
    public async Task<ApplicationServiceResult<PagedResponse<TransactionResponse>>> GetAllAsync(
        DateOnly? from, DateOnly? to, string? month, TransactionType? type,
        Guid? categoryId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Transactions.AsNoTracking().Where(x => x.UserId == userContext.UserId);

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var monthStart))
                return ApplicationServiceResult<PagedResponse<TransactionResponse>>.BadRequest(
                    "month phải có định dạng yyyy-MM.");
            var monthEnd = monthStart.AddMonths(1);
            query = query.Where(x => x.TransactionDate >= monthStart && x.TransactionDate < monthEnd);
        }
        if (from.HasValue) query = query.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.TransactionDate <= to.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Note != null && x.Note.ToLower().Contains(term)) ||
                (x.StoreName != null && x.StoreName.ToLower().Contains(term)) ||
                x.Category.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query.Include(x => x.Category)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = entities.Select(x => x.ToResponse()).ToList();
        var response = new PagedResponse<TransactionResponse>(
            items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
        return ApplicationServiceResult<PagedResponse<TransactionResponse>>.Ok(response);
    }

    public async Task<ApplicationServiceResult<TransactionResponse>> CreateAsync(
        TransactionRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!IdempotencySupport.TryCreate(
                idempotencyKey, "transactions:create", request, out var idempotency, out var keyError))
            return ApplicationServiceResult<TransactionResponse>.BadRequest(keyError!);
        var replay = await IdempotencySupport.FindAsync<TransactionResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return ApplicationServiceResult<TransactionResponse>.Conflict(
                "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true && replay.Response is not null)
            return new ApplicationServiceResult<TransactionResponse>(
                replay.StatusCode, replay.Response, Version: replay.Response.Version);

        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await OwnedCategory(request.CategoryId, cancellationToken);
        if (category is null)
            return ApplicationServiceResult<TransactionResponse>.BadRequest("Danh mục không hợp lệ.");
        if (category.Type != request.Type)
            return ApplicationServiceResult<TransactionResponse>.BadRequest(
                "Loại giao dịch phải khớp với danh mục.");

        var transaction = new Transaction
        {
            UserId = userContext.UserId,
            Amount = request.Amount,
            Type = request.Type,
            TransactionDate = request.TransactionDate,
            CategoryId = category.Id,
            Category = category,
            Note = Clean(request.Note),
            StoreName = Clean(request.StoreName)
        };
        var budgetAlert = request.Type == TransactionType.EXPENSE
            ? await BudgetAlertService.EvaluateProjectedAsync(
                db, userContext.UserId, category.Id, request.TransactionDate,
                request.Amount, null, cancellationToken)
            : null;
        db.Transactions.Add(transaction);
        var response = transaction.ToResponse(budgetAlert);
        IdempotencySupport.Add(db, userContext.UserId, idempotency,
            StatusCodes.Status201Created, response);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotency is not null)
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);
                await databaseTransaction.DisposeAsync();
            }
            db.ChangeTracker.Clear();
            replay = await IdempotencySupport.FindAsync<TransactionResponse>(
                db, userContext.UserId, idempotency, cancellationToken);
            if (replay?.RequestConflict == true)
                return ApplicationServiceResult<TransactionResponse>.Conflict(
                    "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
            if (replay?.Exists == true && replay.Response is not null)
                return new ApplicationServiceResult<TransactionResponse>(
                    replay.StatusCode, replay.Response, Version: replay.Response.Version);
            throw;
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<TransactionResponse>.Created(response, transaction.Version);
    }

    public async Task<ApplicationServiceResult<TransactionResponse>> UpdateAsync(
        Guid id, TransactionRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var transaction = await db.Transactions.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (transaction is null)
            return ApplicationServiceResult<TransactionResponse>.NotFound();
        if (transaction.GoalId.HasValue)
            return ApplicationServiceResult<TransactionResponse>.Conflict(
                "Giao dịch hoàn thành mục tiêu không thể chỉnh sửa.");
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, transaction.Version))
            return ApplicationServiceResult<TransactionResponse>.PreconditionFailed();
        var category = await OwnedCategory(request.CategoryId, cancellationToken);
        if (category is null)
            return ApplicationServiceResult<TransactionResponse>.BadRequest("Danh mục không hợp lệ.");
        if (category.Type != request.Type)
            return ApplicationServiceResult<TransactionResponse>.BadRequest(
                "Loại giao dịch phải khớp với danh mục.");

        var budgetAlert = request.Type == TransactionType.EXPENSE
            ? await BudgetAlertService.EvaluateProjectedAsync(
                db, userContext.UserId, category.Id, request.TransactionDate,
                request.Amount, transaction.Id, cancellationToken)
            : null;

        transaction.Amount = request.Amount;
        transaction.Type = request.Type;
        transaction.TransactionDate = request.TransactionDate;
        transaction.CategoryId = category.Id;
        transaction.Category = category;
        transaction.Note = Clean(request.Note);
        transaction.StoreName = Clean(request.StoreName);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<TransactionResponse>.PreconditionFailed();
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<TransactionResponse>.Ok(
            transaction.ToResponse(budgetAlert), transaction.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (transaction is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (transaction.GoalId.HasValue)
            return ApplicationServiceResult<object?>.Conflict(
                "Giao dịch hoàn thành mục tiêu không thể xóa.");
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, transaction.Version))
            return ApplicationServiceResult<object?>.PreconditionFailed();

        db.Transactions.Remove(transaction);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<object?>.PreconditionFailed();
        }
        return ApplicationServiceResult<object?>.NoContent();
    }

    private Task<Category?> OwnedCategory(Guid categoryId, CancellationToken cancellationToken) =>
        FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, categoryId, userContext.UserId, cancellationToken);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
