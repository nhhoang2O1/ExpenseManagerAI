using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Mvc;
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
    public async Task Database_rejects_a_transaction_linked_to_another_users_goal()
    {
        await using var db = await fixture.ResetAndCreateDbAsync();
        var owner = NewUser("goal-owner@example.com");
        var otherUser = NewUser("other-goal-owner@example.com");
        var ownerCategory = new Category
        {
            UserId = owner.Id,
            User = owner,
            Name = "Goal expense",
            Type = TransactionType.EXPENSE
        };
        var foreignGoal = new Goal
        {
            UserId = otherUser.Id,
            User = otherUser,
            Name = "Foreign goal",
            TargetAmount = 100_000,
            CurrentAmount = 100_000,
            Status = GoalStatus.READY_TO_COMPLETE
        };
        db.AddRange(owner, otherUser, ownerCategory, foreignGoal);
        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            UserId = owner.Id,
            CategoryId = ownerCategory.Id,
            GoalId = foreignGoal.Id,
            Amount = 100_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = new DateOnly(2026, 8, 11)
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

    [PostgreSqlFact]
    public async Task Concurrent_goal_funding_for_one_user_cannot_overreserve_balance()
    {
        await using (var seedDb = await fixture.ResetAndCreateDbAsync())
        {
            var user = NewUser("concurrent-goals@example.com");
            var incomeCategory = new Category
            {
                UserId = user.Id,
                User = user,
                Name = "Income",
                Type = TransactionType.INCOME
            };
            var firstGoal = new Goal
            {
                UserId = user.Id,
                User = user,
                Name = "First goal",
                TargetAmount = 100
            };
            var secondGoal = new Goal
            {
                UserId = user.Id,
                User = user,
                Name = "Second goal",
                TargetAmount = 100
            };
            seedDb.AddRange(user, incomeCategory, firstGoal, secondGoal, new Transaction
            {
                UserId = user.Id,
                User = user,
                CategoryId = incomeCategory.Id,
                Category = incomeCategory,
                Amount = 100,
                Type = TransactionType.INCOME,
                TransactionDate = new DateOnly(2026, 8, 11)
            });
            await seedDb.SaveChangesAsync();

            await using var firstDb = fixture.CreateDb();
            await using var secondDb = fixture.CreateDb();
            var firstController = new GoalsController(
                new ExpenseManager.Api.Services.GoalsApplicationService(
                    firstDb, new TestUserContext(user.Id)));
            var secondController = new GoalsController(
                new ExpenseManager.Api.Services.GoalsApplicationService(
                    secondDb, new TestUserContext(user.Id)));

            var results = await Task.WhenAll(
                firstController.AddFunds(
                    firstGoal.Id, new(80), CancellationToken.None),
                secondController.AddFunds(
                    secondGoal.Id, new(80), CancellationToken.None));

            Assert.Single(results, result => result.Result is OkObjectResult);
            Assert.Single(results, result => result.Result is ConflictObjectResult);

            await using var verificationDb = fixture.CreateDb();
            var reserved = await verificationDb.Goals
                .Where(x => x.UserId == user.Id)
                .SumAsync(x => x.CurrentAmount);
            Assert.Equal(80, reserved);
        }
    }

    private static User NewUser(string email) => new()
    {
        Name = "Database Integrity Tester",
        Email = email,
        PasswordHash = "hash"
    };
}
