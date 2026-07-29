using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    ISecurityTokenGenerator tokenGenerator,
    IAuthSecretHasher secretHasher,
    IAccountCodeSender codeSender,
    TimeProvider timeProvider,
    IAuthSessionService? sessionService,
    IJwtTokenService? legacyTokenService) : ControllerBase
{
    private static readonly (string Name, TransactionType Type, string Color, string Icon)[] DefaultCategories =
    [
        ("Ăn uống", TransactionType.EXPENSE, "#EF4444", "ic_food"),
        ("Di chuyển", TransactionType.EXPENSE, "#F59E0B", "ic_transport"),
        ("Mua sắm", TransactionType.EXPENSE, "#8B5CF6", "ic_shopping"),
        ("Nhà ở", TransactionType.EXPENSE, "#795548", "ic_house"),
        ("Giải trí", TransactionType.EXPENSE, "#9C27B0", "ic_entertainment"),
        ("Sức khỏe", TransactionType.EXPENSE, "#F44336", "ic_health"),
        ("Giáo dục", TransactionType.EXPENSE, "#3F51B5", "ic_education"),
        ("Hóa đơn", TransactionType.EXPENSE, "#3B82F6", "ic_bill"),
        ("Khác", TransactionType.EXPENSE, "#6B7280", "ic_other"),
        ("Lương", TransactionType.INCOME, "#10B981", "ic_salary"),
        ("Quà tặng", TransactionType.INCOME, "#E91E63", "ic_gift"),
        ("Đầu tư", TransactionType.INCOME, "#00BCD4", "ic_invest"),
        ("Làm thêm", TransactionType.INCOME, "#8BC34A", "ic_freelance"),
        ("Khác", TransactionType.INCOME, "#14B8A6", "ic_other")
    ];

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<ActionResult<AuthSessionResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (existing is not null && existing.IsEmailVerified)
            return Conflict(new { message = "Email đã được sử dụng." });

        if (existing is not null)
        {
            if (passwordHasher.VerifyHashedPassword(existing, existing.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
                return Conflict(new { message = "Email is already in use." });
            await SendRegistrationCodeAsync(existing, cancellationToken);
            return Accepted(new { email, message = "Verification code sent." });
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            IsEmailVerified = false
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        AddMissingDefaultCategories(user.Id, []);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Email đã được sử dụng." });
        }

        await SendRegistrationCodeAsync(user, cancellationToken);
        return Accepted(new { email, message = "Verification code sent." });
    }

    [HttpPost("confirm-registration")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetVerify)]
    public async Task<ActionResult<AuthSessionResponse>> ConfirmRegistration(
        RegistrationConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var challenge = user is null ? null : await db.AccountVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == AccountCodePurpose.REGISTRATION && x.UsedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (user is null || user.IsEmailVerified || challenge is null || challenge.FailedAttempts >= 5 ||
            !secretHasher.Verify(CodeScope(user.Id), request.Code, challenge.CodeHash))
        {
            if (challenge is not null)
            {
                challenge.FailedAttempts++;
                await db.SaveChangesAsync(cancellationToken);
            }
            return BadRequest(new { message = "Verification code is invalid or expired." });
        }

        challenge.UsedAt = now;
        user.IsEmailVerified = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await CreateSessionAsync(user, cancellationToken));
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<ActionResult<AuthSessionResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsEmailVerified ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });

        await EnsureDefaultCategoriesAsync(user.Id, cancellationToken);

        var session = await CreateSessionAsync(user, cancellationToken);
        return Ok(session);
    }

    private Task<AuthSessionResponse> CreateSessionAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (sessionService is not null)
            return sessionService.CreateAsync(user, ClientIp(), cancellationToken);

        if (legacyTokenService is null)
            throw new InvalidOperationException("Auth session service is not configured.");

        return Task.FromResult(new AuthSessionResponse(
            legacyTokenService.Create(user),
            string.Empty,
            900,
            new UserResponse(user.Id, user.Name, user.Email)));
    }

    private string? ClientIp() => HttpContext?.Connection.RemoteIpAddress?.ToString();

    private async Task SendRegistrationCodeAsync(User user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var outstanding = await db.AccountVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == AccountCodePurpose.REGISTRATION && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in outstanding) item.UsedAt = now;

        var code = tokenGenerator.CreateSixDigitCode();
        db.AccountVerificationCodes.Add(new AccountVerificationCode
        {
            UserId = user.Id,
            Purpose = AccountCodePurpose.REGISTRATION,
            CodeHash = secretHasher.Hash(CodeScope(user.Id), code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10)
        });
        await db.SaveChangesAsync(cancellationToken);
        await codeSender.SendRegistrationCodeAsync(user.Email, code, cancellationToken);
    }

    private static string CodeScope(Guid userId) => $"registration:{userId}";

    private async Task EnsureDefaultCategoriesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await NormalizeLegacyIncomeOtherAsync(userId, cancellationToken);

        var existing = await db.Categories.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Name, x.Type })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(x => CategoryKey(x.Name, x.Type))
            .ToHashSet(StringComparer.Ordinal);

        AddMissingDefaultCategories(userId, existingKeys);
        await SyncDefaultCategoryMetadataAsync(userId, cancellationToken);

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
    }

    private void AddMissingDefaultCategories(Guid userId, HashSet<string> existingKeys)
    {
        foreach (var item in DefaultCategories)
        {
            if (existingKeys.Contains(CategoryKey(item.Name, item.Type)))
                continue;

            db.Categories.Add(new Category
            {
                UserId = userId,
                Name = item.Name,
                Type = item.Type,
                Color = item.Color,
                Icon = item.Icon
            });
        }
    }

    private async Task NormalizeLegacyIncomeOtherAsync(Guid userId, CancellationToken cancellationToken)
    {
        var legacy = await db.Categories.SingleOrDefaultAsync(
            x => x.UserId == userId &&
                 x.Type == TransactionType.INCOME &&
                 x.Name == "Thu nhập khác",
            cancellationToken);
        if (legacy is null)
            return;

        var canonical = await db.Categories.SingleOrDefaultAsync(
            x => x.UserId == userId &&
                 x.Type == TransactionType.INCOME &&
                 x.Name == "Khác",
            cancellationToken);

        if (canonical is null)
        {
            legacy.Name = "Khác";
            legacy.Color = "#14B8A6";
            legacy.Icon = "account_balance_wallet";
            return;
        }

        var transactions = await db.Transactions
            .Where(x => x.UserId == userId && x.CategoryId == legacy.Id)
            .ToListAsync(cancellationToken);
        foreach (var transaction in transactions)
            transaction.CategoryId = canonical.Id;

        db.Categories.Remove(legacy);
    }

    private async Task SyncDefaultCategoryMetadataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var category in categories)
        {
            var match = DefaultCategories.FirstOrDefault(
                x => x.Name == category.Name && x.Type == category.Type);
            if (match.Name is null)
                continue;

            category.Color = match.Color;
            category.Icon = match.Icon;
        }
    }

    private static string CategoryKey(string name, TransactionType type) =>
        $"{type}:{name.Trim().ToUpperInvariant()}";
}
