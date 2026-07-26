using System.ComponentModel.DataAnnotations;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Contracts;

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required] string Password);

public sealed record UserResponse(Guid Id, string Name, string Email);
/// <summary>
/// Base login/register response retained for older clients. The new
/// AuthSessionResponse derives from it and adds the refresh-token pair.
/// </summary>
public record AuthResponse(string AccessToken, UserResponse User)
{
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; } = 900;
}

public sealed record CategoryRequest(
    [Required, StringLength(100)] string Name,
    TransactionType Type,
    [StringLength(20)] string? Color,
    [StringLength(50)] string? Icon);

public sealed record CategoryResponse(
    Guid Id, string Name, TransactionType Type, string? Color, string? Icon,
    long Version = 1);

public sealed record TransactionRequest(
    [Range(1, long.MaxValue)] long Amount,
    TransactionType Type,
    DateOnly TransactionDate,
    Guid CategoryId,
    [StringLength(1000)] string? Note,
    [StringLength(200)] string? StoreName);

public sealed record TransactionResponse(
    Guid Id,
    long Amount,
    TransactionType Type,
    DateOnly TransactionDate,
    Guid CategoryId,
    string CategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    string? Note,
    string? StoreName,
    Guid? ReceiptId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Version = 1);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record ReceiptUploadResponse(
    Guid Id,
    ReceiptStatus Status,
    ReceiptClassification? Classification,
    DateTime CreatedAt,
    long Version = 1);

public sealed record ReceiptResponse(
    Guid Id,
    ReceiptStatus Status,
    ReceiptClassification? Classification,
    string? StoreName,
    DateOnly? ReceiptDate,
    long? TotalAmount,
    long? VatAmount,
    decimal? OverallConfidence,
    IReadOnlyList<string> Warnings,
    string? RawText,
    string? ModelVersion,
    int ProcessingAttempts = 0,
    DateTime? NextRetryAt = null,
    string? LastError = null,
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null,
    long Version = 1,
    Guid? SuggestedCategoryId = null,
    string? SuggestedCategoryName = null,
    decimal? CategoryConfidence = null,
    string? CategoryReason = null);

public sealed record ConfirmReceiptRequest(
    [Required, StringLength(200)] string StoreName,
    DateOnly ReceiptDate,
    [Range(1, long.MaxValue)] long TotalAmount,
    [Range(0, long.MaxValue)] long? VatAmount,
    Guid CategoryId,
    [StringLength(1000)] string? Note);

public sealed record DailyStatisticResponse(
    DateOnly Date, long Income, long Expense, long Balance);

public sealed record MonthlyStatisticResponse(
    int Year, int Month, long Income, long Expense, long Balance);

public sealed record CategoryStatisticResponse(
    Guid CategoryId,
    string CategoryName,
    TransactionType Type,
    long Total,
    int TransactionCount,
    string? CategoryColor,
    string? CategoryIcon);

public sealed record BudgetRequest(
    Guid CategoryId,
    [Range(1, long.MaxValue)] long Amount,
    [Required, RegularExpression(@"^\d{4}-\d{2}$")] string MonthYear);

public sealed record BudgetResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    long Amount,
    string MonthYear,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Version = 1);

public sealed record GoalRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(1, long.MaxValue)] long TargetAmount,
    [Range(0, long.MaxValue)] long CurrentAmount = 0);

public sealed record AddGoalFundsRequest(
    [Range(1, long.MaxValue)] long Amount);

public sealed record GoalResponse(
    Guid Id,
    string Name,
    long TargetAmount,
    long CurrentAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Version = 1);

public sealed record GoalHistoryResponse(
    Guid Id,
    Guid GoalId,
    long AmountAdded,
    DateTime Date,
    long? RequestedAmount = null,
    long? BalanceAfter = null);

public sealed record ReminderRequest(
    [Required, StringLength(500, MinimumLength = 1)] string Content,
    [Range(1, 31)] int DayOfMonth,
    [Range(0, 23)] int Hour,
    [Range(0, 59)] int Minute,
    bool IsActive = true);

public sealed record ReminderResponse(
    Guid Id,
    string Content,
    int DayOfMonth,
    int Hour,
    int Minute,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Version = 1);
