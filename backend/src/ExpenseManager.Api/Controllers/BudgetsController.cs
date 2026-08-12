using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budgets")]
public sealed class BudgetsController(IBudgetsApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetResponse>>> GetAll(
        [FromQuery] string? monthYear, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAllAsync(monthYear, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> CreateOrUpdate(
        BudgetRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CreateOrUpdateAsync(
            request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BudgetResponse>> Update(
        Guid id, BudgetRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.UpdateAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken)).Result!;
}
