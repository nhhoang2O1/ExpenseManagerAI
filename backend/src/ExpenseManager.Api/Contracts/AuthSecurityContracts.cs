using System.ComponentModel.DataAnnotations;

namespace ExpenseManager.Api.Contracts;

public sealed record AuthSessionResponse : AuthResponse
{
    public AuthSessionResponse(
        string accessToken,
        string refreshToken,
        int expiresIn,
        UserResponse user) : base(accessToken, user)
    {
        RefreshToken = refreshToken;
        ExpiresIn = expiresIn;
    }
}

public sealed record RefreshTokenRequest(
    [Required, StringLength(1024, MinimumLength = 20)] string RefreshToken);

public sealed record LogoutRequest(
    [Required, StringLength(1024, MinimumLength = 20)] string RefreshToken);

public sealed record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(320)] string Email);

public sealed record ResetPasswordRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, RegularExpression(@"^\d{6}$")] string Code,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public sealed record ProfileResponse(
    Guid Id,
    string Name,
    string Email,
    DateTime CreatedAt,
    int FinancialCycleStartDay = 1);

public sealed record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name);

public sealed record UpdateFinancialCycleRequest(
    [Range(1, 31)] int StartDay);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public sealed record EmailChangeRequest(
    [Required, EmailAddress, StringLength(320)] string NewEmail,
    [Required] string CurrentPassword);

public sealed record EmailChangeConfirmRequest(
    [Required, RegularExpression(@"^\d{6}$")] string Code);

public sealed record DeleteAccountRequest(
    [Required] string Password);
