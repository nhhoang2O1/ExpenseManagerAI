using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

public sealed class FinancialControllerBehaviorTests
{
    [Fact]
    public async Task Budget_upsert_accepts_only_owned_expense_categories()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("owner");
        var anotherUser = User("other");
        var expense = Category(owner, "Food", TransactionType.EXPENSE);
        var income = Category(owner, "Salary", TransactionType.INCOME);
        var foreignExpense = Category(anotherUser, "Foreign", TransactionType.EXPENSE);
        db.AddRange(owner, anotherUser, expense, income, foreignExpense);
        await db.SaveChangesAsync();
        var controller = WithHttpContext(
            new BudgetsController(new ExpenseManager.Api.Services.BudgetsApplicationService(
                db, new TestUserContext(owner.Id))));

        var incomeResult = await controller.CreateOrUpdate(
            new BudgetRequest(income.Id, 1_000_000, "2026-07"),
            CancellationToken.None);
        var foreignResult = await controller.CreateOrUpdate(
            new BudgetRequest(foreignExpense.Id, 1_000_000, "2026-07"),
            CancellationToken.None);
        var invalidMonth = await controller.CreateOrUpdate(
            new BudgetRequest(expense.Id, 1_000_000, "2026-13"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(incomeResult.Result);
        Assert.IsType<BadRequestObjectResult>(foreignResult.Result);
        Assert.IsType<BadRequestObjectResult>(invalidMonth.Result);
        Assert.Empty(await db.Budgets.ToListAsync());

        var created = await controller.CreateOrUpdate(
            new BudgetRequest(expense.Id, 1_000_000, "2026-07"),
            CancellationToken.None);
        var updated = await controller.CreateOrUpdate(
            new BudgetRequest(expense.Id, 1_500_000, "2026-07"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(created.Result).StatusCode);
        Assert.IsType<OkObjectResult>(updated.Result);
        var budget = Assert.Single(await db.Budgets.ToListAsync());
        Assert.Equal(1_500_000, budget.Amount);
        Assert.Equal("\"2\"", controller.Response.Headers.ETag);
    }

    [Fact]
    public async Task Transaction_create_validates_category_and_normalizes_optional_text()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("owner");
        var anotherUser = User("other");
        var expense = Category(owner, "Food", TransactionType.EXPENSE);
        var income = Category(owner, "Salary", TransactionType.INCOME);
        var foreignExpense = Category(anotherUser, "Foreign", TransactionType.EXPENSE);
        db.AddRange(owner, anotherUser, expense, income, foreignExpense);
        await db.SaveChangesAsync();
        var controller = WithHttpContext(
            new TransactionsController(new ExpenseManager.Api.Services.TransactionsApplicationService(
                db, new TestUserContext(owner.Id))));
        var request = new TransactionRequest(
            125_000,
            TransactionType.EXPENSE,
            new DateOnly(2026, 7, 9),
            expense.Id,
            "  lunch  ",
            "  Circle K  ");

        var wrongType = await controller.Create(
            request with { CategoryId = income.Id },
            CancellationToken.None);
        var foreignCategory = await controller.Create(
            request with { CategoryId = foreignExpense.Id },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(wrongType.Result);
        Assert.IsType<BadRequestObjectResult>(foreignCategory.Result);
        Assert.Empty(await db.Transactions.ToListAsync());

        var created = await controller.Create(request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(created.Result).StatusCode);
        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal("lunch", transaction.Note);
        Assert.Equal("Circle K", transaction.StoreName);
        Assert.Equal(owner.Id, transaction.UserId);
        Assert.Equal("\"1\"", controller.Response.Headers.ETag);
    }

    [Fact]
    public async Task Transaction_create_returns_non_blocking_budget_alert()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("budget-alert");
        var expense = Category(owner, "Food", TransactionType.EXPENSE);
        db.AddRange(owner, expense);
        db.Budgets.Add(new Budget
        {
            UserId = owner.Id,
            User = owner,
            CategoryId = expense.Id,
            Category = expense,
            Amount = 50_000,
            MonthYear = "2026-08"
        });
        db.Transactions.Add(Transaction(
            owner, expense, 35_000, new DateOnly(2026, 8, 2)));
        await db.SaveChangesAsync();
        var controller = WithHttpContext(
            new TransactionsController(new ExpenseManager.Api.Services.TransactionsApplicationService(
                db, new TestUserContext(owner.Id))));

        var result = await controller.Create(
            new TransactionRequest(
                20_000,
                TransactionType.EXPENSE,
                new DateOnly(2026, 8, 16),
                expense.Id,
                null,
                null),
            CancellationToken.None);

        var response = Assert.IsType<TransactionResponse>(
            Assert.IsType<ObjectResult>(result.Result).Value);
        Assert.NotNull(response.BudgetAlert);
        Assert.Equal(BudgetAlertLevel.EXCEEDED, response.BudgetAlert.Level);
        Assert.Equal(50_000, response.BudgetAlert.BudgetAmount);
        Assert.Equal(55_000, response.BudgetAlert.SpentAmount);
        Assert.Equal(5_000, response.BudgetAlert.ExceededAmount);
        Assert.Equal(110, response.BudgetAlert.UsagePercent);
        Assert.Equal(2, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Statistics_calculate_balances_and_exclude_other_users()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("owner");
        var anotherUser = User("other");
        var ownerIncome = Category(owner, "Salary", TransactionType.INCOME);
        var ownerExpense = Category(owner, "Food", TransactionType.EXPENSE);
        var foreignExpense = Category(anotherUser, "Foreign", TransactionType.EXPENSE);
        db.AddRange(owner, anotherUser, ownerIncome, ownerExpense, foreignExpense);
        db.Transactions.AddRange(
            Transaction(owner, ownerIncome, 1_000_000, new DateOnly(2026, 7, 1)),
            Transaction(owner, ownerExpense, 250_000, new DateOnly(2026, 7, 1)),
            Transaction(owner, ownerExpense, 100_000, new DateOnly(2026, 7, 2)),
            Transaction(anotherUser, foreignExpense, 9_000_000, new DateOnly(2026, 7, 1)));
        var ownerGoal = new Goal
        {
            UserId = owner.Id,
            User = owner,
            Name = "Laptop",
            TargetAmount = 5_000_000,
            CurrentAmount = 200_000
        };
        var foreignGoal = new Goal
        {
            UserId = anotherUser.Id,
            User = anotherUser,
            Name = "Foreign goal",
            TargetAmount = 9_000_000,
            CurrentAmount = 9_000_000
        };
        db.Goals.AddRange(ownerGoal, foreignGoal);
        db.GoalHistories.AddRange(
            new GoalHistory
            {
                Goal = ownerGoal,
                GoalId = ownerGoal.Id,
                AmountAdded = 200_000,
                ActionType = GoalHistoryActionType.FUND,
                Date = new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc)
            },
            new GoalHistory
            {
                Goal = foreignGoal,
                GoalId = foreignGoal.Id,
                AmountAdded = 9_000_000,
                ActionType = GoalHistoryActionType.FUND,
                Date = new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();
        var controller = new StatisticsController(
            new ExpenseManager.Api.Services.StatisticsApplicationService(
                db, new TestUserContext(owner.Id)));

        var dailyResult = await controller.Daily(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            CancellationToken.None);
        var monthlyResult = await controller.Monthly(2026, CancellationToken.None);
        var categoryResult = await controller.ByCategory(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CancellationToken.None);

        var daily = Assert.IsAssignableFrom<IEnumerable<DailyStatisticResponse>>(
            Assert.IsType<OkObjectResult>(dailyResult.Result).Value).ToList();
        Assert.Collection(
            daily,
            first =>
            {
                Assert.Equal(new DateOnly(2026, 7, 1), first.Date);
                Assert.Equal(1_000_000, first.Income);
                Assert.Equal(250_000, first.Expense);
                Assert.Equal(750_000, first.Balance);
            },
            second =>
            {
                Assert.Equal(new DateOnly(2026, 7, 2), second.Date);
                Assert.Equal(0, second.Income);
                Assert.Equal(100_000, second.Expense);
                Assert.Equal(-100_000, second.Balance);
            });

        var monthly = Assert.Single(Assert.IsAssignableFrom<IEnumerable<MonthlyStatisticResponse>>(
            Assert.IsType<OkObjectResult>(monthlyResult.Result).Value));
        Assert.Equal(1_000_000, monthly.Income);
        Assert.Equal(350_000, monthly.Expense);
        Assert.Equal(200_000, monthly.Savings);
        Assert.Equal(450_000, monthly.Balance);

        var categories = Assert.IsAssignableFrom<IEnumerable<CategoryStatisticResponse>>(
            Assert.IsType<OkObjectResult>(categoryResult.Result).Value).ToList();
        Assert.Equal(2, categories.Count);
        Assert.DoesNotContain(categories, item => item.CategoryId == foreignExpense.Id);
        Assert.Equal(1_000_000, categories[0].Total);
        Assert.Equal(350_000, categories[1].Total);
        Assert.Equal(2, categories[1].TransactionCount);
    }

    [Fact]
    public async Task Invalid_date_ranges_and_month_filters_return_bad_request()
    {
        await using var db = TestSupport.CreateDb();
        var user = User("owner");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var statistics = new StatisticsController(
            new ExpenseManager.Api.Services.StatisticsApplicationService(
                db, new TestUserContext(user.Id)));
        var transactions = new TransactionsController(
            new ExpenseManager.Api.Services.TransactionsApplicationService(
                db, new TestUserContext(user.Id)));

        var daily = await statistics.Daily(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1),
            CancellationToken.None);
        var categories = await statistics.ByCategory(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1),
            CancellationToken.None);
        var month = await transactions.GetAll(
            null, null, "2026-99", null, null, null, 1, 20, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(daily.Result);
        Assert.IsType<BadRequestObjectResult>(categories.Result);
        Assert.IsType<BadRequestObjectResult>(month.Result);
    }

    [Fact]
    public async Task Category_create_rejects_unknown_transaction_type()
    {
        await using var db = TestSupport.CreateDb();
        var user = User("owner");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = WithHttpContext(
            new CategoriesController(new ExpenseManager.Api.Services.CategoriesApplicationService(
                db, new TestUserContext(user.Id))));

        var result = await controller.Create(
            new CategoryRequest("Unknown", (TransactionType)999, null, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Categories.ToListAsync());
    }

    [Fact]
    public async Task Category_create_normalizes_name_color_and_icon()
    {
        await using var db = TestSupport.CreateDb();
        var user = User("owner");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = WithHttpContext(
            new CategoriesController(new ExpenseManager.Api.Services.CategoriesApplicationService(
                db, new TestUserContext(user.Id))));

        var result = await controller.Create(
            new CategoryRequest(
                "  Ăn uống  ", TransactionType.EXPENSE, "  #FF0000  ", "  ic_food  "),
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var category = Assert.Single(await db.Categories.ToListAsync());
        Assert.Equal("Ăn uống", category.Name);
        Assert.Equal("#FF0000", category.Color);
        Assert.Equal("ic_food", category.Icon);
    }

    private static T WithHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static User User(string prefix) => new()
    {
        Name = $"{prefix} user",
        Email = $"{prefix}-{Guid.NewGuid():N}@example.com",
        PasswordHash = "hash"
    };

    private static Category Category(
        User user,
        string name,
        TransactionType type) => new()
    {
        UserId = user.Id,
        User = user,
        Name = name,
        Type = type
    };

    private static Domain.Transaction Transaction(
        User user,
        Category category,
        long amount,
        DateOnly date) => new()
    {
        UserId = user.Id,
        User = user,
        CategoryId = category.Id,
        Category = category,
        Amount = amount,
        Type = category.Type,
        TransactionDate = date
    };
}
