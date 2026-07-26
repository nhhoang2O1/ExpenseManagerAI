namespace ExpenseManager.Api.Domain;

public enum AccountCodePurpose
{
    PASSWORD_RESET,
    EMAIL_CHANGE
}

/// <summary>
/// A long-lived session credential. Only a keyed hash of the opaque token is
/// persisted, so a database read does not expose usable refresh tokens.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}

/// <summary>
/// Short-lived, single-use verification challenge for password reset and
/// email-address changes. The plaintext six-digit code is never stored.
/// </summary>
public sealed class AccountVerificationCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AccountCodePurpose Purpose { get; set; }
    public required string CodeHash { get; set; }
    public string? PendingEmail { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public User User { get; set; } = null!;
}
