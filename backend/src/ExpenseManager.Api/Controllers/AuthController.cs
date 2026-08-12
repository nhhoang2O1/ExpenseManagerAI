using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthApplicationService service) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<ActionResult<RegistrationAcceptedResponse>> Register(
        RegisterRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.RegisterAsync(request, cancellationToken));

    [HttpPost("confirm-registration")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetVerify)]
    public async Task<ActionResult<AuthSessionResponse>> ConfirmRegistration(
        RegistrationConfirmationRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.ConfirmRegistrationAsync(
            request, ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(), cancellationToken));

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<ActionResult<AuthSessionResponse>> Login(
        LoginRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.LoginAsync(
            request, ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(), cancellationToken));
}
