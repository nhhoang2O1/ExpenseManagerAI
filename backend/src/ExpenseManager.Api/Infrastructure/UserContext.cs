using System.Security.Claims;

namespace ExpenseManager.Api.Infrastructure;

public interface IUserContext
{
    Guid UserId { get; }
}

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("Missing user identifier.");
        }
    }
}
