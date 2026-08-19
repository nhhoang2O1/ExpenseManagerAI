using System.Text.Json;
using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Infrastructure;

public static class Mappings
{
    public static CategoryResponse ToResponse(this Category category) =>
        new(category.Id, category.Name, category.Type, category.Color, category.Icon, category.Version, category.IsActive);

    public static TransactionResponse ToResponse(
        this Domain.Transaction transaction,
        BudgetAlertResponse? budgetAlert = null) =>
        new(
            transaction.Id,
            transaction.Amount,
            transaction.Type,
            transaction.TransactionDate,
            transaction.CategoryId,
            transaction.Category.Name,
            transaction.Category.Color,
            transaction.Category.Icon,
            transaction.Note,
            transaction.StoreName,
            transaction.ReceiptId,
            transaction.CreatedAt,
            transaction.UpdatedAt,
            transaction.Version,
            budgetAlert);

    public static BudgetResponse ToResponse(this Budget budget) =>
        new(
            budget.Id,
            budget.CategoryId,
            budget.Category.Name,
            budget.Category.Color,
            budget.Category.Icon,
            budget.Amount,
            budget.MonthYear,
            budget.CreatedAt,
            budget.UpdatedAt,
            budget.Version);

    public static GoalResponse ToResponse(this Goal goal) =>
        new(
            goal.Id,
            goal.Name,
            goal.TargetAmount,
            goal.CurrentAmount,
            goal.CreatedAt,
            goal.UpdatedAt,
            goal.Status,
            goal.CompletedAt,
            goal.Version);

    public static GoalHistoryResponse ToResponse(this GoalHistory history) =>
        new(history.Id, history.GoalId, history.AmountAdded, history.Date,
            history.RequestedAmount, history.BalanceAfter, history.ActionType);

    public static ReminderResponse ToResponse(this Reminder reminder) =>
        new(
            reminder.Id,
            reminder.Content,
            reminder.DayOfMonth,
            reminder.Hour,
            reminder.Minute,
            reminder.IsActive,
            reminder.CreatedAt,
            reminder.UpdatedAt,
            reminder.Version);

    public static ReceiptResponse ToResponse(this Receipt receipt)
    {
        var result = receipt.OcrResult;
        var warnings = result is null
            ? []
            : JsonSerializer.Deserialize<List<string>>(result.WarningsJson) ?? [];
        var lines = result is null ? [] : ReadOcrLines(result.LinesJson);

        return new ReceiptResponse(
            receipt.Id,
            receipt.Status,
            receipt.Classification,
            result?.StoreName,
            result?.ReceiptDate,
            result?.TotalAmount,
            result?.VatAmount,
            result?.OverallConfidence,
            warnings,
            result?.RawText,
            result?.ModelVersion,
            lines,
            receipt.ProcessingAttempts,
            receipt.NextRetryAt,
            receipt.LastError,
            receipt.CreatedAt,
            receipt.UpdatedAt,
            receipt.Version);
    }

    private static IReadOnlyList<OcrLineResponse> ReadOcrLines(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateArray()
                .Where(x => x.TryGetProperty("text", out _))
                .Select(x => new OcrLineResponse(x.GetProperty("text").GetString() ?? string.Empty))
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
