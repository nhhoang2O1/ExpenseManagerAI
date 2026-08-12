using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(IReportsApplicationService service) : ControllerBase
{
    [HttpGet("range.xlsx")]
    public async Task<IActionResult> RangeXlsx(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateExcelAsync(from, to, cancellationToken);
        if (result.StatusCode >= 400)
            return this.ToActionResult(result).Result!;
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("range.pdf")]
    public async Task<IActionResult> RangePdf(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePdfAsync(from, to, cancellationToken);
        if (result.StatusCode >= 400)
            return this.ToActionResult(result).Result!;
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }
}
