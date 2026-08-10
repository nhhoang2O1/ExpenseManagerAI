using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;

namespace ExpenseManager.Api.Tests;

public sealed class CategorySuggestionServiceTests
{
    [Fact]
    public async Task Suggests_food_from_generic_receipt_content_without_brand_mapping()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser("category-content@example.com");
        var food = NewCategory(user, "Ăn uống");
        var transport = NewCategory(user, "Di chuyển");
        db.AddRange(user, food, transport);
        await db.SaveChangesAsync();
        var result = NewOcrResult(
            "Tiệm Trà Mùa Hè",
            "MILK TEA\nMANG VỀ\nSIZE L\nTOPPING TRÂN CHÂU\nTổng cộng 25.000");

        var suggestion = await new CategorySuggestionService(db)
            .SuggestAsync(user.Id, result, CancellationToken.None);

        Assert.NotNull(suggestion);
        Assert.Equal(food.Id, suggestion.CategoryId);
        Assert.True(suggestion.Confidence >= 0.80m);
    }

    [Fact]
    public async Task One_history_row_does_not_override_strong_receipt_content()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser("category-history@example.com");
        var food = NewCategory(user, "Ăn uống");
        var shopping = NewCategory(user, "Mua sắm");
        var transaction = new Transaction
        {
            User = user,
            UserId = user.Id,
            Category = shopping,
            CategoryId = shopping.Id,
            Amount = 25_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreName = "Quán Mùa Hè"
        };
        db.AddRange(user, food, shopping, transaction);
        await db.SaveChangesAsync();
        var result = NewOcrResult(
            "Quán Mùa Hè",
            "RESTAURANT ORDER\nDINE IN\nCOMBO MEAL\nTổng cộng 25.000");

        var service = new CategorySuggestionService(db);
        var analysis = await service.AnalyzeAsync(user.Id, result, CancellationToken.None);
        var suggestion = await service.SuggestAsync(user.Id, result, CancellationToken.None);

        Assert.NotNull(analysis);
        Assert.Equal(SemanticExpenseCategory.FOOD_AND_DRINK,
            analysis.Decision.SemanticCategory);
        Assert.NotNull(suggestion);
        Assert.Equal(food.Id, suggestion.CategoryId);
        var history = analysis.Candidates
            .Single(x => x.Category == SemanticExpenseCategory.SHOPPING)
            .Evidence.Single(x => x.Source == CategoryEvidenceSource.USER_HISTORY);
        Assert.Equal(2.4m, history.Contribution);
    }

    [Fact]
    public async Task Current_receipt_transaction_is_excluded_from_history()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser("category-leakage@example.com");
        var food = NewCategory(user, "Ăn uống");
        var receiptId = Guid.NewGuid();
        var result = NewOcrResult("Unknown Store", "TOTAL 10.000", receiptId);
        var transaction = new Transaction
        {
            User = user,
            UserId = user.Id,
            Category = food,
            CategoryId = food.Id,
            ReceiptId = receiptId,
            Amount = 10_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreName = "Unknown Store"
        };
        db.AddRange(user, food, transaction);
        await db.SaveChangesAsync();

        var analysis = await new CategorySuggestionService(db)
            .AnalyzeAsync(user.Id, result, CancellationToken.None);

        Assert.NotNull(analysis);
        Assert.False(analysis.Decision.Accepted);
        Assert.DoesNotContain(
            analysis.Candidates.SelectMany(x => x.Evidence),
            x => x.Source == CategoryEvidenceSource.USER_HISTORY);
    }

    internal static User NewUser(string email) => new()
    {
        Name = email,
        Email = email,
        PasswordHash = "hash"
    };

    internal static Category NewCategory(User user, string name) => new()
    {
        User = user,
        UserId = user.Id,
        Name = name,
        Type = TransactionType.EXPENSE
    };

    internal static OcrResult NewOcrResult(
        string store,
        string rawText,
        Guid? receiptId = null) => new()
    {
        ReceiptId = receiptId ?? Guid.NewGuid(),
        StoreName = store,
        RawText = rawText,
        LinesJson = "[]",
        OverallConfidence = 0.9m,
        ModelVersion = "test",
        ParserVersion = "generic",
        WarningsJson = "[]"
    };
}
