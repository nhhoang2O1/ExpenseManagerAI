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

    private static bool IsPostgres(AppDbContext db) =>
        db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
}
