using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? month,
        [FromQuery] TransactionType? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Transactions.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId);

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var monthStart))
                return BadRequest(new { message = "month phải có định dạng yyyy-MM." });
            var monthEnd = monthStart.AddMonths(1);
            query = query.Where(x => x.TransactionDate >= monthStart && x.TransactionDate < monthEnd);
        }
        if (from.HasValue)
            query = query.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.TransactionDate <= to.Value);
        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);
        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);
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
        return Ok(new PagedResponse<TransactionResponse>(
            items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        TransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IdempotencySupport.TryCreate(
                this, "transactions:create", request, out var idempotency, out var keyError))
            return keyError!;
        var replay = await IdempotencySupport.FindAsync<TransactionResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return IdempotencySupport.Conflict(this);
        if (replay?.Exists == true && replay.Response is not null)
        {
            OptimisticConcurrency.WriteEtag(this, replay.Response.Version);
            return StatusCode(replay.StatusCode, replay.Response);
        }

        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await OwnedCategory(request.CategoryId, cancellationToken);
        if (category is null)
            return BadRequest(new { message = "Danh mục không hợp lệ." });
        if (category.Type != request.Type)
            return BadRequest(new { message = "Loại giao dịch phải khớp với danh mục." });

        var transaction = new Domain.Transaction
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
        db.Transactions.Add(transaction);
        var response = transaction.ToResponse();
        IdempotencySupport.Add(
            db,
            userContext.UserId,
            idempotency,
            StatusCodes.Status201Created,
            response);
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
                return IdempotencySupport.Conflict(this);
            if (replay?.Exists == true && replay.Response is not null)
            {
                OptimisticConcurrency.WriteEtag(this, replay.Response.Version);
                return StatusCode(replay.StatusCode, replay.Response);
            }
            throw;
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, transaction.Version);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id,
        TransactionRequest request,
        CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var transaction = await db.Transactions.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (transaction is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, transaction.Version))
            return OptimisticConcurrency.PreconditionFailed(this);
        var category = await OwnedCategory(request.CategoryId, cancellationToken);
        if (category is null)
            return BadRequest(new { message = "Danh mục không hợp lệ." });
        if (category.Type != request.Type)
            return BadRequest(new { message = "Loại giao dịch phải khớp với danh mục." });

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
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, transaction.Version);
        return Ok(transaction.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (transaction is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, transaction.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        db.Transactions.Remove(transaction);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        return NoContent();
    }

    private Task<Category?> OwnedCategory(Guid categoryId, CancellationToken cancellationToken) =>
        FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, categoryId, userContext.UserId, cancellationToken);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
