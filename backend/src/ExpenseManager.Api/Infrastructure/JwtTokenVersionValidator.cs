using System.Security.Claims;
using ExpenseManager.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Infrastructure;

public interface IJwtTokenVersionValidator
{
    Task ValidateAsync(TokenValidatedContext context);
}

/// <summary>
/// Rejects an otherwise valid JWT after a security-sensitive account event
/// increments the user's token version.
/// </summary>
public sealed class JwtTokenVersionValidator(AppDbContext db)
    : IJwtTokenVersionValidator
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenVersionValue = context.Principal?.FindFirstValue(JwtClaimNames.TokenVersion);
        if (!Guid.TryParse(userIdValue, out var userId) ||
            !int.TryParse(tokenVersionValue, out var tokenVersion))
        {
            context.Fail("Access token is missing its security version.");
            return;
        }

        var current = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.TokenVersion == tokenVersion);
        if (!current)
            context.Fail("Access token has been revoked.");
    }
}
