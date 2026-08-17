using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface IStatisticsApplicationService
{
    Task<ApplicationServiceResult<IReadOnlyList<DailyStatisticResponse>>> GetDailyAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<IReadOnlyList<MonthlyStatisticResponse>>> GetMonthlyAsync(
        int? year, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<IReadOnlyList<CategoryStatisticResponse>>> GetByCategoryAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}

public sealed class StatisticsApplicationService(AppDbContext db, IUserContext userContext)
    : IStatisticsApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<DailyStatisticResponse>>> GetDailyAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var end = to ?? LocalToday();
        var start = from ?? end.AddDays(-29);
        if (start > end)
            return ApplicationServiceResult<IReadOnlyList<DailyStatisticResponse>>.BadRequest(
                "from không thể sau to.");

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

        IReadOnlyList<DailyStatisticResponse> result = rows.Select(x =>
            new DailyStatisticResponse(
                x.Date, x.Income, x.Expense, StatisticsRules.Balance(x.Income, x.Expense)))
            .ToList();
        return ApplicationServiceResult<IReadOnlyList<DailyStatisticResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<IReadOnlyList<MonthlyStatisticResponse>>> GetMonthlyAsync(
        int? year, CancellationToken cancellationToken)
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

        var zone = LocalZone();
        var localStart = DateTime.SpecifyKind(new DateTime(selectedYear, 1, 1), DateTimeKind.Unspecified);
        var localEnd = localStart.AddYears(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        var fundHistory = await db.GoalHistories.AsNoTracking()
            .Where(x => x.Goal.UserId == userContext.UserId &&
                        x.ActionType == GoalHistoryActionType.FUND &&
                        x.Date >= utcStart && x.Date < utcEnd)
            .Select(x => new { x.Date, x.AmountAdded })
            .ToListAsync(cancellationToken);
        var savingsByMonth = fundHistory
            .GroupBy(x => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(x.Date, DateTimeKind.Utc), zone).Month)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.AmountAdded));
        var transactionsByMonth = rows.ToDictionary(x => x.Month);
        var months = transactionsByMonth.Keys.Union(savingsByMonth.Keys).OrderBy(x => x);

        IReadOnlyList<MonthlyStatisticResponse> result = months.Select(month =>
        {
            transactionsByMonth.TryGetValue(month, out var transaction);
            var income = transaction?.Income ?? 0;
            var expense = transaction?.Expense ?? 0;
            var savings = savingsByMonth.GetValueOrDefault(month);
            return new MonthlyStatisticResponse(
                selectedYear, month, income, expense, savings,
                StatisticsRules.Remaining(income, expense, savings));
        })
            .ToList();
        return ApplicationServiceResult<IReadOnlyList<MonthlyStatisticResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<IReadOnlyList<CategoryStatisticResponse>>> GetByCategoryAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var end = to ?? LocalToday();
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end)
            return ApplicationServiceResult<IReadOnlyList<CategoryStatisticResponse>>.BadRequest(
                "from không thể sau to.");

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

        IReadOnlyList<CategoryStatisticResponse> result = rows.Select(x => new CategoryStatisticResponse(
            x.CategoryId, x.Name, x.Type, x.Total, x.TransactionCount, x.Color, x.Icon)).ToList();
        return ApplicationServiceResult<IReadOnlyList<CategoryStatisticResponse>>.Ok(result);
    }

    private IQueryable<Transaction> OwnedTransactions(DateOnly start, DateOnly end) =>
        db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId &&
                 x.TransactionDate >= start && x.TransactionDate <= end);

    private static DateOnly LocalToday()
    {
        var zone = LocalZone();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }

    private static TimeZoneInfo LocalZone() => TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
}
