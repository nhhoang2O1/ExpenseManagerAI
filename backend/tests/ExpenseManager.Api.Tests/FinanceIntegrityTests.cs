using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

public sealed class FinanceIntegrityTests
{
    [Fact]
    public async Task Referenced_category_cannot_change_type_or_be_deleted()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var category = NewCategory(user, TransactionType.EXPENSE);
        db.AddRange(user, category, new Budget
        {
            UserId = user.Id,
            User = user,
            CategoryId = category.Id,
            Category = category,
            Amount = 1_000_000,
            MonthYear = "2026-07"
        });
        await db.SaveChangesAsync();
        var controller = new CategoriesController(
            new ExpenseManager.Api.Services.CategoriesApplicationService(
                db, new TestUserContext(user.Id)));

        var update = await controller.Update(
            category.Id,
            new CategoryRequest(category.Name, TransactionType.INCOME, null, null),
            CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(update.Result);
        Assert.Equal(TransactionType.EXPENSE, category.Type);

        var delete = await controller.Delete(category.Id, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(delete);
        Assert.True(await db.Categories.AnyAsync(x => x.Id == category.Id));
    }

    [Fact]
    public async Task Add_funds_above_remaining_amount_is_rejected_without_changes()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var incomeCategory = NewCategory(user, TransactionType.INCOME);
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Emergency fund",
            TargetAmount = 100,
            CurrentAmount = 80
        };
        db.AddRange(user, incomeCategory, goal, new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = incomeCategory.Id,
            Category = incomeCategory,
            Amount = 1_000,
            Type = TransactionType.INCOME,
            TransactionDate = new DateOnly(2026, 8, 6)
        });
        await db.SaveChangesAsync();
        var controller = new GoalsController(
            new ExpenseManager.Api.Services.GoalsApplicationService(
                db, new TestUserContext(user.Id)));

        var response = await controller.AddFunds(
            goal.Id, new AddGoalFundsRequest(50), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(80, goal.CurrentAmount);
        Assert.Empty(await db.GoalHistories.ToListAsync());
    }

    [Fact]
    public async Task Mutation_with_stale_if_match_returns_precondition_failed()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var incomeCategory = NewCategory(user, TransactionType.INCOME);
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Emergency fund",
            TargetAmount = 100,
            Version = 7
        };
        db.AddRange(user, incomeCategory, goal, new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = incomeCategory.Id,
            Category = incomeCategory,
            Amount = 100,
            Type = TransactionType.INCOME,
            TransactionDate = new DateOnly(2026, 8, 6)
        });
        await db.SaveChangesAsync();
        var controller = new GoalsController(new ExpenseManager.Api.Services.GoalsApplicationService(
            db, new TestUserContext(user.Id)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["If-Match"] = "\"6\"";

        var response = await controller.Update(
            goal.Id,
            new GoalRequest("Changed", 100),
            CancellationToken.None);

        var failed = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, failed.StatusCode);
        Assert.Equal("Emergency fund", goal.Name);
    }

    [Fact]
    public async Task Create_transaction_replays_same_idempotency_key_once()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var category = NewCategory(user, TransactionType.EXPENSE);
        db.AddRange(user, category);
        await db.SaveChangesAsync();
        var controller = new TransactionsController(new ExpenseManager.Api.Services.TransactionsApplicationService(
            db, new TestUserContext(user.Id)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["Idempotency-Key"] = "transaction-1";
        var request = new TransactionRequest(
            25_000,
            TransactionType.EXPENSE,
            new DateOnly(2026, 7, 16),
            category.Id,
            "Lunch",
            null);

        var first = await controller.Create(request, CancellationToken.None);
        var second = await controller.Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(first.Result);
        var replay = Assert.IsType<ObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status201Created, replay.StatusCode);
        Assert.Single(await db.Transactions.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());

        var conflict = await controller.Create(
            request with { Amount = 30_000 }, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(conflict.Result);
        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Add_funds_replays_same_idempotency_key_without_double_applying()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var incomeCategory = NewCategory(user, TransactionType.INCOME);
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Emergency fund",
            TargetAmount = 100,
            CurrentAmount = 10
        };
        db.AddRange(user, incomeCategory, goal, new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = incomeCategory.Id,
            Category = incomeCategory,
            Amount = 100,
            Type = TransactionType.INCOME,
            TransactionDate = new DateOnly(2026, 8, 6)
        });
        await db.SaveChangesAsync();
        var controller = new GoalsController(new ExpenseManager.Api.Services.GoalsApplicationService(
            db, new TestUserContext(user.Id)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["Idempotency-Key"] = "funds-1";

        await controller.AddFunds(goal.Id, new AddGoalFundsRequest(20), CancellationToken.None);
        await controller.AddFunds(goal.Id, new AddGoalFundsRequest(20), CancellationToken.None);

        Assert.Equal(30, goal.CurrentAmount);
        Assert.Single(await db.GoalHistories.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task Completing_goal_only_updates_goal_and_history()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Laptop",
            TargetAmount = 500,
            CurrentAmount = 500,
            Status = GoalStatus.READY_TO_COMPLETE
        };
        db.AddRange(user, goal);
        await db.SaveChangesAsync();
        var controller = new GoalsController(
            new ExpenseManager.Api.Services.GoalsApplicationService(
                db, new TestUserContext(user.Id)));

        var response = await controller.Complete(
            goal.Id, new CompleteGoalRequest(), CancellationToken.None);

        var result = Assert.IsType<GoalResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(GoalStatus.COMPLETED, result.Status);
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Single(await db.GoalHistories.Where(x => x.ActionType == GoalHistoryActionType.COMPLETE)
            .ToListAsync());
    }

    [Fact]
    public async Task Add_funds_does_not_depend_on_transaction_balance()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var expenseCategory = NewCategory(user, TransactionType.EXPENSE);
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Travel",
            TargetAmount = 100,
            CurrentAmount = 10
        };
        db.AddRange(user, expenseCategory, goal, new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = expenseCategory.Id,
            Category = expenseCategory,
            Amount = 1_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = new DateOnly(2026, 8, 6)
        });
        await db.SaveChangesAsync();
        var controller = new GoalsController(
            new ExpenseManager.Api.Services.GoalsApplicationService(
                db, new TestUserContext(user.Id)));

        var response = await controller.AddFunds(
            goal.Id, new AddGoalFundsRequest(50), CancellationToken.None);

        var result = Assert.IsType<GoalResponse>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(60, result.CurrentAmount);
    }

    [Fact]
    public async Task Cancelling_goal_releases_reserved_amount_without_creating_transaction()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var incomeCategory = NewCategory(user, TransactionType.INCOME);
        var goal = new Goal
        {
            UserId = user.Id,
            User = user,
            Name = "Trip",
            TargetAmount = 500,
            CurrentAmount = 300,
            Status = GoalStatus.ACTIVE
        };
        db.AddRange(user, incomeCategory, goal, new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = incomeCategory.Id,
            Category = incomeCategory,
            Amount = 500,
            Type = TransactionType.INCOME,
            TransactionDate = new DateOnly(2026, 8, 6)
        });
        await db.SaveChangesAsync();
        var controller = new GoalsController(
            new ExpenseManager.Api.Services.GoalsApplicationService(
                db, new TestUserContext(user.Id)));

        var response = await controller.Cancel(goal.Id, CancellationToken.None);

        var result = Assert.IsType<GoalResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(GoalStatus.CANCELLED, result.Status);
        Assert.Empty(await db.Transactions.Where(x => x.GoalId == goal.Id).ToListAsync());
        Assert.Single(await db.GoalHistories.Where(x => x.ActionType == GoalHistoryActionType.CANCEL)
            .ToListAsync());
    }

    [Fact]
    public async Task Transaction_pages_have_stable_tie_breaker_without_missing_items()
    {
        await using var db = TestSupport.CreateDb();
        var user = NewUser();
        var category = NewCategory(user, TransactionType.EXPENSE);
        var timestamp = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var transactionDate = new DateOnly(2026, 7, 16);
        var transactions = Enumerable.Range(1, 150).Select(number => new Domain.Transaction
        {
            Id = GuidFrom(number),
            UserId = user.Id,
            User = user,
            CategoryId = category.Id,
            Category = category,
            Amount = number,
            Type = TransactionType.EXPENSE,
            TransactionDate = transactionDate,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        }).ToArray();
        db.AddRange(user, category);
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
        var controller = new TransactionsController(
            new ExpenseManager.Api.Services.TransactionsApplicationService(
                db, new TestUserContext(user.Id)));

        var first = await Page(controller, 1, 100);
        var second = await Page(controller, 2, 100);

        Assert.Equal(150, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(100, first.Items.Count);
        Assert.Equal(50, second.Items.Count);
        Assert.Equal(150, first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count());
    }

    private static async Task<PagedResponse<TransactionResponse>> Page(
        TransactionsController controller,
        int page,
        int pageSize)
    {
        var response = await controller.GetAll(
            null, null, null, null, null, null, page, pageSize, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<PagedResponse<TransactionResponse>>(ok.Value);
    }

    private static User NewUser() => new()
    {
        Name = "Finance Tester",
        Email = $"finance-{Guid.NewGuid():N}@example.com",
        PasswordHash = "hash"
    };

    private static Category NewCategory(User user, TransactionType type) => new()
    {
        UserId = user.Id,
        User = user,
        Name = "Food",
        Type = type
    };

    private static Guid GuidFrom(int value)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
