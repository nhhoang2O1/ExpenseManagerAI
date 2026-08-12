using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reminders")]
public sealed class RemindersController(IRemindersApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> Create(
        ReminderRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CreateAsync(
            request, ControllerContext.HttpContext?.Request.Headers["Idempotency-Key"].ToString(), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReminderResponse>> Update(
        Guid id, ReminderRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.UpdateAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken)).Result!;
}
