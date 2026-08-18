namespace ExpenseManager.Api.Domain;

public enum TransactionType
{
    INCOME,
    EXPENSE
}

public enum BudgetAlertLevel
{
    APPROACHING,
    EXCEEDED
}

public enum GoalStatus
{
    ACTIVE,
    READY_TO_COMPLETE,
    COMPLETED,
    CANCELLED
}

public enum GoalHistoryActionType
{
    FUND,
    COMPLETE,
    CANCEL
}

public enum ReceiptStatus
{
    UPLOADED,
    QUEUED,
    PROCESSING,
    REVIEW_REQUIRED,
    OCR_FAILED,
    CONFIRMED
}

public enum ReceiptClassification
{
    SUPPORTED,
    GENERIC,
    UNRECOGNIZED,
    LOW_QUALITY
}

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Incremented whenever all access tokens must be invalidated.</summary>
    public int TokenVersion { get; set; }
    public int FinancialCycleStartDay { get; set; } = 1;
    public long Version { get; set; } = 1;
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Receipt> Receipts { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<Goal> Goals { get; set; } = [];
    public ICollection<Reminder> Reminders { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<AccountVerificationCode> VerificationCodes { get; set; } = [];
}

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public TransactionType Type { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
}

public sealed class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public long Amount { get; set; }
    public required string MonthYear { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

public sealed class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public long TargetAmount { get; set; }
    public long CurrentAmount { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.ACTIVE;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
    public User User { get; set; } = null!;
    public ICollection<GoalHistory> History { get; set; } = [];
    public Transaction? CompletionTransaction { get; set; }
}

public sealed class GoalHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public long AmountAdded { get; set; }
    public long? RequestedAmount { get; set; }
    public long? BalanceAfter { get; set; }
    public GoalHistoryActionType ActionType { get; set; } = GoalHistoryActionType.FUND;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public Goal Goal { get; set; } = null!;
}

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Content { get; set; }
    public int DayOfMonth { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
    public User User { get; set; } = null!;
}

public sealed class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? GoalId { get; set; }
    public long Amount { get; set; }
    public TransactionType Type { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Note { get; set; }
    public string? StoreName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Receipt? Receipt { get; set; }
    public Goal? Goal { get; set; }
}

public sealed class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSize { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.UPLOADED;
    public ReceiptClassification? Classification { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ReceiptImage Image { get; set; } = null!;
    public OcrResult? OcrResult { get; set; }
    public Transaction? Transaction { get; set; }
}

/// <summary>
/// The original receipt bytes are kept in PostgreSQL instead of a path to a
/// file outside the database. Keeping the blob in a separate table prevents
/// normal receipt/status queries from materializing up to 10 MB of image data.
/// </summary>
public sealed class ReceiptImage
{
    public Guid ReceiptId { get; set; }
    public required byte[] Data { get; set; }
    public Receipt Receipt { get; set; } = null!;
}

public sealed class OcrResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public required string RawText { get; set; }
    public required string LinesJson { get; set; }
    public string? StoreName { get; set; }
    public DateOnly? ReceiptDate { get; set; }
    public long? TotalAmount { get; set; }
    public long? VatAmount { get; set; }
    public decimal OverallConfidence { get; set; }
    public required string ModelVersion { get; set; }
    public required string ParserVersion { get; set; }
    public required string WarningsJson { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Receipt Receipt { get; set; } = null!;
}

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Scope { get; set; }
    public required string Key { get; set; }
    public required string RequestHash { get; set; }
    public int StatusCode { get; set; }
    public required string ResponseJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public User User { get; set; } = null!;
}
