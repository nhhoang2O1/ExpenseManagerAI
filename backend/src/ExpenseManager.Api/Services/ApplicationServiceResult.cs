using Microsoft.AspNetCore.Mvc;
using ExpenseManager.Api.Infrastructure;

namespace ExpenseManager.Api.Services;

/// <summary>
/// Transport-neutral result returned by application services. Controllers map
/// this result to HTTP responses; application services do not create MVC
/// results or depend on ControllerBase.
/// </summary>
public sealed record ApplicationServiceResult<T>(
    int StatusCode,
    T? Value = default,
    string? Message = null,
    long? Version = null)
{
    public static ApplicationServiceResult<T> Ok(T value, long? version = null) =>
        new(StatusCodes.Status200OK, value, Version: version);

    public static ApplicationServiceResult<T> Created(T value, long? version = null) =>
        new(StatusCodes.Status201Created, value, Version: version);

    public static ApplicationServiceResult<T> Accepted(T value) =>
        new(StatusCodes.Status202Accepted, value);

    public static ApplicationServiceResult<T> NoContent() =>
        new(StatusCodes.Status204NoContent);

    public static ApplicationServiceResult<T> BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, Message: message);

    public static ApplicationServiceResult<T> Unauthorized(string message) =>
        new(StatusCodes.Status401Unauthorized, Message: message);

    public static ApplicationServiceResult<T> NotFound() =>
        new(StatusCodes.Status404NotFound);

    public static ApplicationServiceResult<T> Conflict(string message) =>
        new(StatusCodes.Status409Conflict, Message: message);

    public static ApplicationServiceResult<T> PreconditionFailed() =>
        new(StatusCodes.Status412PreconditionFailed,
            Message: "Dữ liệu đã được thay đổi bởi yêu cầu khác. Hãy tải lại và thử lại.");
}

public static class ApplicationServiceResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(
        this ControllerBase controller,
        ApplicationServiceResult<T> result)
    {
        if (result.Version.HasValue && controller.ControllerContext.HttpContext is not null)
            OptimisticConcurrency.WriteEtag(controller.Response, result.Version.Value);

        return result.StatusCode switch
        {
            StatusCodes.Status204NoContent => new NoContentResult(),
            StatusCodes.Status202Accepted => new AcceptedResult((string?)null, result.Value),
            StatusCodes.Status404NotFound => new NotFoundResult(),
            StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(
                new { message = result.Message }),
            StatusCodes.Status400BadRequest => new BadRequestObjectResult(new { message = result.Message }),
            StatusCodes.Status409Conflict => new ConflictObjectResult(new { message = result.Message }),
            _ when result.StatusCode >= 400 => new ObjectResult(
                new { message = result.Message }) { StatusCode = result.StatusCode },
            _ when result.Value is null => new StatusCodeResult(result.StatusCode),
            StatusCodes.Status200OK => new OkObjectResult(result.Value),
            _ => new ObjectResult(result.Value) { StatusCode = result.StatusCode }
        };
    }
}
