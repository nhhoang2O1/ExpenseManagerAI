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
[Route("api/budgets")]
public sealed class BudgetsController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetResponse>>> GetAll(
        [FromQuery] string? monthYear,
        CancellationToken cancellationToken)
    {
        var query = db.Budgets.AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId);

        if (!string.IsNullOrWhiteSpace(monthYear))
        {
            if (!BudgetRules.IsValidMonthYear(monthYear))
                return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });
            query = query.Where(x => x.MonthYear == monthYear);
        }

        var items = await query
            .OrderBy(x => x.MonthYear)
            .ThenBy(x => x.Category.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> CreateOrUpdate(
        BudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!BudgetRules.IsValidMonthYear(request.MonthYear))
            return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });

        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, request.CategoryId, userContext.UserId, cancellationToken);
        if (category is null || !BudgetRules.CanUseCategory(category.Type))
            return BadRequest(new { message = "Danh muc chi tieu khong hop le." });

        var budget = await db.Budgets.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.UserId == userContext.UserId &&
                 x.CategoryId == request.CategoryId &&
                 x.MonthYear == request.MonthYear,
            cancellationToken);

        var created = false;
        if (budget is null)
        {
            created = true;
            budget = new Budget
            {
                UserId = userContext.UserId,
                CategoryId = category.Id,
                Category = category,
                Amount = request.Amount,
                MonthYear = request.MonthYear
            };
            db.Budgets.Add(budget);
        }
        else
        {
            if (!OptimisticConcurrency.IfMatchSatisfied(this, budget.Version))
                return OptimisticConcurrency.PreconditionFailed(this);
            budget.Amount = request.Amount;
            budget.Category = category;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        catch (DbUpdateException)
        {
            // The unique index is authoritative when two requests create the
            // same category/month budget concurrently.
            return Conflict(new { message = "Ngân sách cho danh mục và tháng này đã tồn tại." });
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, budget.Version);
        return created
            ? StatusCode(StatusCodes.Status201Created, budget.ToResponse())
            : Ok(budget.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BudgetResponse>> Update(
        Guid id,
        BudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!BudgetRules.IsValidMonthYear(request.MonthYear))
            return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });

        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var budget = await db.Budgets.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, budget.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        var category = await FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, request.CategoryId, userContext.UserId, cancellationToken);
        if (category is null || !BudgetRules.CanUseCategory(category.Type))
            return BadRequest(new { message = "Danh muc chi tieu khong hop le." });

        var duplicate = await db.Budgets.AnyAsync(
            x => x.Id != id &&
                 x.UserId == userContext.UserId &&
                 x.CategoryId == request.CategoryId &&
                 x.MonthYear == request.MonthYear,
            cancellationToken);
        if (duplicate)
            return Conflict(new { message = "Ngan sach cho danh muc va thang nay da ton tai." });

        budget.CategoryId = category.Id;
        budget.Category = category;
        budget.Amount = request.Amount;
        budget.MonthYear = request.MonthYear;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Ngân sách cho danh mục và tháng này đã tồn tại." });
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, budget.Version);
        return Ok(budget.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, budget.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        db.Budgets.Remove(budget);
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

}
