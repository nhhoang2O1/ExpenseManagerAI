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
            if (!IsMonthYear(monthYear))
                return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });
            query = query.Where(x => x.MonthYear == monthYear);
        }

        var items = await query
            .OrderBy(x => x.MonthYear)
            .ThenBy(x => x.Category.Name)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> CreateOrUpdate(
        BudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsMonthYear(request.MonthYear))
            return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });

        var category = await db.Categories.SingleOrDefaultAsync(
            x => x.Id == request.CategoryId && x.UserId == userContext.UserId,
            cancellationToken);
        if (category is null || category.Type != TransactionType.EXPENSE)
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
            budget.Amount = request.Amount;
            budget.Category = category;
        }

        await db.SaveChangesAsync(cancellationToken);
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
        if (!IsMonthYear(request.MonthYear))
            return BadRequest(new { message = "monthYear phai co dinh dang yyyy-MM." });

        var budget = await db.Budgets.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return NotFound();

        var category = await db.Categories.SingleOrDefaultAsync(
            x => x.Id == request.CategoryId && x.UserId == userContext.UserId,
            cancellationToken);
        if (category is null || category.Type != TransactionType.EXPENSE)
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
        await db.SaveChangesAsync(cancellationToken);
        return Ok(budget.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return NotFound();

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsMonthYear(string value) =>
        value.Length == 7 &&
        value[4] == '-' &&
        int.TryParse(value[..4], out _) &&
        int.TryParse(value[5..], out var month) &&
        month is >= 1 and <= 12;
}
