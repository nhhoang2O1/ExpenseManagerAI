using System.Text.Json;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface IReceiptProcessingService
{
    Task<Receipt?> ProcessAsync(Guid receiptId, Guid userId, CancellationToken cancellationToken);
}

public sealed class ReceiptProcessingService(
    AppDbContext db,
    IOcrClient ocrClient) : IReceiptProcessingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Receipt?> ProcessAsync(
        Guid receiptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.Include(x => x.OcrResult).SingleOrDefaultAsync(
            x => x.Id == receiptId && x.UserId == userId, cancellationToken);
        if (receipt is null)
            return null;
        if (receipt.Status == ReceiptStatus.CONFIRMED)
            return receipt;
        if (receipt.Status == ReceiptStatus.PROCESSING)
            return receipt;

        receipt.Status = ReceiptStatus.PROCESSING;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var response = await ocrClient.ProcessAsync(
                receipt.FilePath, receipt.ContentType, cancellationToken);
            receipt.Classification = response.Classification;
            receipt.Status = ReceiptStatus.REVIEW_REQUIRED;

            var result = receipt.OcrResult ?? new OcrResult
            {
                ReceiptId = receipt.Id,
                RawText = string.Empty,
                LinesJson = "[]",
                ModelVersion = string.Empty,
                ParserVersion = string.Empty,
                WarningsJson = "[]"
            };
            result.RawText = response.RawText ?? string.Empty;
            result.LinesJson = JsonSerializer.Serialize(response.Lines ?? [], JsonOptions);
            result.StoreName = response.Fields?.StoreName;
            result.ReceiptDate = response.Fields?.ReceiptDate;
            result.TotalAmount = response.Fields?.TotalAmount;
            result.VatAmount = response.Fields?.VatAmount;
            result.OverallConfidence = response.OverallConfidence;
            result.ModelVersion = response.ModelVersion ?? string.Empty;
            result.ParserVersion = response.ParserVersion ?? string.Empty;
            result.WarningsJson = JsonSerializer.Serialize(response.Warnings ?? [], JsonOptions);
            result.ProcessingTimeMs = response.ProcessingTimeMs;
            result.CreatedAt = DateTime.UtcNow;
            if (receipt.OcrResult is null)
            {
                receipt.OcrResult = result;
                db.OcrResults.Add(result);
            }
            await db.SaveChangesAsync(cancellationToken);
            return receipt;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            receipt.Status = ReceiptStatus.OCR_FAILED;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
