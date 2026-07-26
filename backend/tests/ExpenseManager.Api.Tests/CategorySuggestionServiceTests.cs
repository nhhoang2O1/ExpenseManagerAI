using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;

namespace ExpenseManager.Api.Tests;

public sealed class CategorySuggestionServiceTests
{
    [Fact]
    public async Task Suggests_food_from_generic_receipt_content_without_merchant_parser()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser("category-content@example.com");
        var food = NewCategory(user, "Ăn uống");
        var transport = NewCategory(user, "Di chuyển");
        db.AddRange(user, food, transport);
        await db.SaveChangesAsync();
        var result = NewOcrResult("TRÀ SỮA PÊ MIN BA", "Trà sữa trân châu\nTổng cộng 25.000");

        var suggestion = await new CategorySuggestionService(db)
            .SuggestAsync(user.Id, result, CancellationToken.None);

        Assert.NotNull(suggestion);
        Assert.Equal(food.Id, suggestion.CategoryId);
        Assert.True(suggestion.Confidence >= 0.82m);
    }

    [Fact]
    public async Task Confirmed_merchant_history_has_priority_over_keywords()
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
            StoreName = "Cửa hàng của tôi"
        };
        db.AddRange(user, food, shopping, transaction);
        await db.SaveChangesAsync();
        var result = NewOcrResult("CUA HANG CUA TOI", "Trà sữa\nTổng cộng 25.000");

        var suggestion = await new CategorySuggestionService(db)
            .SuggestAsync(user.Id, result, CancellationToken.None);

        Assert.NotNull(suggestion);
        Assert.Equal(shopping.Id, suggestion.CategoryId);
        Assert.Equal(0.94m, suggestion.Confidence);
    }

    private static User NewUser(string email) => new()
    {
        Name = email,
        Email = email,
        PasswordHash = "hash"
    };

    private static Category NewCategory(User user, string name) => new()
    {
        User = user,
        UserId = user.Id,
        Name = name,
        Type = TransactionType.EXPENSE
    };

    private static OcrResult NewOcrResult(string store, string rawText) => new()
    {
        ReceiptId = Guid.NewGuid(),
        StoreName = store,
        RawText = rawText,
        LinesJson = "[]",
        OverallConfidence = 0.9m,
        ModelVersion = "test",
        ParserVersion = "generic",
        WarningsJson = "[]"
    };
}
