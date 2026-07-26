using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(
    IAccountSecurityService accountSecurityService,
    IUserContext userContext) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> GetProfile(
        CancellationToken cancellationToken)
    {
        var profile = await accountSecurityService.GetProfileAsync(
            userContext.UserId,
            cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Name.Trim().Length < 2)
            return BadRequest(new { message = "Tên phải có ít nhất 2 ký tự." });

        var profile = await accountSecurityService.UpdateProfileAsync(
            userContext.UserId,
            request.Name,
            cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var status = await accountSecurityService.ChangePasswordAsync(
            userContext.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        return status switch
        {
            ChangePasswordStatus.SUCCESS => NoContent(),
            ChangePasswordStatus.USER_NOT_FOUND => NotFound(),
            ChangePasswordStatus.PASSWORD_REUSED => BadRequest(new
            {
                message = "Mật khẩu mới phải khác mật khẩu hiện tại."
            }),
            _ => BadRequest(new { message = "Mật khẩu hiện tại không đúng." })
        };
    }

    [HttpPost("email-change/request")]
    public async Task<IActionResult> RequestEmailChange(
        EmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var status = await accountSecurityService.RequestEmailChangeAsync(
            userContext.UserId,
            request.NewEmail,
            request.CurrentPassword,
            cancellationToken);
        return status switch
        {
            EmailChangeRequestStatus.SUCCESS => Accepted(new
            {
                message = "Mã xác nhận đã được gửi đến email mới."
            }),
            EmailChangeRequestStatus.USER_NOT_FOUND => NotFound(),
            EmailChangeRequestStatus.INVALID_CURRENT_PASSWORD => BadRequest(new
            {
                message = "Mật khẩu hiện tại không đúng."
            }),
            EmailChangeRequestStatus.EMAIL_UNCHANGED => BadRequest(new
            {
                message = "Email mới phải khác email hiện tại."
            }),
            EmailChangeRequestStatus.EMAIL_TAKEN => Conflict(new
            {
                message = "Email đã được sử dụng."
            }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Không thể gửi mã xác nhận lúc này."
            })
        };
    }

    [HttpPost("email-change/confirm")]
    public async Task<ActionResult<ProfileResponse>> ConfirmEmailChange(
        EmailChangeConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountSecurityService.ConfirmEmailChangeAsync(
            userContext.UserId,
            request.Code,
            cancellationToken);
        return result.Status switch
        {
            EmailChangeConfirmStatus.SUCCESS => Ok(result.Profile),
            EmailChangeConfirmStatus.USER_NOT_FOUND => NotFound(),
            EmailChangeConfirmStatus.EMAIL_TAKEN => Conflict(new
            {
                message = "Email đã được sử dụng."
            }),
            _ => BadRequest(new
            {
                message = "Mã xác nhận không hợp lệ hoặc đã hết hạn."
            })
        };
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(
        DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        var status = await accountSecurityService.DeleteAccountAsync(
            userContext.UserId,
            request.Password,
            cancellationToken);
        return status switch
        {
            DeleteAccountStatus.SUCCESS => NoContent(),
            DeleteAccountStatus.USER_NOT_FOUND => NotFound(),
            _ => BadRequest(new { message = "Mật khẩu không đúng." })
        };
    }
}
