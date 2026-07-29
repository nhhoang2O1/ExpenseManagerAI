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
            new BudgetsController(db, new TestUserContext(owner.Id)));

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
            new TransactionsController(db, new TestUserContext(owner.Id)));
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
        await db.SaveChangesAsync();
        var controller = new StatisticsController(db, new TestUserContext(owner.Id));

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
        Assert.Equal(650_000, monthly.Balance);

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
        var statistics = new StatisticsController(db, new TestUserContext(user.Id));
        var transactions = new TransactionsController(db, new TestUserContext(user.Id));

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
