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
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<GoalResponse>> Create(
        GoalRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return BadRequest(new { message = "Tên mục tiêu không được để trống." });

        var goal = new Goal
        {
            UserId = userContext.UserId,
            Name = name,
            TargetAmount = request.TargetAmount,
            CurrentAmount = 0
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, goal.Version);
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
        if (!OptimisticConcurrency.IfMatchSatisfied(this, goal.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        var name = request.Name.Trim();
        if (name.Length == 0)
            return BadRequest(new { message = "Tên mục tiêu không được để trống." });

        if (request.TargetAmount < goal.CurrentAmount)
            return Conflict(new { message = "Target amount cannot be lower than the current balance." });

        goal.Name = name;
        goal.TargetAmount = request.TargetAmount;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        OptimisticConcurrency.WriteEtag(this, goal.Version);
        return Ok(goal.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, goal.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        db.Goals.Remove(goal);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/funds")]
    public async Task<ActionResult<GoalResponse>> AddFunds(
        Guid id,
        AddGoalFundsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (!IdempotencySupport.TryCreate(
                this, $"goals:{id}:add-funds", request, out var idempotency, out var keyError))
            return keyError!;

        // PostgreSQL's row lock serializes concurrent additions so none of the
        // increments is lost. Other providers are used only by fast unit tests.
        await using var transaction = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var goal = transaction is null
            ? await db.Goals.SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userContext.UserId, cancellationToken)
            : await db.Goals
                .FromSqlInterpolated($"SELECT * FROM goals WHERE id = {id} AND user_id = {userContext.UserId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        if (goal is null)
            return NotFound();
        var replay = await IdempotencySupport.FindAsync<GoalResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return IdempotencySupport.Conflict(this);
        if (replay?.Exists == true)
        {
            var current = goal.ToResponse();
            OptimisticConcurrency.WriteEtag(this, current.Version);
            return StatusCode(replay.StatusCode, current);
        }
        if (!OptimisticConcurrency.IfMatchSatisfied(this, goal.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        var funding = GoalFundingRules.Calculate(
            goal.TargetAmount, goal.CurrentAmount, request.Amount);
        if (funding.WasAlreadyFunded)
            return Conflict(new { message = "This goal is already fully funded." });
        var appliedAmount = funding.AppliedAmount;
        goal.CurrentAmount = funding.BalanceAfter;
        if (appliedAmount > 0)
        {
            db.GoalHistories.Add(new GoalHistory
            {
                GoalId = goal.Id,
                AmountAdded = appliedAmount,
                RequestedAmount = request.Amount,
                BalanceAfter = goal.CurrentAmount
            });
        }
        IdempotencySupport.Add(
            db,
            userContext.UserId,
            idempotency,
            StatusCodes.Status200OK,
            goal.ToResponse());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, goal.Version);
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
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }
}
