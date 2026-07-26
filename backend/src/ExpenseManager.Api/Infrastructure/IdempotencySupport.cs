using System.Security.Cryptography;
using System.Text.Json;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Infrastructure;

internal sealed record IdempotencyRequestContext(
    string Scope,
    string Key,
    string RequestHash);

internal sealed record IdempotencyLookup<T>(
    bool Exists,
    bool RequestConflict,
    int StatusCode,
    T? Response);

/// <summary>
/// Small shared primitive for mutation endpoints. Missing keys remain accepted
/// during the compatibility window; once present, a key is bound to the exact
/// request payload and the original response is persisted with the mutation.
/// </summary>
internal static class IdempotencySupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryCreate<TRequest>(
        ControllerBase controller,
        string scope,
        TRequest request,
        out IdempotencyRequestContext? context,
        out ObjectResult? error)
    {
        context = null;
        error = null;
        var httpContext = controller.ControllerContext.HttpContext;
        if (httpContext is null)
            return true;

        var key = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        if (key.Length == 0)
            return true;
        if (key.Length > 200)
        {
            error = controller.BadRequest(new
            {
                message = "Idempotency-Key không được dài quá 200 ký tự."
            });
            return false;
        }

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(requestBytes));
        context = new IdempotencyRequestContext(scope, key, hash);
        return true;
    }

    public static async Task<IdempotencyLookup<T>?> FindAsync<T>(
        AppDbContext db,
        Guid userId,
        IdempotencyRequestContext? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return null;

        var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == userId && x.Scope == request.Scope && x.Key == request.Key,
            cancellationToken);
        if (record is null)
            return null;
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.RequestHash),
                Convert.FromHexString(request.RequestHash)))
            return new IdempotencyLookup<T>(true, true, StatusCodes.Status409Conflict, default);

        var response = JsonSerializer.Deserialize<T>(record.ResponseJson, JsonOptions);
        return new IdempotencyLookup<T>(true, false, record.StatusCode, response);
    }

    public static void Add<TResponse>(
        AppDbContext db,
        Guid userId,
        IdempotencyRequestContext? request,
        int statusCode,
        TResponse response)
    {
        if (request is null)
            return;

        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            UserId = userId,
            Scope = request.Scope,
            Key = request.Key,
            RequestHash = request.RequestHash,
            StatusCode = statusCode,
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
    }

    public static ObjectResult Conflict(ControllerBase controller) =>
        controller.Conflict(new
        {
            message = "Idempotency-Key đã được dùng với nội dung yêu cầu khác."
        });
}
