using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public enum RefreshSessionStatus
{
    SUCCESS,
    INVALID,
    EXPIRED,
    REUSED
}

public sealed record RefreshSessionResult(
    RefreshSessionStatus Status,
    AuthSessionResponse? Session = null);

public interface IAuthSessionService
{
    Task<AuthSessionResponse> CreateAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<RefreshSessionResult> RotateAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<int> RevokeAllAsync(
        Guid userId,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public sealed class AuthSessionService(
    AppDbContext db,
    IJwtTokenService jwtTokenService,
    ISecurityTokenGenerator tokenGenerator,
    IAuthSecretHasher secretHasher,
    IConfiguration configuration,
    TimeProvider timeProvider) : IAuthSessionService
{
    private const string RefreshTokenHashScope = "refresh-token";

    private readonly TimeSpan _refreshLifetime = TimeSpan.FromDays(
        configuration.GetValue("AuthSecurity:RefreshTokenDays", 30));

    private readonly int _accessTokenLifetimeSeconds = checked(
        configuration.GetValue("Jwt:ExpiresMinutes", 15) * 60);

    public async Task<AuthSessionResponse> CreateAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (entity, rawToken) = CreateRefreshToken(user.Id, now, ipAddress);
        db.Set<RefreshToken>().Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return BuildResponse(user, rawToken);
    }

    public async Task<RefreshSessionResult> RotateAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var hash = secretHasher.Hash(RefreshTokenHashScope, rawRefreshToken);
        var existing = await db.Set<RefreshToken>()
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (existing is null)
            return new RefreshSessionResult(RefreshSessionStatus.INVALID);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existing.RevokedAt is not null)
        {
            if (existing.ReplacedByTokenId is not null)
            {
                if (string.Equals(
                        existing.RevokedReason,
                        "Reuse detected",
                        StringComparison.Ordinal))
                    return new RefreshSessionResult(RefreshSessionStatus.REUSED);

                existing.RevokedReason = "Reuse detected";
                existing.ConcurrencyStamp = Guid.NewGuid();
                try
                {
                    await RevokeAllAsync(
                        existing.UserId,
                        "Refresh token reuse detected",
                        ipAddress,
                        cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // A parallel replay detector already advanced the same
                    // revocation state; the caller still receives no session.
                    db.ChangeTracker.Clear();
                }
                return new RefreshSessionResult(RefreshSessionStatus.REUSED);
            }

            return new RefreshSessionResult(RefreshSessionStatus.INVALID);
        }

        if (existing.ExpiresAt <= now)
        {
            Revoke(existing, now, "Expired", ipAddress);
            await db.SaveChangesAsync(cancellationToken);
            return new RefreshSessionResult(RefreshSessionStatus.EXPIRED);
        }

        var (replacement, rawReplacement) =
            CreateRefreshToken(existing.UserId, now, ipAddress);
        Revoke(existing, now, "Rotated", ipAddress);
        existing.ReplacedByTokenId = replacement.Id;
        db.Set<RefreshToken>().Add(replacement);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A parallel request already rotated this credential. Treat the
            // second use as a replay and invalidate every remaining session.
            var userId = existing.UserId;
            db.ChangeTracker.Clear();
            await RevokeAllAsync(
                userId,
                "Concurrent refresh token reuse detected",
                ipAddress,
                cancellationToken);
            return new RefreshSessionResult(RefreshSessionStatus.REUSED);
        }

        return new RefreshSessionResult(
            RefreshSessionStatus.SUCCESS,
            BuildResponse(existing.User, rawReplacement));
    }

    public async Task RevokeAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var hash = secretHasher.Hash(RefreshTokenHashScope, rawRefreshToken);
        var token = await db.Set<RefreshToken>()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (token is null || token.RevokedAt is not null)
            return;

        Revoke(
            token,
            timeProvider.GetUtcNow().UtcDateTime,
            "Logout",
            ipAddress);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Logout is intentionally idempotent; another request already
            // revoked the same credential.
            db.ChangeTracker.Clear();
        }
    }

    public async Task<int> RevokeAllAsync(
        Guid userId,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var tokens = await db.Set<RefreshToken>()
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var user = db.Users.Local.FirstOrDefault(x => x.Id == userId)
            ?? await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is not null)
            user.TokenVersion = checked(user.TokenVersion + 1);

        if (tokens.Count > 0)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var token in tokens)
                Revoke(token, now, reason, ipAddress);
        }

        // This service can be composed with an account mutation on the same
        // scoped DbContext. Persist that mutation even when no sessions exist.
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(
        Guid userId,
        DateTime now,
        string? ipAddress)
    {
        var rawToken = tokenGenerator.CreateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = secretHasher.Hash(RefreshTokenHashScope, rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(_refreshLifetime),
            CreatedByIp = NormalizeIp(ipAddress)
        };
        return (entity, rawToken);
    }

    private AuthSessionResponse BuildResponse(User user, string rawRefreshToken) =>
        new(
            jwtTokenService.Create(user),
            rawRefreshToken,
            _accessTokenLifetimeSeconds,
            new UserResponse(user.Id, user.Name, user.Email, user.FinancialCycleStartDay));

    private static void Revoke(
        RefreshToken token,
        DateTime now,
        string reason,
        string? ipAddress)
    {
        token.RevokedAt = now;
        token.RevokedReason = reason;
        token.RevokedByIp = NormalizeIp(ipAddress);
        token.ConcurrencyStamp = Guid.NewGuid();
    }

    private static string? NormalizeIp(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, 64)];
}
