using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Controllers;
using ExpenseManager.Api.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Tests;

public sealed class ReminderIntegrationTests
{
    [Fact]
    public async Task Create_is_idempotent_and_list_returns_only_current_users_reminders()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("owner");
        var anotherUser = User("other");
        var foreignReminder = new Reminder
        {
            UserId = anotherUser.Id,
            User = anotherUser,
            Content = "Foreign reminder",
            DayOfMonth = 10,
            Hour = 7,
            Minute = 30,
            IsActive = true
        };
        db.AddRange(owner, anotherUser, foreignReminder);
        await db.SaveChangesAsync();
        var controller = Controller(db, owner.Id);
        controller.Request.Headers["Idempotency-Key"] = "reminder-create-1";
        var request = new ReminderRequest("  Electricity bill  ", 20, 8, 15, true);

        var first = await controller.Create(request, CancellationToken.None);
        var replay = await controller.Create(request, CancellationToken.None);
        var listed = await controller.GetAll(CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(first.Result).StatusCode);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(replay.Result).StatusCode);
        var ownerReminder = Assert.Single(await db.Reminders.Where(x => x.UserId == owner.Id).ToListAsync());
        Assert.Equal("Electricity bill", ownerReminder.Content);
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
        var response = Assert.IsAssignableFrom<IReadOnlyList<ReminderResponse>>(
            Assert.IsType<OkObjectResult>(listed.Result).Value);
        Assert.Single(response);
        Assert.DoesNotContain(response, item => item.Id == foreignReminder.Id);
    }

    [Fact]
    public async Task Stale_update_and_foreign_delete_are_rejected()
    {
        await using var db = TestSupport.CreateDb();
        var owner = User("owner");
        var anotherUser = User("other");
        var ownerReminder = Reminder(owner, "Owner reminder", 7);
        var foreignReminder = Reminder(anotherUser, "Foreign reminder", 1);
        db.AddRange(owner, anotherUser, ownerReminder, foreignReminder);
        await db.SaveChangesAsync();
        var controller = Controller(db, owner.Id);
        controller.Request.Headers["If-Match"] = "\"6\"";

        var stale = await controller.Update(
            ownerReminder.Id,
            new ReminderRequest("Changed", 15, 9, 0, true),
            CancellationToken.None);
        var foreignDelete = await controller.Delete(foreignReminder.Id, CancellationToken.None);

        var failed = Assert.IsType<ObjectResult>(stale.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, failed.StatusCode);
        Assert.Equal("Owner reminder", ownerReminder.Content);
        Assert.IsType<NotFoundResult>(foreignDelete);
        Assert.True(await db.Reminders.AnyAsync(x => x.Id == foreignReminder.Id));
    }

    private static RemindersController Controller(ExpenseManager.Api.Data.AppDbContext db, Guid userId) =>
        new(new ExpenseManager.Api.Services.RemindersApplicationService(
            db, new TestUserContext(userId)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static User User(string suffix) => new()
    {
        Name = $"Reminder {suffix}",
        Email = $"reminder-{suffix}-{Guid.NewGuid():N}@example.com",
        PasswordHash = "hash"
    };

    private static Reminder Reminder(User user, string content, long version) => new()
    {
        UserId = user.Id,
        User = user,
        Content = content,
        DayOfMonth = 10,
        Hour = 8,
        Minute = 0,
        IsActive = true,
        Version = version
    };
}
