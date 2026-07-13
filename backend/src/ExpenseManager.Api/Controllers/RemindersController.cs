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
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => x.ToResponse()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> Create(
        ReminderRequest request,
        CancellationToken cancellationToken)
    {
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
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, reminder.ToResponse());
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

        reminder.Content = request.Content.Trim();
        reminder.DayOfMonth = request.DayOfMonth;
        reminder.Hour = request.Hour;
        reminder.Minute = request.Minute;
        reminder.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(reminder.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (reminder is null)
            return NotFound();

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
