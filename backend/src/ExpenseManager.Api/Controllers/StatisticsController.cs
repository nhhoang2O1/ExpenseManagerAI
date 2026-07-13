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
[Route("api/statistics")]
public sealed class StatisticsController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<ActionResult<IReadOnlyList<DailyStatisticResponse>>> Daily(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var end = to ?? LocalToday();
        var start = from ?? end.AddDays(-29);
        if (start > end)
            return BadRequest(new { message = "from không thể sau to." });

        var rows = await OwnedTransactions(start, end)
            .GroupBy(x => x.TransactionDate)
            .Select(group => new
            {
                Date = group.Key,
                Income = group.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount),
                Expense = group.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x =>
            new DailyStatisticResponse(x.Date, x.Income, x.Expense, x.Income - x.Expense)));
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<IReadOnlyList<MonthlyStatisticResponse>>> Monthly(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var selectedYear = year ?? LocalToday().Year;
        var start = new DateOnly(selectedYear, 1, 1);
        var end = start.AddYears(1).AddDays(-1);
        var rows = await OwnedTransactions(start, end)
            .GroupBy(x => new { x.TransactionDate.Year, x.TransactionDate.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Income = group.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount),
                Expense = group.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount)
            })
            .OrderBy(x => x.Month)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x =>
            new MonthlyStatisticResponse(x.Year, x.Month, x.Income, x.Expense, x.Income - x.Expense)));
    }

    [HttpGet("by-category")]
    public async Task<ActionResult<IReadOnlyList<CategoryStatisticResponse>>> ByCategory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var end = to ?? LocalToday();
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end)
            return BadRequest(new { message = "from không thể sau to." });

        var rows = await OwnedTransactions(start, end)
            .GroupBy(x => new
            {
                x.CategoryId,
                x.Category.Name,
                x.Type,
                x.Category.Color,
                x.Category.Icon
            })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.Name,
                group.Key.Type,
                Total = group.Sum(x => x.Amount),
                TransactionCount = group.Count(),
                group.Key.Color,
                group.Key.Icon
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new CategoryStatisticResponse(
            x.CategoryId,
            x.Name,
            x.Type,
            x.Total,
            x.TransactionCount,
            x.Color,
            x.Icon)));
    }

    private IQueryable<Domain.Transaction> OwnedTransactions(DateOnly start, DateOnly end) =>
        db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId &&
                 x.TransactionDate >= start && x.TransactionDate <= end);

    private static DateOnly LocalToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }
}
