using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseManager.Api.Tests;

public sealed class SecurityInfrastructureTests
{
    [Fact]
    public void Hmac_hasher_is_deterministic_scoped_and_rejects_invalid_hashes()
    {
        var hasher = new HmacAuthSecretHasher(Configuration());

        var hash = hasher.Hash("refresh-token", "secret-value");

        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.Equal(hash, hasher.Hash("refresh-token", "secret-value"));
        Assert.True(hasher.Verify("refresh-token", "secret-value", hash));
        Assert.False(hasher.Verify("another-scope", "secret-value", hash));
        Assert.False(hasher.Verify("refresh-token", "another-secret", hash));
        Assert.False(hasher.Verify("refresh-token", "secret-value", "not-hex"));
        Assert.False(hasher.Verify("refresh-token", "secret-value", "00"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    public void Hmac_hasher_requires_a_strong_configured_key(string? secret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthSecurity:HashKey"] = secret
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => new HmacAuthSecretHasher(configuration));
    }

    [Fact]
    public void Security_token_generator_returns_contract_safe_values()
    {
        var generator = new SecurityTokenGenerator();
        var refreshTokens = Enumerable.Range(0, 32)
            .Select(_ => generator.CreateRefreshToken())
            .ToArray();

        Assert.Equal(refreshTokens.Length, refreshTokens.Distinct().Count());
        Assert.All(refreshTokens, token =>
        {
            Assert.Equal(86, token.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
            Assert.DoesNotContain("=", token);
        });
        Assert.All(Enumerable.Range(0, 32), _ =>
            Assert.Matches("^\\d{6}$", generator.CreateSixDigitCode()));
    }

    [Fact]
    public void Jwt_service_emits_identity_security_and_expiry_claims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Security Tester",
            Email = "security@example.com",
            PasswordHash = "hash",
            TokenVersion = 7
        };
        var service = new JwtTokenService(Configuration());
        var earliestExpiry = DateTime.UtcNow.AddMinutes(14);

        var encoded = service.Create(user);
        var latestExpiry = DateTime.UtcNow.AddMinutes(16);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);

        Assert.Equal("tests", token.Issuer);
        Assert.Contains("expense-manager-tests", token.Audiences);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Contains(token.Claims, claim => claim.Value == user.Id.ToString());
        Assert.Contains(token.Claims, claim => claim.Value == user.Name);
        Assert.Contains(token.Claims, claim => claim.Value == user.Email);
        Assert.Equal(
            "7",
            token.Claims.Single(claim => claim.Type == JwtClaimNames.TokenVersion).Value);
        Assert.InRange(token.ValidTo, earliestExpiry, latestExpiry);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short-secret")]
    public void Jwt_service_rejects_missing_or_short_secrets(string? secret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret
            })
            .Build();
        var user = new User
        {
            Name = "Security Tester",
            Email = "security@example.com",
            PasswordHash = "hash"
        };

        Assert.Throws<InvalidOperationException>(
            () => new JwtTokenService(configuration).Create(user));
    }

    [Fact]
    public void Http_user_context_reads_the_authenticated_user_identifier()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "test"))
        };
        var userContext = new HttpUserContext(new HttpContextAccessor
        {
            HttpContext = httpContext
        });

        Assert.Equal(userId, userContext.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Http_user_context_rejects_missing_or_invalid_identifiers(string? value)
    {
        var claims = value is null
            ? Array.Empty<Claim>()
            : [new Claim(ClaimTypes.NameIdentifier, value)];
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var userContext = new HttpUserContext(new HttpContextAccessor
        {
            HttpContext = httpContext
        });

        Assert.Throws<UnauthorizedAccessException>(() => userContext.UserId);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-with-at-least-thirty-two-bytes",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "expense-manager-tests",
                ["Jwt:ExpiresMinutes"] = "15"
            })
            .Build();
}
