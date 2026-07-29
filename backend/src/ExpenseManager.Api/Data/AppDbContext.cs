using System.Text;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalHistory> GoalHistories => Set<GoalHistory>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptImage> ReceiptImages => Set<ReceiptImage>();
    public DbSet<OcrResult> OcrResults => Set<OcrResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccountVerificationCode> AccountVerificationCodes => Set<AccountVerificationCode>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.IsEmailVerified).HasDefaultValue(true);
            entity.HasIndex(x => x.TokenVersion);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Name, x.Type }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Color).HasMaxLength(20);
            entity.Property(x => x.Icon).HasMaxLength(50);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany(x => x.Categories)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(x => x.ReceiptId).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.TransactionDate });
            entity.HasIndex(x => new { x.UserId, x.TransactionDate, x.CreatedAt, x.Id });
            entity.Property(x => x.Amount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.StoreName).HasMaxLength(200);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany(x => x.Transactions)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Receipt).WithOne(x => x.Transaction)
                .HasForeignKey<Transaction>(x => x.ReceiptId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.CategoryId, x.MonthYear }).IsUnique();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.MonthYear).HasMaxLength(7);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany(x => x.Budgets)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany(x => x.Budgets)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.TargetAmount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.CurrentAmount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany(x => x.Goals)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalHistory>(entity =>
        {
            entity.HasIndex(x => new { x.GoalId, x.Date });
            entity.Property(x => x.AmountAdded).HasColumnType("numeric(18,0)");
            entity.Property(x => x.RequestedAmount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.BalanceAfter).HasColumnType("numeric(18,0)");
            entity.HasOne(x => x.Goal).WithMany(x => x.History)
                .HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Content).HasMaxLength(500);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany(x => x.Reminders)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Classification).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.ProcessingAttempts).HasDefaultValue(0);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.Version).HasColumnType("bigint").IsConcurrencyToken();
            entity.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseExpiresAt, x.CreatedAt });
            entity.HasOne(x => x.User).WithMany(x => x.Receipts)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceiptImage>(entity =>
        {
            entity.HasKey(x => x.ReceiptId);
            entity.Property(x => x.Data).HasColumnType("bytea").IsRequired();
            entity.HasOne(x => x.Receipt).WithOne(x => x.Image)
                .HasForeignKey<ReceiptImage>(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OcrResult>(entity =>
        {
            entity.HasIndex(x => x.ReceiptId).IsUnique();
            entity.Property(x => x.LinesJson).HasColumnType("jsonb");
            entity.Property(x => x.WarningsJson).HasColumnType("jsonb");
            entity.Property(x => x.StoreName).HasMaxLength(200);
            entity.Property(x => x.OverallConfidence).HasPrecision(8, 6);
            entity.Property(x => x.TotalAmount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.VatAmount).HasColumnType("numeric(18,0)");
            entity.Property(x => x.ModelVersion).HasMaxLength(100);
            entity.Property(x => x.ParserVersion).HasMaxLength(100);
            entity.HasOne(x => x.Receipt).WithOne(x => x.OcrResult)
                .HasForeignKey<OcrResult>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ConfigureAuthSecurity();

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Scope, x.Key }).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
            entity.Property(x => x.Scope).HasMaxLength(100);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.Property(x => x.RequestHash).HasMaxLength(128);
            entity.Property(x => x.ResponseJson).HasColumnType("jsonb");
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        ApplySnakeCaseNames(modelBuilder);
    }

    public override int SaveChanges()
    {
        TouchUpdatedEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchUpdatedEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void TouchUpdatedEntities()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Transaction>()
                     .Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
        foreach (var entry in ChangeTracker.Entries<Receipt>()
                     .Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
        foreach (var entry in ChangeTracker.Entries<Budget>()
                     .Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
        foreach (var entry in ChangeTracker.Entries<Goal>()
                     .Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
        foreach (var entry in ChangeTracker.Entries<Reminder>()
                     .Where(x => x.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;

        // Every mutable aggregate carries a monotonically increasing version.
        // Controllers may expose it as an ETag/If-Match value; EF also uses it
        // as a concurrency token so a stale write cannot silently overwrite a
        // newer change.
        foreach (var entry in ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Modified))
        {
            var version = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "Version");
            if (version is not null)
                version.CurrentValue = Convert.ToInt64(version.OriginalValue) + 1L;
        }
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.ClrType.Name) switch
            {
                "user" => "users",
                "category" => "categories",
                "budget" => "budgets",
                "goal" => "goals",
                "goal_history" => "goal_histories",
                "reminder" => "reminders",
                "transaction" => "transactions",
                "receipt" => "receipts",
                "receipt_image" => "receipt_images",
                "ocr_result" => "ocr_results",
                "refresh_token" => "refresh_tokens",
                "account_verification_code" => "account_verification_codes",
                "idempotency_record" => "idempotency_records",
                var name => name
            });
        }

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
            foreach (var key in entity.GetKeys())
                key.SetName($"pk_{entity.GetTableName()}");
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName($"ix_{entity.GetTableName()}_{string.Join('_', index.Properties.Select(x => ToSnakeCase(x.Name)))}");
            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName($"fk_{entity.GetTableName()}_{foreignKey.PrincipalEntityType.GetTableName()}_{string.Join('_', foreignKey.Properties.Select(x => ToSnakeCase(x.Name)))}");
        }
    }

    private static string ToSnakeCase(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && i > 0)
                result.Append('_');
            result.Append(char.ToLowerInvariant(value[i]));
        }
        return result.ToString();
    }
}
