using System.IdentityModel.Tokens.Jwt;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseManager.Api.Tests;

public sealed class AuthSecurityTests
{
    [Fact]
    public async Task Refresh_rotates_stored_token_and_reuse_revokes_every_session()
    {
        await using var db = TestSupport.CreateDb();
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var generator = new DeterministicSecurityTokenGenerator();
        var sessionService = CreateSessionService(db, time, generator);
        var user = CreateUser("refresh@example.com", "initial-password");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var first = await sessionService.CreateAsync(user, "127.0.0.1", default);
        var firstRefreshToken = Assert.IsType<string>(first.RefreshToken);

        Assert.Equal(900, first.ExpiresIn);
        Assert.NotEqual(firstRefreshToken, db.RefreshTokens.Single().TokenHash);
        Assert.Equal(
            time.GetUtcNow().UtcDateTime.AddDays(30),
            db.RefreshTokens.Single().ExpiresAt);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken);
        Assert.Equal("0", jwt.Claims.Single(x => x.Type == JwtClaimNames.TokenVersion).Value);

        time.Advance(TimeSpan.FromMinutes(1));
        var rotated = await sessionService.RotateAsync(
            firstRefreshToken,
            "127.0.0.2",
            default);

        Assert.Equal(RefreshSessionStatus.SUCCESS, rotated.Status);
        Assert.NotNull(rotated.Session);
        Assert.NotEqual(first.RefreshToken, rotated.Session!.RefreshToken);
        var persisted = await db.RefreshTokens.OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.NotNull(persisted[0].RevokedAt);
        Assert.Equal("Rotated", persisted[0].RevokedReason);
        Assert.Equal(persisted[1].Id, persisted[0].ReplacedByTokenId);
        Assert.Null(persisted[1].RevokedAt);

        var replay = await sessionService.RotateAsync(
            firstRefreshToken,
            "127.0.0.3",
            default);

        Assert.Equal(RefreshSessionStatus.REUSED, replay.Status);
        Assert.All(await db.RefreshTokens.ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.Equal(1, (await db.Users.SingleAsync()).TokenVersion);

        // Repeating the already-handled replay must not keep incrementing the
        // security version and create an unbounded account-lockout primitive.
        await sessionService.RotateAsync(firstRefreshToken, "127.0.0.3", default);
        Assert.Equal(1, (await db.Users.SingleAsync()).TokenVersion);
    }

    [Fact]
    public async Task Password_reset_is_neutral_limited_single_use_and_revokes_sessions()
    {
        await using var db = TestSupport.CreateDb();
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var generator = new DeterministicSecurityTokenGenerator(
            "123456",
            "654321",
            "111111");
        var sender = new CapturingAccountCodeSender();
        var sessionService = CreateSessionService(db, time, generator);
        var accountService = CreateAccountService(
            db,
            time,
            generator,
            sender,
            sessionService);
        var user = CreateUser("reset@example.com", "initial-password");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await sessionService.CreateAsync(user, null, default);

        await accountService.RequestPasswordResetAsync("RESET@EXAMPLE.COM", default);

        Assert.Equal(("reset@example.com", "123456"), Assert.Single(sender.PasswordResets));
        var firstChallenge = await db.AccountVerificationCodes.SingleAsync();
        Assert.NotEqual("123456", firstChallenge.CodeHash);
        Assert.Equal(firstChallenge.CreatedAt.AddMinutes(10), firstChallenge.ExpiresAt);

        // Repeated requests during the cooldown keep the existing challenge
        // active and do not send another email or reset its attempt counter.
        await accountService.RequestPasswordResetAsync("reset@example.com", default);
        Assert.Single(sender.PasswordResets);
        Assert.Single(await db.AccountVerificationCodes.ToListAsync());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(
                PasswordResetStatus.INVALID_CODE,
                await accountService.ResetPasswordAsync(
                    "reset@example.com",
                    "000000",
                    "replacement-password",
                    default));
        }

        Assert.Equal(5, firstChallenge.FailedAttempts);
        Assert.NotNull(firstChallenge.UsedAt);
        Assert.Equal(
            PasswordResetStatus.INVALID_CODE,
            await accountService.ResetPasswordAsync(
                "reset@example.com",
                "123456",
                "replacement-password",
                default));

        await accountService.RequestPasswordResetAsync("reset@example.com", default);
        Assert.Equal(
            PasswordResetStatus.SUCCESS,
            await accountService.ResetPasswordAsync(
                "reset@example.com",
                "654321",
                "replacement-password",
                default));

