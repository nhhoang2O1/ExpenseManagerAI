using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statistics")]
public sealed class StatisticsController(IStatisticsApplicationService service) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<ActionResult<IReadOnlyList<DailyStatisticResponse>>> Daily(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetDailyAsync(from, to, cancellationToken));

    [HttpGet("monthly")]
    public async Task<ActionResult<IReadOnlyList<MonthlyStatisticResponse>>> Monthly(
        [FromQuery] int? year, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetMonthlyAsync(year, cancellationToken));

    [HttpGet("by-category")]
    public async Task<ActionResult<IReadOnlyList<CategoryStatisticResponse>>> ByCategory(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetByCategoryAsync(from, to, cancellationToken));
}
