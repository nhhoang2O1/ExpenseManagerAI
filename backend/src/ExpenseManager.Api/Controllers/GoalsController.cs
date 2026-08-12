using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public sealed class GoalsController(IGoalsApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> GetAll(CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<GoalResponse>> Create(
        GoalRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalResponse>> Update(
        Guid id, GoalRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.UpdateAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken)).Result!;

    [HttpPost("{id:guid}/funds")]
    public async Task<ActionResult<GoalResponse>> AddFunds(
        Guid id, AddGoalFundsRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.AddFundsAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["Idempotency-Key"].ToString(),
            ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpGet("available-balance")]
    public async Task<ActionResult<AvailableBalanceResponse>> GetAvailableBalance(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAvailableBalanceAsync(year, month, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<GoalResponse>> Complete(
        Guid id, CompleteGoalRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CompleteAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["Idempotency-Key"].ToString(),
            ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<GoalResponse>> Cancel(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CancelAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<GoalHistoryResponse>>> GetHistory(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetHistoryAsync(id, cancellationToken));
}
