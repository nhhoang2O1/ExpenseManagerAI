using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface ICategoriesApplicationService
{
    Task<ApplicationServiceResult<IReadOnlyList<CategoryResponse>>> GetAllAsync(CancellationToken cancellationToken);
    Task<ApplicationServiceResult<CategoryResponse>> CreateAsync(CategoryRequest request, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<CategoryResponse>> UpdateAsync(Guid id, CategoryRequest request, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<object?>> DeleteAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
}

public sealed class CategoriesApplicationService(AppDbContext db, IUserContext userContext)
    : ICategoriesApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<CategoryResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var items = await db.Categories.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderBy(x => x.Type).ThenBy(x => x.Name)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Type, x.Color, x.Icon, x.Version))
            .ToListAsync(cancellationToken);
        IReadOnlyList<CategoryResponse> result = items;
        return ApplicationServiceResult<IReadOnlyList<CategoryResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<CategoryResponse>> CreateAsync(
        CategoryRequest request, CancellationToken cancellationToken)
    {
        if (!CategoryRules.IsSupportedType(request.Type))
            return ApplicationServiceResult<CategoryResponse>.BadRequest("Loại danh mục không hợp lệ.");
        var name = CategoryRules.NormalizeName(request.Name);
        if (name.Length == 0)
            return ApplicationServiceResult<CategoryResponse>.BadRequest("Tên danh mục không được để trống.");
        var duplicate = await db.Categories.AnyAsync(
            x => x.UserId == userContext.UserId && x.Name == name && x.Type == request.Type,
            cancellationToken);
        if (duplicate)
            return ApplicationServiceResult<CategoryResponse>.Conflict("Danh mục đã tồn tại.");

        var category = new Category
        {
            UserId = userContext.UserId,
            Name = name,
            Type = request.Type,
            Color = CategoryRules.NormalizeOptionalText(request.Color),
            Icon = CategoryRules.NormalizeOptionalText(request.Icon)
        };
        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApplicationServiceResult<CategoryResponse>.Conflict("Danh mục đã tồn tại.");
        }
        return ApplicationServiceResult<CategoryResponse>.Created(category.ToResponse(), category.Version);
    }

    public async Task<ApplicationServiceResult<CategoryResponse>> UpdateAsync(
        Guid id, CategoryRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForMutationAsync(
            db, id, userContext.UserId, cancellationToken);
        if (category is null)
            return ApplicationServiceResult<CategoryResponse>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, category.Version))
            return ApplicationServiceResult<CategoryResponse>.PreconditionFailed();
        if (!CategoryRules.IsSupportedType(request.Type))
            return ApplicationServiceResult<CategoryResponse>.BadRequest("Loại danh mục không hợp lệ.");
        var name = CategoryRules.NormalizeName(request.Name);
        if (name.Length == 0)
            return ApplicationServiceResult<CategoryResponse>.BadRequest("Tên danh mục không được để trống.");

        if (category.Type != request.Type)
        {
            var isReferenced = await db.Transactions.AnyAsync(
                    x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken) ||
                await db.Budgets.AnyAsync(
                    x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken);
            if (isReferenced)
                return ApplicationServiceResult<CategoryResponse>.Conflict(
                    "Không thể đổi loại danh mục đang được dùng bởi giao dịch hoặc ngân sách.");
        }
        var duplicate = await db.Categories.AnyAsync(
            x => x.UserId == userContext.UserId && x.Id != id &&
                 x.Name == name && x.Type == request.Type, cancellationToken);
        if (duplicate)
            return ApplicationServiceResult<CategoryResponse>.Conflict("Danh mục đã tồn tại.");

        category.Name = name;
        category.Type = request.Type;
        category.Color = CategoryRules.NormalizeOptionalText(request.Color);
        category.Icon = CategoryRules.NormalizeOptionalText(request.Icon);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<CategoryResponse>.PreconditionFailed();
        }
        catch (DbUpdateException)
        {
            return ApplicationServiceResult<CategoryResponse>.Conflict("Danh mục đã tồn tại.");
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<CategoryResponse>.Ok(category.ToResponse(), category.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForMutationAsync(
            db, id, userContext.UserId, cancellationToken);
        if (category is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, category.Version))
            return ApplicationServiceResult<object?>.PreconditionFailed();
        var hasTransactions = await db.Transactions.AnyAsync(
            x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken);
        var hasBudgets = await db.Budgets.AnyAsync(
            x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken);
        if (hasTransactions || hasBudgets)
            return ApplicationServiceResult<object?>.Conflict(
                "Không thể xóa danh mục đang được dùng bởi giao dịch hoặc ngân sách.");

        db.Categories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<object?>.PreconditionFailed();
        }
        catch (DbUpdateException)
        {
            return ApplicationServiceResult<object?>.Conflict(
                "Không thể xóa danh mục đang được dùng bởi giao dịch hoặc ngân sách.");
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<object?>.NoContent();
    }
}
