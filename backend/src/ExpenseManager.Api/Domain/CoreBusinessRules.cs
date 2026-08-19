namespace ExpenseManager.Api.Domain;

public static class AuthInputRules
{
    public static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public static string NormalizeName(string name) => name.Trim();
}

public static class BudgetRules
{
    public static bool IsValidMonthYear(string? value) =>
        value is { Length: 7 } &&
        value[4] == '-' &&
        int.TryParse(value[..4], out var year) &&
        year >= 1 &&
        int.TryParse(value[5..], out var month) &&
        month is >= 1 and <= 12;

    public static bool CanUseCategory(TransactionType type) =>
        type == TransactionType.EXPENSE;

    public static BudgetAlertLevel? AlertLevel(long budgetAmount, long spentAmount)
    {
        if (budgetAmount <= 0 || spentAmount < 0)
            return null;
        if (spentAmount >= budgetAmount)
            return BudgetAlertLevel.EXCEEDED;
        return spentAmount * 100m >= budgetAmount * 80m
            ? BudgetAlertLevel.APPROACHING
            : null;
    }
}

public static class FinancialCycleRules
{
    public static bool IsValidStartDay(int day) => day is >= 1 and <= 31;

    public static DateOnly StartFor(DateOnly date, int configuredDay)
    {
        if (!IsValidStartDay(configuredDay))
            throw new ArgumentOutOfRangeException(nameof(configuredDay));
        var currentDay = Math.Min(configuredDay, DateTime.DaysInMonth(date.Year, date.Month));
        if (date.Day >= currentDay)
            return new DateOnly(date.Year, date.Month, currentDay);
        var previous = date.AddMonths(-1);
        return new DateOnly(previous.Year, previous.Month,
            Math.Min(configuredDay, DateTime.DaysInMonth(previous.Year, previous.Month)));
    }

    public static DateOnly EndFor(DateOnly cycleStart, int configuredDay)
    {
        var nextMonth = cycleStart.AddMonths(1);
        var nextStart = new DateOnly(nextMonth.Year, nextMonth.Month,
            Math.Min(configuredDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        return nextStart.AddDays(-1);
    }

    public static string KeyFor(DateOnly date, int configuredDay) =>
        StartFor(date, configuredDay).ToString("yyyy-MM");
}

public readonly record struct GoalFundingDecision(
    long RequestedAmount,
    long AppliedAmount,
    long BalanceAfter,
    bool WasAlreadyFunded);

public static class GoalFundingRules
{
    public static GoalFundingDecision Calculate(
        long targetAmount,
        long currentAmount,
        long requestedAmount)
    {
        if (targetAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetAmount));
        if (currentAmount < 0 || currentAmount > targetAmount)
            throw new ArgumentOutOfRangeException(nameof(currentAmount));
        if (requestedAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedAmount));

        var remaining = targetAmount - currentAmount;
        if (remaining == 0)
            return new GoalFundingDecision(requestedAmount, 0, currentAmount, true);
        if (requestedAmount > remaining)
            throw new ArgumentOutOfRangeException(
                nameof(requestedAmount), "Requested amount cannot exceed the remaining goal amount.");
        return new GoalFundingDecision(
            requestedAmount,
            requestedAmount,
            currentAmount + requestedAmount,
            false);
    }
}

public static class StatisticsRules
{
    public static long Balance(long income, long expense) => income - expense;
    public static long AvailableBalance(long income, long expense, long reserved) =>
        Balance(income, expense) - reserved;
}

public static class CategoryRules
{
    public static string NormalizeName(string name) => name.Trim();

    public static string? NormalizeOptionalText(string? value) => value?.Trim();

    public static bool IsSupportedType(TransactionType type) =>
        type is TransactionType.INCOME or TransactionType.EXPENSE;
}
