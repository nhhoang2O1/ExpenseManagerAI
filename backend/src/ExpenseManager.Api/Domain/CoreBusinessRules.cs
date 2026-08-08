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
        var applied = Math.Min(requestedAmount, remaining);
        return new GoalFundingDecision(
            requestedAmount,
            applied,
            currentAmount + applied,
            remaining == 0);
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
