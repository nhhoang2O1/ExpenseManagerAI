using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public enum PasswordResetStatus
{
    SUCCESS,
    INVALID_CODE,
    PASSWORD_REUSED
}

public enum ChangePasswordStatus
{
    SUCCESS,
    USER_NOT_FOUND,
    INVALID_CURRENT_PASSWORD,
    PASSWORD_REUSED
}

public enum EmailChangeRequestStatus
{
    SUCCESS,
    USER_NOT_FOUND,
    INVALID_CURRENT_PASSWORD,
    EMAIL_UNCHANGED,
    EMAIL_TAKEN,
    DELIVERY_FAILED
}

public enum EmailChangeConfirmStatus
{
    SUCCESS,
    USER_NOT_FOUND,
    INVALID_CODE,
    EMAIL_TAKEN
}

public sealed record EmailChangeConfirmResult(
    EmailChangeConfirmStatus Status,
    ProfileResponse? Profile = null);

public enum DeleteAccountStatus
{
    SUCCESS,
    USER_NOT_FOUND,
    INVALID_PASSWORD
}

public interface IAccountSecurityService
{
    Task<ProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileResponse?> UpdateProfileAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<PasswordResetStatus> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken);
    Task<ChangePasswordStatus> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
    Task<EmailChangeRequestStatus> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string currentPassword,
        CancellationToken cancellationToken);
    Task<EmailChangeConfirmResult> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);
    Task<DeleteAccountStatus> DeleteAccountAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken);
}