        var passwordHasher = new PasswordHasher<User>();
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                "replacement-password"));
        Assert.All(await db.RefreshTokens.ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(
            PasswordResetStatus.INVALID_CODE,
            await accountService.ResetPasswordAsync(
                "reset@example.com",
                "654321",
                "another-password",
                default));

        // The same outward operation for an unknown address sends nothing and
        // does not disclose account existence.
        await accountService.RequestPasswordResetAsync("missing@example.com", default);
        Assert.Equal(2, sender.PasswordResets.Count);
    }

    [Fact]
    public async Task Expired_password_reset_code_is_rejected()
    {
        await using var db = TestSupport.CreateDb();
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var generator = new DeterministicSecurityTokenGenerator("123456");
        var sessionService = CreateSessionService(db, time, generator);
        var accountService = CreateAccountService(
            db,
            time,
            generator,
            new CapturingAccountCodeSender(),
            sessionService);
        var user = CreateUser("expired@example.com", "initial-password");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await accountService.RequestPasswordResetAsync(user.Email, default);
        time.Advance(TimeSpan.FromMinutes(10));

        var status = await accountService.ResetPasswordAsync(
            user.Email,
            "123456",
            "replacement-password",
            default);

        Assert.Equal(PasswordResetStatus.INVALID_CODE, status);
        Assert.NotNull((await db.AccountVerificationCodes.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task Account_security_flows_update_profile_password_email_and_delete()
    {
        await using var db = TestSupport.CreateDb();
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));
        var generator = new DeterministicSecurityTokenGenerator("234567");
        var sender = new CapturingAccountCodeSender();
        var sessionService = CreateSessionService(db, time, generator);
        var accountService = CreateAccountService(
            db,
            time,
            generator,
            sender,
            sessionService);
        var user = CreateUser("account@example.com", "initial-password");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await sessionService.CreateAsync(user, null, default);

        var profile = await accountService.UpdateProfileAsync(
            user.Id,
            "  Updated Name  ",
            default);
        Assert.Equal("Updated Name", profile!.Name);
        Assert.Equal(
            ChangePasswordStatus.INVALID_CURRENT_PASSWORD,
            await accountService.ChangePasswordAsync(
                user.Id,
                "wrong-password",
                "replacement-password",
                default));
        Assert.Equal(
            ChangePasswordStatus.PASSWORD_REUSED,
            await accountService.ChangePasswordAsync(
                user.Id,
                "initial-password",
                "initial-password",
                default));
        Assert.Equal(
            ChangePasswordStatus.SUCCESS,
            await accountService.ChangePasswordAsync(
                user.Id,
                "initial-password",
                "replacement-password",
                default));

        Assert.Equal(
            EmailChangeRequestStatus.SUCCESS,
            await accountService.RequestEmailChangeAsync(
                user.Id,
                "NEW@EXAMPLE.COM",
                "replacement-password",
                default));
        Assert.Equal(("new@example.com", "234567"), Assert.Single(sender.EmailChanges));
        Assert.Equal(
            EmailChangeConfirmStatus.INVALID_CODE,
            (await accountService.ConfirmEmailChangeAsync(
                user.Id,
                "000000",
                default)).Status);

        var confirmed = await accountService.ConfirmEmailChangeAsync(
            user.Id,
            "234567",
            default);
        Assert.Equal(EmailChangeConfirmStatus.SUCCESS, confirmed.Status);
        Assert.Equal("new@example.com", confirmed.Profile!.Email);
        Assert.Equal(2, user.TokenVersion);

        Assert.Equal(
            DeleteAccountStatus.INVALID_PASSWORD,
            await accountService.DeleteAccountAsync(user.Id, "wrong-password", default));
        Assert.Equal(
            DeleteAccountStatus.SUCCESS,
            await accountService.DeleteAccountAsync(
                user.Id,
                "replacement-password",
                default));
        Assert.False(await db.Users.AnyAsync());
    }

    private static User CreateUser(string email, string password)
    {
        var user = new User
        {
            Name = "Test User",
            Email = email,
            PasswordHash = string.Empty
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        return user;
    }

    private static AuthSessionService CreateSessionService(
        AppDbContext db,
        TimeProvider timeProvider,
        ISecurityTokenGenerator generator)
    {
        var configuration = CreateConfiguration();
        return new AuthSessionService(
            db,
            new JwtTokenService(configuration),
            generator,
            new HmacAuthSecretHasher(configuration),
            configuration,
            timeProvider);
    }

    private static AccountSecurityService CreateAccountService(
        AppDbContext db,
        TimeProvider timeProvider,
        ISecurityTokenGenerator generator,
        IAccountCodeSender sender,
        IAuthSessionService sessionService)
    {
        var configuration = CreateConfiguration();
        return new AccountSecurityService(
            db,
            new PasswordHasher<User>(),
            generator,
            new HmacAuthSecretHasher(configuration),
            sender,
            sessionService,
            timeProvider,
            NullLogger<AccountSecurityService>.Instance);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-with-at-least-thirty-two-bytes",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "tests",
                ["Jwt:ExpiresMinutes"] = "15",
                ["AuthSecurity:RefreshTokenDays"] = "30"
            })
            .Build();

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class DeterministicSecurityTokenGenerator(params string[] codes)
        : ISecurityTokenGenerator
    {
        private readonly Queue<string> _codes = new(codes);
        private int _refreshSequence;

        public string CreateRefreshToken() =>
            $"test-refresh-token-{++_refreshSequence:D4}-{new string('x', 64)}";

        public string CreateSixDigitCode() =>
            _codes.Count > 0 ? _codes.Dequeue() : "999999";
    }

    private sealed class CapturingAccountCodeSender : IAccountCodeSender
    {
        public List<(string Email, string Code)> PasswordResets { get; } = [];
        public List<(string Email, string Code)> EmailChanges { get; } = [];

        public Task SendPasswordResetCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken)
        {
            PasswordResets.Add((email, code));
            return Task.CompletedTask;
        }

        public Task SendEmailChangeCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken)
        {
            EmailChanges.Add((email, code));
            return Task.CompletedTask;
        }
    }
}
