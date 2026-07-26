using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Register_then_login_returns_token_and_seeds_categories()
    {
        await using var db = TestSupport.CreateDb();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-with-at-least-thirty-two-bytes",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "tests"
            })
            .Build();
        var controller = new AuthController(
            db,
            new PasswordHasher<User>(),
            new StubSessionService(new JwtTokenService(configuration)),
            null);

        var registered = await controller.Register(
            new RegisterRequest("Nguyen Van A", "USER@EXAMPLE.COM", "strong-password"),
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(registered.Result);
        Assert.Equal(201, created.StatusCode);
        var auth = Assert.IsType<AuthSessionResponse>(created.Value);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.Equal(900, auth.ExpiresIn);
        Assert.Equal("user@example.com", auth.User.Email);
        Assert.Equal(14, await db.Categories.CountAsync(x => x.UserId == auth.User.Id));
        Assert.Equal(9, await db.Categories.CountAsync(
            x => x.UserId == auth.User.Id && x.Type == TransactionType.EXPENSE));
        Assert.Equal(5, await db.Categories.CountAsync(
            x => x.UserId == auth.User.Id && x.Type == TransactionType.INCOME));

        var missingCategory = await db.Categories.FirstAsync(
            x => x.UserId == auth.User.Id && x.Name == "Nhà ở");
        db.Categories.Remove(missingCategory);
        db.Categories.Add(new Category
        {
            UserId = auth.User.Id,
            Name = "Thu nhập khác",
            Type = TransactionType.INCOME,
            Color = "#14B8A6",
            Icon = "account_balance_wallet"
        });
        await db.SaveChangesAsync();
        Assert.Equal(14, await db.Categories.CountAsync(x => x.UserId == auth.User.Id));

        var login = await controller.Login(
            new LoginRequest("user@example.com", "strong-password"),
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(login.Result);
        var loggedIn = Assert.IsType<AuthSessionResponse>(ok.Value);
        Assert.Equal(auth.User, loggedIn.User);
        Assert.False(string.IsNullOrWhiteSpace(loggedIn.AccessToken));
        Assert.Equal(14, await db.Categories.CountAsync(x => x.UserId == auth.User.Id));
        Assert.False(await db.Categories.AnyAsync(
            x => x.UserId == auth.User.Id && x.Name == "Thu nhập khác"));
        Assert.True(await db.Categories.AnyAsync(
            x => x.UserId == auth.User.Id &&
                 x.Type == TransactionType.INCOME &&
                 x.Name == "Khác"));
    }

    private sealed class StubSessionService(IJwtTokenService jwtTokenService)
        : IAuthSessionService
    {
        public Task<AuthSessionResponse> CreateAsync(
            User user,
            string? ipAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AuthSessionResponse(
                jwtTokenService.Create(user),
                $"test-refresh-token-{Guid.NewGuid():N}",
                900,
                new UserResponse(user.Id, user.Name, user.Email)));

        public Task<RefreshSessionResult> RotateAsync(
            string rawRefreshToken,
            string? ipAddress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeAsync(
            string rawRefreshToken,
            string? ipAddress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> RevokeAllAsync(
            Guid userId,
            string reason,
            string? ipAddress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
