using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSQLIntegration")]
public sealed class DatabaseIntegrityPostgreSqlTests(PostgreSqlIntegrationFixture fixture)
{
    [PostgreSqlFact]
    public async Task New_user_is_unverified_by_default_in_the_database()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var user = NewUser("unverified@example.com");
        db.Users.Add(user);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.False(await db.Users.Where(x => x.Id == user.Id)
            .Select(x => x.IsEmailVerified).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task Database_rejects_a_transaction_linked_to_another_users_category()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var owner = NewUser("owner@example.com");
        var otherUser = NewUser("other@example.com");
        var foreignCategory = new Category
        {
            UserId = otherUser.Id,
            User = otherUser,
            Name = "Foreign",
            Type = TransactionType.EXPENSE
        };
        db.AddRange(owner, otherUser, foreignCategory);
        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            UserId = owner.Id,
            CategoryId = foreignCategory.Id,
            Amount = 25_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = new DateOnly(2026, 7, 30)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task Database_rejects_non_positive_financial_amounts()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var user = NewUser("amount@example.com");
        var category = new Category
        {
            UserId = user.Id,
            User = user,
            Name = "Food",
            Type = TransactionType.EXPENSE
        };
        db.AddRange(user, category);
        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            CategoryId = category.Id,
            Amount = 0,
            Type = TransactionType.EXPENSE,
            TransactionDate = new DateOnly(2026, 7, 30)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static User NewUser(string email) => new()
    {
        Name = "Database Integrity Tester",
        Email = email,
        PasswordHash = "hash"
    };
}
