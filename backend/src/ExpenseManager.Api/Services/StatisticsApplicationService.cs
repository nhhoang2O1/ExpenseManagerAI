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
        var startDay = await db.Users.AsNoTracking()
            .Where(x => x.Id == userContext.UserId)
            .Select(x => x.FinancialCycleStartDay)
            .SingleOrDefaultAsync(cancellationToken);
        if (!FinancialCycleRules.IsValidStartDay(startDay)) startDay = 1;
        var firstCycle = FinancialCycleRules.StartFor(
            new DateOnly(selectedYear, 1, DateTime.DaysInMonth(selectedYear, 1)), startDay);
        var lastCycle = FinancialCycleRules.StartFor(
            new DateOnly(selectedYear, 12, 31), startDay);
        var transactions = await OwnedTransactions(firstCycle, FinancialCycleRules.EndFor(lastCycle, startDay))
            .ToListAsync(cancellationToken);

        var result = new List<MonthlyStatisticResponse>();
        for (var month = 1; month <= 12; month++)
        {
            var cycleStart = FinancialCycleRules.StartFor(
                new DateOnly(selectedYear, month, DateTime.DaysInMonth(selectedYear, month)), startDay);
            var cycleEnd = FinancialCycleRules.EndFor(cycleStart, startDay);
            var income = transactions.Where(x => x.TransactionDate >= cycleStart &&
                    x.TransactionDate <= cycleEnd && x.Type == TransactionType.INCOME)
                .Sum(x => x.Amount);
            var expense = transactions.Where(x => x.TransactionDate >= cycleStart &&
                    x.TransactionDate <= cycleEnd && x.Type == TransactionType.EXPENSE)
                .Sum(x => x.Amount);
            if (income != 0 || expense != 0)
                result.Add(new MonthlyStatisticResponse(
                    cycleStart.Year, cycleStart.Month, income, expense,
                    StatisticsRules.Balance(income, expense)));
        }
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