public sealed class AccountSecurityService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    ISecurityTokenGenerator tokenGenerator,
    IAuthSecretHasher secretHasher,
    IAccountCodeSender codeSender,
    IAuthSessionService sessionService,
    TimeProvider timeProvider,
    ILogger<AccountSecurityService> logger) : IAccountSecurityService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PasswordResetRequestCooldown = TimeSpan.FromMinutes(1);
    private const int MaximumFailedAttempts = 5;

    public async Task<ProfileResponse?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new ProfileResponse(x.Id, x.Name, x.Email, x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProfileResponse?> UpdateProfileAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
        if (user is null)
            return null;

        user.Name = name.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return ToProfile(user);
    }

    public async Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == normalizedEmail,
            cancellationToken);
        if (user is null)
        {
            // Keep cryptographic work on the unknown-account path and never
            // reveal whether the address is registered.
            var decoy = tokenGenerator.CreateSixDigitCode();
            _ = secretHasher.Hash(CodeScope(Guid.Empty, AccountCodePurpose.PASSWORD_RESET), decoy);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var recentOutstandingChallenge = await db.Set<AccountVerificationCode>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == user.Id &&
                x.Purpose == AccountCodePurpose.PASSWORD_RESET &&
                x.UsedAt == null &&
                x.ExpiresAt > now &&
                x.CreatedAt > now - PasswordResetRequestCooldown,
                cancellationToken);
        if (recentOutstandingChallenge)
            return;

        await InvalidateOutstandingCodesAsync(
            user.Id,
            AccountCodePurpose.PASSWORD_RESET,
            now,
            cancellationToken);
        var code = tokenGenerator.CreateSixDigitCode();
        var challenge = new AccountVerificationCode
        {
            UserId = user.Id,
            Purpose = AccountCodePurpose.PASSWORD_RESET,
            CodeHash = secretHasher.Hash(
                CodeScope(user.Id, AccountCodePurpose.PASSWORD_RESET),
                code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime)
        };
        db.Set<AccountVerificationCode>().Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await codeSender.SendPasswordResetCodeAsync(
                normalizedEmail,
                code,
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            challenge.UsedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(
                exception,
                "Password reset code delivery failed for user {UserId}.",
                user.Id);
        }
    }

    public async Task<PasswordResetStatus> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == normalizedEmail,
            cancellationToken);
        if (user is null)
        {
            _ = secretHasher.Hash(
                CodeScope(Guid.Empty, AccountCodePurpose.PASSWORD_RESET),
                code);
            return PasswordResetStatus.INVALID_CODE;
        }

        var challenge = await ValidateCodeAsync(
            user.Id,
            AccountCodePurpose.PASSWORD_RESET,
            code,
            cancellationToken);
        if (challenge is null)
            return PasswordResetStatus.INVALID_CODE;

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword)
            != PasswordVerificationResult.Failed)
            return PasswordResetStatus.PASSWORD_REUSED;

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        challenge.UsedAt = timeProvider.GetUtcNow().UtcDateTime;
        challenge.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await sessionService.RevokeAllAsync(
                user.Id,
                "Password reset",
                null,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return PasswordResetStatus.INVALID_CODE;
        }
        return PasswordResetStatus.SUCCESS;
    }

    public async Task<ChangePasswordStatus> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
        if (user is null)
            return ChangePasswordStatus.USER_NOT_FOUND;

        if (passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                currentPassword) == PasswordVerificationResult.Failed)
            return ChangePasswordStatus.INVALID_CURRENT_PASSWORD;

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword)
            != PasswordVerificationResult.Failed)
            return ChangePasswordStatus.PASSWORD_REUSED;

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        await sessionService.RevokeAllAsync(
            user.Id,
            "Password changed",
            null,
            cancellationToken);
        return ChangePasswordStatus.SUCCESS;
    }

    public async Task<EmailChangeRequestStatus> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
        if (user is null)
            return EmailChangeRequestStatus.USER_NOT_FOUND;

        if (passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                currentPassword) == PasswordVerificationResult.Failed)
            return EmailChangeRequestStatus.INVALID_CURRENT_PASSWORD;

        var normalizedEmail = NormalizeEmail(newEmail);
        if (normalizedEmail == user.Email)
            return EmailChangeRequestStatus.EMAIL_UNCHANGED;
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
            return EmailChangeRequestStatus.EMAIL_TAKEN;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await InvalidateOutstandingCodesAsync(
            user.Id,
            AccountCodePurpose.EMAIL_CHANGE,
            now,
            cancellationToken);
        var code = tokenGenerator.CreateSixDigitCode();
        var challenge = new AccountVerificationCode
        {
            UserId = user.Id,
            Purpose = AccountCodePurpose.EMAIL_CHANGE,
            PendingEmail = normalizedEmail,
            CodeHash = secretHasher.Hash(
                CodeScope(user.Id, AccountCodePurpose.EMAIL_CHANGE),
                code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime)
        };
        db.Set<AccountVerificationCode>().Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await codeSender.SendEmailChangeCodeAsync(
                normalizedEmail,
                code,
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            challenge.UsedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(
                exception,
                "Email change code delivery failed for user {UserId}.",
                user.Id);
            return EmailChangeRequestStatus.DELIVERY_FAILED;
        }

        return EmailChangeRequestStatus.SUCCESS;
    }

    public async Task<EmailChangeConfirmResult> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
        if (user is null)
            return new EmailChangeConfirmResult(EmailChangeConfirmStatus.USER_NOT_FOUND);

        var challenge = await ValidateCodeAsync(
            user.Id,
            AccountCodePurpose.EMAIL_CHANGE,
            code,
            cancellationToken);
        if (challenge?.PendingEmail is null)
            return new EmailChangeConfirmResult(EmailChangeConfirmStatus.INVALID_CODE);

        if (await db.Users.AnyAsync(
                x => x.Id != user.Id && x.Email == challenge.PendingEmail,
                cancellationToken))
        {
            challenge.UsedAt = timeProvider.GetUtcNow().UtcDateTime;
            challenge.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            return new EmailChangeConfirmResult(EmailChangeConfirmStatus.EMAIL_TAKEN);
        }

        user.Email = challenge.PendingEmail;
        challenge.UsedAt = timeProvider.GetUtcNow().UtcDateTime;
        challenge.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await sessionService.RevokeAllAsync(
                user.Id,
                "Email changed",
                null,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new EmailChangeConfirmResult(EmailChangeConfirmStatus.INVALID_CODE);
        }
        catch (DbUpdateException)
        {
            return new EmailChangeConfirmResult(EmailChangeConfirmStatus.EMAIL_TAKEN);
        }

        return new EmailChangeConfirmResult(
            EmailChangeConfirmStatus.SUCCESS,
            ToProfile(user));
    }

    public async Task<DeleteAccountStatus> DeleteAccountAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
        if (user is null)
            return DeleteAccountStatus.USER_NOT_FOUND;
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
            == PasswordVerificationResult.Failed)
            return DeleteAccountStatus.INVALID_PASSWORD;

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return DeleteAccountStatus.SUCCESS;
    }

    private async Task<AccountVerificationCode?> ValidateCodeAsync(
        Guid userId,
        AccountCodePurpose purpose,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await db.Set<AccountVerificationCode>()
            .Where(x =>
                x.UserId == userId &&
                x.Purpose == purpose &&
                x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null)
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (challenge.ExpiresAt <= now ||
            challenge.FailedAttempts >= MaximumFailedAttempts)
        {
            challenge.UsedAt = now;
            challenge.ConcurrencyStamp = Guid.NewGuid();
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request consumed or invalidated the same challenge.
            }
            return null;
        }

        if (!secretHasher.Verify(CodeScope(userId, purpose), code, challenge.CodeHash))
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= MaximumFailedAttempts)
                challenge.UsedAt = now;
            challenge.ConcurrencyStamp = Guid.NewGuid();
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A parallel attempt already advanced the challenge state.
            }
            return null;
        }

        return challenge;
    }

    private async Task InvalidateOutstandingCodesAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTime usedAt,
        CancellationToken cancellationToken)
    {
        var existing = await db.Set<AccountVerificationCode>()
            .Where(x =>
                x.UserId == userId &&
                x.Purpose == purpose &&
                x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var challenge in existing)
        {
            challenge.UsedAt = usedAt;
            challenge.ConcurrencyStamp = Guid.NewGuid();
        }
    }

    private static string CodeScope(Guid userId, AccountCodePurpose purpose) =>
        $"account-code:{purpose}:{userId:N}";

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static ProfileResponse ToProfile(User user) =>
        new(user.Id, user.Name, user.Email, user.CreatedAt);
}
