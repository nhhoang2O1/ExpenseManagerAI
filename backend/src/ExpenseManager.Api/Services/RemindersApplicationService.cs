using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface IRemindersApplicationService
{
    Task<ApplicationServiceResult<IReadOnlyList<ReminderResponse>>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<ApplicationServiceResult<ReminderResponse>> CreateAsync(
        ReminderRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<ReminderResponse>> UpdateAsync(
        Guid id, ReminderRequest request, string? ifMatch, CancellationToken cancellationToken);

    Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken);
}

public sealed class RemindersApplicationService(AppDbContext db, IUserContext userContext)
    : IRemindersApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<ReminderResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var items = await db.Reminders.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderBy(x => x.DayOfMonth)
            .ThenBy(x => x.Hour)
            .ThenBy(x => x.Minute)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        IReadOnlyList<ReminderResponse> result = items.Select(x => x.ToResponse()).ToList();
        return ApplicationServiceResult<IReadOnlyList<ReminderResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<ReminderResponse>> CreateAsync(
        ReminderRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!IdempotencySupport.TryCreate(
                idempotencyKey, "reminders:create", request, out var idempotency, out var keyError))
            return ApplicationServiceResult<ReminderResponse>.BadRequest(keyError!);

        var replay = await IdempotencySupport.FindAsync<ReminderResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return ApplicationServiceResult<ReminderResponse>.Conflict(
                "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true && replay.Response is not null)
            return new ApplicationServiceResult<ReminderResponse>(
                replay.StatusCode, replay.Response, Version: replay.Response.Version);

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
        IdempotencySupport.Add(db, userContext.UserId, idempotency,
            StatusCodes.Status201Created, response);
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
                return ApplicationServiceResult<ReminderResponse>.Conflict(
                    "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
            if (replay?.Exists == true && replay.Response is not null)
                return new ApplicationServiceResult<ReminderResponse>(
                    replay.StatusCode, replay.Response, Version: replay.Response.Version);
            throw;
        }
        return ApplicationServiceResult<ReminderResponse>.Created(response, reminder.Version);
    }

    public async Task<ApplicationServiceResult<ReminderResponse>> UpdateAsync(
        Guid id, ReminderRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (reminder is null)
            return ApplicationServiceResult<ReminderResponse>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, reminder.Version))
            return ApplicationServiceResult<ReminderResponse>.PreconditionFailed();

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
            return ApplicationServiceResult<ReminderResponse>.PreconditionFailed();
        }
        return ApplicationServiceResult<ReminderResponse>.Ok(reminder.ToResponse(), reminder.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (reminder is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, reminder.Version))
            return ApplicationServiceResult<object?>.PreconditionFailed();

        db.Reminders.Remove(reminder);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<object?>.PreconditionFailed();
        }
        return ApplicationServiceResult<object?>.NoContent();
    }
}
