using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseManager.Api.Infrastructure;

/// <summary>
/// Coordinates category type changes with creation/movement of transactions
/// and budgets. PostgreSQL row locks close the check-then-write race while the
/// InMemory provider used by unit tests keeps the simple LINQ path.
/// </summary>
internal static class FinanceDatabaseLocks
{
    public static async Task<IDbContextTransaction?> BeginIfPostgresAsync(
        AppDbContext db,
        CancellationToken cancellationToken) =>
        IsPostgres(db)
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

    public static Task<Category?> GetOwnedCategoryForReferenceAsync(
        AppDbContext db,
        Guid categoryId,
        Guid userId,
        CancellationToken cancellationToken) =>
        IsPostgres(db)
            ? db.Categories
                .FromSqlInterpolated(
                    $"SELECT * FROM categories WHERE id = {categoryId} AND user_id = {userId} FOR KEY SHARE")
                .SingleOrDefaultAsync(cancellationToken)
            : db.Categories.SingleOrDefaultAsync(
                x => x.Id == categoryId && x.UserId == userId,
                cancellationToken);

    public static Task<Category?> GetOwnedCategoryForMutationAsync(
        AppDbContext db,
        Guid categoryId,
        Guid userId,
        CancellationToken cancellationToken) =>
        IsPostgres(db)
            ? db.Categories
                .FromSqlInterpolated(
                    $"SELECT * FROM categories WHERE id = {categoryId} AND user_id = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : db.Categories.SingleOrDefaultAsync(
                x => x.Id == categoryId && x.UserId == userId,
                cancellationToken);

    /// <summary>
    /// Serializes balance reservations for one user. Locking only the goal row
    /// is insufficient because concurrent requests can fund different goals
    /// after reading the same available balance.
    /// </summary>
    public static async Task LockUserForGoalFundingAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!IsPostgres(db))
            return;

        _ = await db.Users
            .FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(cancellationToken);
    }

    private static bool IsPostgres(AppDbContext db) =>
        db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
}
