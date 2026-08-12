using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receipts")]
public sealed class ReceiptsController(
    IReceiptImageReader imageReader,
    IReceiptsApplicationService service) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ReceiptUploadResponse>> Upload(
        IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Cần chọn ảnh hóa đơn." });
        try
        {
            var saved = await imageReader.ReadAsync(file, cancellationToken);
            var result = await service.UploadAsync(
                new ReceiptUploadInput(saved.Data, saved.Size, Path.GetFileName(file.FileName), file.ContentType),
                ControllerContext.HttpContext?.Request.Headers["Idempotency-Key"].ToString(), cancellationToken);
            return this.ToActionResult(result).Result!;
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<ReceiptResponse>> Process(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await service.ProcessAsync(id, false, cancellationToken);
        return result.StatusCode == StatusCodes.Status202Accepted
            ? AcceptedAtAction(nameof(Get), new { id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<ReceiptResponse>> Retry(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await service.ProcessAsync(id, true, cancellationToken);
        return result.StatusCode == StatusCodes.Status202Accepted
            ? AcceptedAtAction(nameof(Get), new { id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ReceiptResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] ReceiptStatus? status = null, [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null, CancellationToken cancellationToken = default) =>
        this.ToActionResult(await service.ListAsync(
            page, pageSize, status, createdFrom, createdTo, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptResponse>> Get(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAsync(id, cancellationToken));

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetImageAsync(id, cancellationToken);
        if (result.StatusCode >= 400)
            return this.ToActionResult(result).Result!;
        return File(new MemoryStream(result.Value!.Content, writable: false),
            result.Value.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<TransactionResponse>> Confirm(
        Guid id, ConfirmReceiptRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.ConfirmAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<object?>> Delete(Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(id, cancellationToken));
}
