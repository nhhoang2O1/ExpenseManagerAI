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
    IReceiptImageReader imageReader,
    IReceiptProcessingService processingService,
    IReceiptConfirmationService confirmationService,
    ICategorySuggestionService categorySuggestionService) : ControllerBase
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

        (byte[] Data, long Size) saved;
        try
        {
            saved = await imageReader.ReadAsync(file, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var imageHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(saved.Data));
        if (!IdempotencySupport.TryCreate(
                this,
                "receipts:upload",
                new
                {
                    FileName = Path.GetFileName(file.FileName),
                    file.ContentType,
                    saved.Size,
                    ImageHash = imageHash
                },
                out var idempotency,
                out var keyError))
            return keyError!;
        var replay = await IdempotencySupport.FindAsync<ReceiptUploadResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return IdempotencySupport.Conflict(this);
        if (replay?.Exists == true && replay.Response is not null)
            return StatusCode(replay.StatusCode, replay.Response);

        var receipt = new Receipt
        {
            UserId = userContext.UserId,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            FileSize = saved.Size,
            Image = new ReceiptImage { Data = saved.Data }
        };
        db.Receipts.Add(receipt);
        var response = new ReceiptUploadResponse(
            receipt.Id, receipt.Status, receipt.Classification, receipt.CreatedAt, receipt.Version);
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
            replay = await IdempotencySupport.FindAsync<ReceiptUploadResponse>(
                db, userContext.UserId, idempotency, cancellationToken);
            if (replay?.RequestConflict == true)
                return IdempotencySupport.Conflict(this);
            if (replay?.Exists == true && replay.Response is not null)
                return StatusCode(replay.StatusCode, replay.Response);
            throw;
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{id:guid}/process")]
    public Task<ActionResult<ReceiptResponse>> Process(
        Guid id,
        CancellationToken cancellationToken) =>
        EnqueueInternal(id, explicitRetry: false, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public Task<ActionResult<ReceiptResponse>> Retry(
        Guid id,
        CancellationToken cancellationToken) =>
        EnqueueInternal(id, explicitRetry: true, cancellationToken);

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ReceiptResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReceiptStatus? status = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "page must be at least 1 and pageSize must be between 1 and 100." });
        if (createdFrom is not null && createdTo is not null && createdFrom > createdTo)
            return BadRequest(new { message = "createdFrom must be earlier than or equal to createdTo." });

        var query = db.Receipts.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId);
        if (status is not null)
            query = query.Where(x => x.Status == status);
        if (createdFrom is not null)
            query = query.Where(x => x.CreatedAt >= createdFrom);
        if (createdTo is not null)
            query = query.Where(x => x.CreatedAt <= createdTo);

        var totalCount = await query.CountAsync(cancellationToken);
        var receipts = await query
            .Include(x => x.OcrResult)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResponse<ReceiptResponse>(
            receipts.Select(x => x.ToResponse()).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.AsNoTracking().Include(x => x.OcrResult)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (receipt is null)
            return NotFound();

        var suggestion = await categorySuggestionService.SuggestAsync(
            userContext.UserId, receipt.OcrResult, cancellationToken);
        var response = receipt.ToResponse();
        if (suggestion is not null)
        {
            response = response with
            {
                SuggestedCategoryId = suggestion.CategoryId,
                SuggestedCategoryName = suggestion.CategoryName,
                CategoryConfidence = suggestion.Confidence,
                CategoryReason = suggestion.Reason
            };
        }
        return Ok(response);
    }

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(
        Guid id,
        CancellationToken cancellationToken)
    {
        var image = await db.Receipts.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userContext.UserId)
            .Select(x => new
            {
                x.ContentType,
                Data = x.Image.Data
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (image is null)
            return NotFound();

        return File(
            new MemoryStream(image.Data, writable: false),
            image.ContentType,
            enableRangeProcessing: true);
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
        var receipt = await db.Receipts
            .Include(x => x.Transaction)
            .Include(x => x.Image)
            .SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (receipt is null)
            return NotFound();
        if (receipt.Transaction is not null)
            return Conflict(new { message = "Không thể xóa hóa đơn đã tạo giao dịch." });

        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult<ReceiptResponse>> EnqueueInternal(
        Guid id,
        bool explicitRetry,
        CancellationToken cancellationToken)
    {
        var receipt = await processingService.EnqueueAsync(
            id,
            userContext.UserId,
            explicitRetry,
            cancellationToken);
        return receipt is null
            ? NotFound()
            : AcceptedAtAction(nameof(Get), new { id }, receipt.ToResponse());
    }
}
