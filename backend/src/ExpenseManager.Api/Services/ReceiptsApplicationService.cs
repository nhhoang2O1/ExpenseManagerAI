using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public sealed record ReceiptUploadInput(
    byte[] Data, long Size, string FileName, string ContentType);

public sealed record ImageFileResult(byte[] Content, string ContentType);

public interface IReceiptsApplicationService
{
    Task<ApplicationServiceResult<ReceiptUploadResponse>> UploadAsync(
        ReceiptUploadInput input, string? idempotencyKey, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<ReceiptResponse>> ProcessAsync(
        Guid id, bool explicitRetry, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<PagedResponse<ReceiptResponse>>> ListAsync(
        int page, int pageSize, ReceiptStatus? status, DateTime? createdFrom,
        DateTime? createdTo, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<ReceiptResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<ImageFileResult>> GetImageAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<TransactionResponse>> ConfirmAsync(
        Guid id, ConfirmReceiptRequest request, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<object?>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ReceiptsApplicationService(
    AppDbContext db,
    IUserContext userContext,
    IReceiptProcessingService processingService,
    IReceiptConfirmationService confirmationService,
    ICategorySuggestionService categorySuggestionService) : IReceiptsApplicationService
{
    public async Task<ApplicationServiceResult<ReceiptUploadResponse>> UploadAsync(
        ReceiptUploadInput input, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var imageHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(input.Data));
        if (!IdempotencySupport.TryCreate(
                idempotencyKey, "receipts:upload",
                new { input.FileName, input.ContentType, input.Size, ImageHash = imageHash },
                out var idempotency, out var keyError))
            return ApplicationServiceResult<ReceiptUploadResponse>.BadRequest(keyError!);

        var replay = await IdempotencySupport.FindAsync<ReceiptUploadResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return ApplicationServiceResult<ReceiptUploadResponse>.Conflict(
                "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true && replay.Response is not null)
            return new ApplicationServiceResult<ReceiptUploadResponse>(
                replay.StatusCode, replay.Response, Version: replay.Response.Version);

        var receipt = new Receipt
        {
            UserId = userContext.UserId,
            OriginalFileName = input.FileName,
            ContentType = input.ContentType,
            FileSize = input.Size,
            Image = new ReceiptImage { Data = input.Data }
        };
        db.Receipts.Add(receipt);
        var response = new ReceiptUploadResponse(
            receipt.Id, receipt.Status, receipt.Classification, receipt.CreatedAt, receipt.Version);
        IdempotencySupport.Add(db, userContext.UserId, idempotency,
            StatusCodes.Status201Created, response);
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
                return ApplicationServiceResult<ReceiptUploadResponse>.Conflict(
                    "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
            if (replay?.Exists == true && replay.Response is not null)
                return new ApplicationServiceResult<ReceiptUploadResponse>(
                    replay.StatusCode, replay.Response, Version: replay.Response.Version);
            throw;
        }
        return ApplicationServiceResult<ReceiptUploadResponse>.Created(response, receipt.Version);
    }

    public async Task<ApplicationServiceResult<ReceiptResponse>> ProcessAsync(
        Guid id, bool explicitRetry, CancellationToken cancellationToken)
    {
        var receipt = await processingService.EnqueueAsync(
            id, userContext.UserId, explicitRetry, cancellationToken);
        return receipt is null
            ? ApplicationServiceResult<ReceiptResponse>.NotFound()
            : ApplicationServiceResult<ReceiptResponse>.Accepted(receipt.ToResponse());
    }

    public async Task<ApplicationServiceResult<PagedResponse<ReceiptResponse>>> ListAsync(
        int page, int pageSize, ReceiptStatus? status, DateTime? createdFrom,
        DateTime? createdTo, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return ApplicationServiceResult<PagedResponse<ReceiptResponse>>.BadRequest(
                "page must be at least 1 and pageSize must be between 1 and 100.");
        if (createdFrom is not null && createdTo is not null && createdFrom > createdTo)
            return ApplicationServiceResult<PagedResponse<ReceiptResponse>>.BadRequest(
                "createdFrom must be earlier than or equal to createdTo.");

        var query = db.Receipts.AsNoTracking().Where(x => x.UserId == userContext.UserId);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (createdFrom is not null) query = query.Where(x => x.CreatedAt >= createdFrom);
        if (createdTo is not null) query = query.Where(x => x.CreatedAt <= createdTo);
        var totalCount = await query.CountAsync(cancellationToken);
        var receipts = await query.Include(x => x.OcrResult)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var response = new PagedResponse<ReceiptResponse>(
            receipts.Select(x => x.ToResponse()).ToList(), page, pageSize, totalCount, totalPages);
        return ApplicationServiceResult<PagedResponse<ReceiptResponse>>.Ok(response);
    }

    public async Task<ApplicationServiceResult<ReceiptResponse>> GetAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.AsNoTracking().Include(x => x.OcrResult)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (receipt is null)
            return ApplicationServiceResult<ReceiptResponse>.NotFound();
        var suggestion = await categorySuggestionService.SuggestAsync(
            userContext.UserId, receipt.OcrResult, cancellationToken);
        var response = receipt.ToResponse();
        if (suggestion is not null)
            response = response with
            {
                SuggestedCategoryId = suggestion.CategoryId,
                SuggestedCategoryName = suggestion.CategoryName,
                CategoryConfidence = suggestion.Confidence,
                CategoryReason = suggestion.Reason
            };
        return ApplicationServiceResult<ReceiptResponse>.Ok(response);
    }

    public async Task<ApplicationServiceResult<ImageFileResult>> GetImageAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var image = await db.Receipts.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userContext.UserId)
            .Select(x => new { x.ContentType, Data = x.Image.Data })
            .SingleOrDefaultAsync(cancellationToken);
        return image is null
            ? ApplicationServiceResult<ImageFileResult>.NotFound()
            : ApplicationServiceResult<ImageFileResult>.Ok(new ImageFileResult(image.Data, image.ContentType));
    }

    public async Task<ApplicationServiceResult<TransactionResponse>> ConfirmAsync(
        Guid id, ConfirmReceiptRequest request, CancellationToken cancellationToken)
    {
        var result = await confirmationService.ConfirmAsync(
            id, userContext.UserId, request, cancellationToken);
        return result.Outcome switch
        {
            ConfirmationOutcome.RECEIPT_NOT_FOUND =>
                ApplicationServiceResult<TransactionResponse>.NotFound(),
            ConfirmationOutcome.CATEGORY_NOT_FOUND =>
                ApplicationServiceResult<TransactionResponse>.BadRequest("Danh mục chi không hợp lệ."),
            ConfirmationOutcome.INVALID_RECEIPT_STATE =>
                ApplicationServiceResult<TransactionResponse>.Conflict(
                    "Hóa đơn chưa sẵn sàng để xác nhận."),
            _ => ApplicationServiceResult<TransactionResponse>.Ok(result.Transaction!.ToResponse())
        };
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.Include(x => x.Transaction).Include(x => x.Image)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (receipt is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (receipt.Transaction is not null)
            return ApplicationServiceResult<object?>.Conflict(
                "Không thể xóa hóa đơn đã tạo giao dịch.");
        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(cancellationToken);
        return ApplicationServiceResult<object?>.NoContent();
    }
}
