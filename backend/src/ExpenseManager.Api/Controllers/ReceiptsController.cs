using System.Text.Json;
using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receipts")]
public sealed class ReceiptsController(
    AppDbContext db,
    IUserContext userContext,
    IReceiptFileStorage fileStorage,
    IReceiptProcessingService processingService,
    IReceiptConfirmationService confirmationService,
    ILogger<ReceiptsController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ReceiptUploadResponse>> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Cần chọn ảnh hóa đơn." });

        (string Path, long Size) saved;
        try
        {
            saved = await fileStorage.SaveAsync(file, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var receipt = new Receipt
        {
            UserId = userContext.UserId,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            FilePath = saved.Path,
            FileSize = saved.Size
        };
        db.Receipts.Add(receipt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            fileStorage.Delete(saved.Path);
            throw;
        }

        return StatusCode(StatusCodes.Status201Created, new ReceiptUploadResponse(
            receipt.Id, receipt.Status, receipt.Classification, receipt.CreatedAt));
    }

    [HttpPost("{id:guid}/process")]
    public Task<ActionResult<ReceiptResponse>> Process(
        Guid id,
        CancellationToken cancellationToken) => ProcessInternal(id, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public Task<ActionResult<ReceiptResponse>> Retry(
        Guid id,
        CancellationToken cancellationToken) => ProcessInternal(id, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.AsNoTracking().Include(x => x.OcrResult)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        return receipt is null ? NotFound() : Ok(receipt.ToResponse());
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<TransactionResponse>> Confirm(
        Guid id,
        ConfirmReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await confirmationService.ConfirmAsync(
            id, userContext.UserId, request, cancellationToken);
        return result.Outcome switch
        {
            ConfirmationOutcome.RECEIPT_NOT_FOUND => NotFound(),
            ConfirmationOutcome.CATEGORY_NOT_FOUND =>
                BadRequest(new { message = "Danh mục chi không hợp lệ." }),
            ConfirmationOutcome.INVALID_RECEIPT_STATE =>
                Conflict(new { message = "Hóa đơn chưa sẵn sàng để xác nhận." }),
            _ => Ok(result.Transaction!.ToResponse())
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.Include(x => x.Transaction).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (receipt is null)
            return NotFound();
        if (receipt.Transaction is not null)
            return Conflict(new { message = "Không thể xóa hóa đơn đã tạo giao dịch." });

        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(cancellationToken);
        fileStorage.Delete(receipt.FilePath);
        return NoContent();
    }

    private async Task<ActionResult<ReceiptResponse>> ProcessInternal(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await processingService.ProcessAsync(
                id, userContext.UserId, cancellationToken);
            return receipt is null ? NotFound() : Ok(receipt.ToResponse());
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or JsonException)
        {
            logger.LogWarning(ex, "OCR processing failed for receipt {ReceiptId}", id);
            var receipt = await db.Receipts.AsNoTracking().Include(x => x.OcrResult)
                .SingleAsync(x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, receipt.ToResponse());
        }
    }
}
