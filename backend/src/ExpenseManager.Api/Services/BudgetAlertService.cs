using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public static class BudgetAlertService
{
    public static async Task<BudgetAlertResponse?> EvaluateProjectedAsync(
        AppDbContext db,
        Guid userId,
        Guid categoryId,
        DateOnly transactionDate,
        long projectedAmount,
        Guid? excludedTransactionId,
        CancellationToken cancellationToken)
    {
        var startDay = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.FinancialCycleStartDay)
            .SingleOrDefaultAsync(cancellationToken);
        if (!FinancialCycleRules.IsValidStartDay(startDay)) startDay = 1;
        var cycleStart = FinancialCycleRules.StartFor(transactionDate, startDay);
        var cycleEnd = FinancialCycleRules.EndFor(cycleStart, startDay);
        var monthYear = $"{cycleStart.Year:0000}-{cycleStart.Month:00}";
        var budget = await db.Budgets.AsNoTracking()
            .Include(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.CategoryId == categoryId &&
                     x.MonthYear == monthYear,
                cancellationToken);
        if (budget is null)
            return null;

        var query = db.Transactions.AsNoTracking().Where(
            x => x.UserId == userId && x.CategoryId == categoryId &&
                 x.Type == TransactionType.EXPENSE &&
                 x.TransactionDate >= cycleStart && x.TransactionDate <= cycleEnd);
        if (excludedTransactionId.HasValue)
            query = query.Where(x => x.Id != excludedTransactionId.Value);
        var existingSpent = await query.SumAsync(x => (long?)x.Amount, cancellationToken) ?? 0L;
        var spent = existingSpent + Math.Max(0L, projectedAmount);
        var level = BudgetRules.AlertLevel(budget.Amount, spent);
        if (!level.HasValue)
            return null;

        var remaining = Math.Max(0L, budget.Amount - spent);
        var exceeded = Math.Max(0L, spent - budget.Amount);
        var usagePercent = budget.Amount <= 0
            ? 0
            : (int)Math.Min(int.MaxValue, Math.Round(spent * 100m / budget.Amount));
        return new BudgetAlertResponse(
            level.Value,
            budget.Id,
            budget.CategoryId,
            budget.Category.Name,
            budget.Amount,
            spent,
            remaining,
            exceeded,
            usagePercent);
    }
}
