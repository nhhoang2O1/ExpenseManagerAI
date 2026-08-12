using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionsApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? month,
        [FromQuery] TransactionType? type, [FromQuery] Guid? categoryId,
        [FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        this.ToActionResult(await service.GetAllAsync(
            from, to, month, type, categoryId, search, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        TransactionRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.CreateAsync(
            request, ControllerContext.HttpContext?.Request.Headers["Idempotency-Key"].ToString(), cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id, TransactionRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.UpdateAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<object?>> Delete(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));
}
