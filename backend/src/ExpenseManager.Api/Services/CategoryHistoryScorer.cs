namespace ExpenseManager.Api.Services;

public sealed class CategoryHistoryScorer
{
    public const decimal MaximumContribution = 12m;

    public IReadOnlyList<CategoryEvidence> Score(
        IReadOnlyList<SemanticExpenseCategory> history)
    {
        if (history.Count == 0)
            return [];

        var total = history.Count;
        var reliability = Math.Min(1m, total / 5m);
        var result = new List<CategoryEvidence>();
        foreach (var group in history.GroupBy(x => x))
        {
            var probability = group.Count() / (decimal)total;
            var purity = Math.Max(0m, (probability - 0.5m) / 0.5m);
            var contribution = decimal.Round(
                MaximumContribution * reliability * purity,
                2,
                MidpointRounding.AwayFromZero);
            if (contribution <= 0)
                continue;

            result.Add(new CategoryEvidence(
                group.Key,
                contribution,
                CategoryEvidenceSource.USER_HISTORY,
                CategoryRuleKind.MODERATE_PHRASE,
                "user-history-distribution",
                $"{group.Count()}/{total}",
                [],
                reliability * purity,
                $"Người dùng đã chọn nhóm này {group.Count()}/{total} lần cho cùng merchant."));
        }

        return result;
    }
}
