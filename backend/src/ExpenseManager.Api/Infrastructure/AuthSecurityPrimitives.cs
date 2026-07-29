using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace ExpenseManager.Api.Infrastructure;

public interface ISecurityTokenGenerator
{
    string CreateRefreshToken();
    string CreateSixDigitCode();
}

public sealed class SecurityTokenGenerator : ISecurityTokenGenerator
{
    public string CreateRefreshToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    public string CreateSixDigitCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

public interface IAuthSecretHasher
{
    string Hash(string scope, string secret);
    bool Verify(string scope, string secret, string expectedHash);
}

public sealed class HmacAuthSecretHasher : IAuthSecretHasher
{
    private readonly byte[] _key;

    public HmacAuthSecretHasher(IConfiguration configuration)
    {
        var configuredKey = configuration["AuthSecurity:HashKey"]
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "AuthSecurity__HashKey or Jwt__Secret is required.");
        _key = Encoding.UTF8.GetBytes(configuredKey);
        if (_key.Length < 32)
            throw new InvalidOperationException(
                "AuthSecurity__HashKey must contain at least 32 bytes.");
    }

    public string Hash(string scope, string secret)
    {
        var payload = Encoding.UTF8.GetBytes($"{scope}\0{secret}");
        return Convert.ToHexString(HMACSHA256.HashData(_key, payload));
    }

    public bool Verify(string scope, string secret, string expectedHash)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Convert.FromHexString(Hash(scope, secret));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public interface IAccountCodeSender
{
    Task SendRegistrationCodeAsync(string email, string code, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken);

    Task SendEmailChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken);
}

/// <summary>
/// SMTP delivery for account verification codes. In Development only, a
/// missing SMTP host falls back to a warning log so local end-to-end testing
/// remains possible. Production fails closed when SMTP is not configured.
/// </summary>
public sealed class SmtpAccountCodeSender(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<SmtpAccountCodeSender> logger) : IAccountCodeSender
{
    public Task SendRegistrationCodeAsync(string email, string code, CancellationToken cancellationToken) =>
        SendAsync(email, "Xac thuc tai khoan", $"Ma xac thuc tai khoan Expense Manager cua ban la {code}. Ma co hieu luc trong 10 phut.", code, cancellationToken);

    public Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Mã đặt lại mật khẩu",
            $"Mã đặt lại mật khẩu của bạn là {code}. Mã có hiệu lực trong 10 phút.",
            code,
            cancellationToken);

    public Task SendEmailChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Mã xác nhận email mới",
            $"Mã xác nhận email mới của bạn là {code}. Mã có hiệu lực trong 10 phút.",
            code,
            cancellationToken);

    private async Task SendAsync(
        string recipient,
        string subject,
        string body,
        string code,
        CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Email__Smtp__Host is required outside Development.");

            logger.LogWarning(
                "Development account verification code for {Recipient}: {Code}",
                recipient,
                code);
            return;
        }

        var fromAddress = configuration["Email:Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Email__Smtp__FromAddress is required.");

        using var message = new MailMessage
        {
            From = new MailAddress(
                fromAddress,
                configuration["Email:Smtp:FromName"] ?? "Expense Manager"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));

        using var client = new SmtpClient(
            host,
            configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true)
        };
        var username = configuration["Email:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(
                username,
                configuration["Email:Smtp:Password"]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
