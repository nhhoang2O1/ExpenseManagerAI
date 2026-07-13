using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Tests;

public sealed class UserIsolationTests
{
    [Fact]
    public async Task Transaction_queries_and_updates_do_not_expose_another_user()
    {
        await using var db = TestSupport.CreateDb();
        var firstUser = NewUser("first@example.com");
        var secondUser = NewUser("second@example.com");
        var firstCategory = NewCategory(firstUser);
        var secondCategory = NewCategory(secondUser);
        var own = NewTransaction(firstUser, firstCategory, "Mine");
        var foreign = NewTransaction(secondUser, secondCategory, "Hidden");
        db.AddRange(firstUser, secondUser, firstCategory, secondCategory, own, foreign);
        await db.SaveChangesAsync();
        var controller = new TransactionsController(db, new TestUserContext(firstUser.Id));

        var response = await controller.GetAll(
            null, null, null, null, null, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var page = Assert.IsType<PagedResponse<TransactionResponse>>(ok.Value);
        var item = Assert.Single(page.Items);
        Assert.Equal(own.Id, item.Id);

        var update = await controller.Update(
            foreign.Id,
            new TransactionRequest(
                999, TransactionType.EXPENSE, new DateOnly(2026, 7, 9),
                firstCategory.Id, null, null),
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(update.Result);
        Assert.Equal(200_000, foreign.Amount);
    }

    private static User NewUser(string email) => new()
    {
        Name = email,
        Email = email,
        PasswordHash = "hash"
    };

    private static Category NewCategory(User user) => new()
    {
        UserId = user.Id,
        User = user,
        Name = "Food",
        Type = TransactionType.EXPENSE
    };

    private static Domain.Transaction NewTransaction(
        User user,
        Category category,
        string note) => new()
    {
        UserId = user.Id,
        User = user,
        CategoryId = category.Id,
        Category = category,
        Amount = 200_000,
        Type = TransactionType.EXPENSE,
        TransactionDate = new DateOnly(2026, 7, 9),
        Note = note
    };
}
