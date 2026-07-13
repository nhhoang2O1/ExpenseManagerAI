namespace ExpenseManager.Api.Domain;

public enum TransactionType
{
    INCOME,
    EXPENSE
}

public enum ReceiptStatus
{
    UPLOADED,
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Receipt> Receipts { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<Goal> Goals { get; set; } = [];
    public ICollection<Reminder> Reminders { get; set; } = [];
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ICollection<GoalHistory> History { get; set; } = [];
}

public sealed class GoalHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public long AmountAdded { get; set; }
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
    public User User { get; set; } = null!;
}

public sealed class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? ReceiptId { get; set; }
    public long Amount { get; set; }
    public TransactionType Type { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Note { get; set; }
    public string? StoreName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Receipt? Receipt { get; set; }
}

public sealed class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.UPLOADED;
    public ReceiptClassification? Classification { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public OcrResult? OcrResult { get; set; }
    public Transaction? Transaction { get; set; }
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
