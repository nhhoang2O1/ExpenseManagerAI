namespace ExpenseManager.Api.Infrastructure;

public static class AuthRateLimitPolicies
{
    public const string General = "auth-general";
    public const string PasswordResetRequest = "password-reset-request";
    public const string PasswordResetVerify = "password-reset-verify";
}
