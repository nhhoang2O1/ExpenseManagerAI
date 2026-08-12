using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Infrastructure;

internal static class OptimisticConcurrency
{
    public static bool IfMatchSatisfied(string? raw, long currentVersion)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(candidate => candidate == "*" || TryReadVersion(candidate, out var version) &&
                version == currentVersion);
    }

    public static void WriteEtag(HttpResponse response, long version) =>
        response.Headers.ETag = $"\"{version}\"";

    public static bool IfMatchSatisfied(ControllerBase controller, long currentVersion)
    {
        var context = controller.ControllerContext.HttpContext;
        if (context is null)
            return true;

        return IfMatchSatisfied(
            context.Request.Headers["If-Match"].ToString(), currentVersion);
    }

    public static ObjectResult PreconditionFailed(ControllerBase controller) =>
        controller.StatusCode(StatusCodes.Status412PreconditionFailed, new
        {
            message = "Dữ liệu đã được thay đổi bởi yêu cầu khác. Hãy tải lại và thử lại."
        });

    public static void WriteEtag(ControllerBase controller, long version)
    {
        var context = controller.ControllerContext.HttpContext;
        if (context is not null)
            WriteEtag(context.Response, version);
    }

    private static bool TryReadVersion(string candidate, out long version)
    {
        var value = candidate.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            value = value[2..].Trim();
        value = value.Trim('"');
        return long.TryParse(value, out version);
    }
}
