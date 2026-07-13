using ExpenseManager.Api.Data;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

internal static class TestSupport
{
    public static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}

internal sealed class TestUserContext(Guid userId) : IUserContext
{
    public Guid UserId { get; } = userId;
}
