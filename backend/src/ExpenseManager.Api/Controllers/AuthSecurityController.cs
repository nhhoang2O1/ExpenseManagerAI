using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthSecurityController(
    IAuthSessionService sessionService,
    IAccountSecurityService accountSecurityService,
    IUserContext userContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.RotateAsync(
            request.RefreshToken,
            ClientIp(),
            cancellationToken);
        return result.Status == RefreshSessionStatus.SUCCESS
            ? Ok(result.Session)
            : Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn." });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [EnableRateLimiting(AuthRateLimitPolicies.General)]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        // Deliberately idempotent so this endpoint cannot be used to probe
        // whether a refresh token is valid.
        await sessionService.RevokeAsync(
            request.RefreshToken,
            ClientIp(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await sessionService.RevokeAllAsync(
            userContext.UserId,
            "Logout from all devices",
            ClientIp(),
            cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetRequest)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountSecurityService.RequestPasswordResetAsync(
            request.Email,
            cancellationToken);
        return Accepted(new
        {
            message = "Nếu email tồn tại, mã đặt lại mật khẩu sẽ được gửi."
        });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetVerify)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var status = await accountSecurityService.ResetPasswordAsync(
            request.Email,
            request.Code,
            request.NewPassword,
            cancellationToken);
        return status switch
        {
            PasswordResetStatus.SUCCESS => NoContent(),
            PasswordResetStatus.PASSWORD_REUSED => BadRequest(new
            {
                message = "Mật khẩu mới phải khác mật khẩu hiện tại."
            }),
            _ => BadRequest(new
            {
                message = "Mã xác nhận không hợp lệ hoặc đã hết hạn."
            })
        };
    }

    private string? ClientIp() => HttpContext?.Connection.RemoteIpAddress?.ToString();
}
