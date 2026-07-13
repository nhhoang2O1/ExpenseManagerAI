using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public sealed class GoalsController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await db.Goals.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<GoalResponse>> Create(
        GoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = new Goal
        {
            UserId = userContext.UserId,
            Name = request.Name.Trim(),
            TargetAmount = request.TargetAmount,
            CurrentAmount = Math.Min(request.CurrentAmount, request.TargetAmount)
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, goal.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalResponse>> Update(
        Guid id,
        GoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return NotFound();

        goal.Name = request.Name.Trim();
        goal.TargetAmount = request.TargetAmount;
        goal.CurrentAmount = Math.Min(request.CurrentAmount, request.TargetAmount);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(goal.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return NotFound();

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/funds")]
    public async Task<ActionResult<GoalResponse>> AddFunds(
        Guid id,
        AddGoalFundsRequest request,
        CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return NotFound();

        goal.CurrentAmount = Math.Min(goal.TargetAmount, goal.CurrentAmount + request.Amount);
        db.GoalHistories.Add(new GoalHistory
        {
            GoalId = goal.Id,
            AmountAdded = request.Amount
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(goal.ToResponse());
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<GoalHistoryResponse>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var exists = await db.Goals.AnyAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (!exists)
            return NotFound();

        var items = await db.GoalHistories.AsNoTracking()
            .Where(x => x.GoalId == id)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }
}
