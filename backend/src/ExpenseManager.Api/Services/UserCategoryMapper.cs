using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Services;

public sealed class UserCategoryMapper(CategoryTextNormalizer normalizer)
{
    private static readonly IReadOnlyDictionary<SemanticExpenseCategory, string[]> Names =
        new Dictionary<SemanticExpenseCategory, string[]>
        {
            [SemanticExpenseCategory.FOOD_AND_DRINK] = ["Ăn uống", "Food and drink", "Food & drink"],
            [SemanticExpenseCategory.TRANSPORT] = ["Di chuyển", "Đi lại", "Transport"],
            [SemanticExpenseCategory.SHOPPING] = ["Mua sắm", "Shopping"],
            [SemanticExpenseCategory.HOUSING] = ["Nhà ở", "Housing"],
            [SemanticExpenseCategory.ENTERTAINMENT] = ["Giải trí", "Entertainment"],
            [SemanticExpenseCategory.HEALTH] = ["Sức khỏe", "Y tế", "Health"],
            [SemanticExpenseCategory.EDUCATION] = ["Giáo dục", "Education"],
            [SemanticExpenseCategory.BILLS] = ["Hóa đơn", "Bills", "Utilities"],
            [SemanticExpenseCategory.OTHER] = ["Khác", "Other"]
        };

    public SemanticExpenseCategory? ToSemantic(Category category)
    {
        if (category.Type != TransactionType.EXPENSE)
            return null;

        var normalized = normalizer.Normalize(category.Name);
        foreach (var item in Names)
        {
            if (item.Value.Any(name => normalizer.Normalize(name) == normalized))
                return item.Key;
        }

        return null;
    }

    public Category? FindCategory(
        SemanticExpenseCategory semanticCategory,
        IReadOnlyList<Category> categories)
    {
        if (!Names.TryGetValue(semanticCategory, out var aliases))
            return null;

        var expenseCategories = categories
            .Where(x => x.Type == TransactionType.EXPENSE)
            .ToList();
        foreach (var alias in aliases)
        {
            var normalizedAlias = normalizer.Normalize(alias);
            var matches = expenseCategories
                .Where(x => normalizer.Normalize(x.Name) == normalizedAlias)
                .ToList();
            if (matches.Count == 1)
                return matches[0];
            if (matches.Count > 1)
                return null;
        }

        return null;
    }
}
