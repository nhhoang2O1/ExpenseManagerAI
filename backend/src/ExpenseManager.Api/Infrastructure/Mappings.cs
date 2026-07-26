using System.Text.Json;
using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Infrastructure;

public static class Mappings
{
    public static CategoryResponse ToResponse(this Category category) =>
        new(category.Id, category.Name, category.Type, category.Color, category.Icon, category.Version);

    public static TransactionResponse ToResponse(this Domain.Transaction transaction) =>
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
            transaction.Version);

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
            goal.Version);

    public static GoalHistoryResponse ToResponse(this GoalHistory history) =>
        new(history.Id, history.GoalId, history.AmountAdded, history.Date,
            history.RequestedAmount, history.BalanceAfter);

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
            receipt.ProcessingAttempts,
            receipt.NextRetryAt,
            receipt.LastError,
            receipt.CreatedAt,
            receipt.UpdatedAt,
            receipt.Version);
    }
}
