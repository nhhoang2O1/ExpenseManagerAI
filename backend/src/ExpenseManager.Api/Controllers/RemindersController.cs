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
[Route("api/reminders")]
public sealed class RemindersController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await db.Reminders.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderBy(x => x.DayOfMonth)
            .ThenBy(x => x.Hour)
            .ThenBy(x => x.Minute)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> Create(
        ReminderRequest request,
        CancellationToken cancellationToken)
    {
        if (!IdempotencySupport.TryCreate(
                this, "reminders:create", request, out var idempotency, out var keyError))
            return keyError!;
        var replay = await IdempotencySupport.FindAsync<ReminderResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return IdempotencySupport.Conflict(this);
        if (replay?.Exists == true && replay.Response is not null)
        {
            OptimisticConcurrency.WriteEtag(this, replay.Response.Version);
            return StatusCode(replay.StatusCode, replay.Response);
        }

        var reminder = new Reminder
        {
            UserId = userContext.UserId,
            Content = request.Content.Trim(),
            DayOfMonth = request.DayOfMonth,
            Hour = request.Hour,
            Minute = request.Minute,
            IsActive = request.IsActive
        };
        db.Reminders.Add(reminder);
        var response = reminder.ToResponse();
        IdempotencySupport.Add(
            db,
            userContext.UserId,
            idempotency,
            StatusCodes.Status201Created,
            response);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotency is not null)
        {
            db.ChangeTracker.Clear();
            replay = await IdempotencySupport.FindAsync<ReminderResponse>(
                db, userContext.UserId, idempotency, cancellationToken);
            if (replay?.RequestConflict == true)
                return IdempotencySupport.Conflict(this);
            if (replay?.Exists == true && replay.Response is not null)
                return StatusCode(replay.StatusCode, replay.Response);
            throw;
        }
        OptimisticConcurrency.WriteEtag(this, reminder.Version);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReminderResponse>> Update(
        Guid id,
        ReminderRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (reminder is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, reminder.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        reminder.Content = request.Content.Trim();
        reminder.DayOfMonth = request.DayOfMonth;
        reminder.Hour = request.Hour;
        reminder.Minute = request.Minute;
        reminder.IsActive = request.IsActive;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        OptimisticConcurrency.WriteEtag(this, reminder.Version);
        return Ok(reminder.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (reminder is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, reminder.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        db.Reminders.Remove(reminder);
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
}
