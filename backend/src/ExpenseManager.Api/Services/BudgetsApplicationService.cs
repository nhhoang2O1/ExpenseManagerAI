using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface IBudgetsApplicationService
{
    Task<ApplicationServiceResult<IReadOnlyList<BudgetResponse>>> GetAllAsync(string? monthYear, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<BudgetResponse>> CreateOrUpdateAsync(BudgetRequest request, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<BudgetResponse>> UpdateAsync(Guid id, BudgetRequest request, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<object?>> DeleteAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
}

public sealed class BudgetsApplicationService(AppDbContext db, IUserContext userContext)
    : IBudgetsApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<BudgetResponse>>> GetAllAsync(
        string? monthYear, CancellationToken cancellationToken)
    {
        var query = db.Budgets.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId);
        if (!string.IsNullOrWhiteSpace(monthYear))
        {
            if (!BudgetRules.IsValidMonthYear(monthYear))
                return ApplicationServiceResult<IReadOnlyList<BudgetResponse>>.BadRequest(
                    "monthYear phai co dinh dang yyyy-MM.");
            query = query.Where(x => x.MonthYear == monthYear);
        }
        var items = await query.OrderBy(x => x.MonthYear).ThenBy(x => x.Category.Name)
            .ThenBy(x => x.Id).ToListAsync(cancellationToken);
        IReadOnlyList<BudgetResponse> result = items.Select(x => x.ToResponse()).ToList();
        return ApplicationServiceResult<IReadOnlyList<BudgetResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<BudgetResponse>> CreateOrUpdateAsync(
        BudgetRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (!BudgetRules.IsValidMonthYear(request.MonthYear))
            return ApplicationServiceResult<BudgetResponse>.BadRequest(
                "monthYear phai co dinh dang yyyy-MM.");
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, request.CategoryId, userContext.UserId, cancellationToken);
        if (category is null || !BudgetRules.CanUseCategory(category.Type))
            return ApplicationServiceResult<BudgetResponse>.BadRequest("Danh muc chi tieu khong hop le.");

        var budget = await db.Budgets.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.UserId == userContext.UserId && x.CategoryId == request.CategoryId &&
                 x.MonthYear == request.MonthYear, cancellationToken);
        var created = false;
        if (budget is null)
        {
            created = true;
            budget = new Budget
            {
                UserId = userContext.UserId, CategoryId = category.Id, Category = category,
                Amount = request.Amount, MonthYear = request.MonthYear
            };
            db.Budgets.Add(budget);
        }
        else
        {
            if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, budget.Version))
                return ApplicationServiceResult<BudgetResponse>.PreconditionFailed();
            budget.Amount = request.Amount;
            budget.Category = category;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<BudgetResponse>.PreconditionFailed();
        }
        catch (DbUpdateException)
        {
            return ApplicationServiceResult<BudgetResponse>.Conflict(
                "Ngân sách cho danh mục và tháng này đã tồn tại.");
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        var response = budget.ToResponse();
        return created
            ? ApplicationServiceResult<BudgetResponse>.Created(response, budget.Version)
            : ApplicationServiceResult<BudgetResponse>.Ok(response, budget.Version);
    }

    public async Task<ApplicationServiceResult<BudgetResponse>> UpdateAsync(
        Guid id, BudgetRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (!BudgetRules.IsValidMonthYear(request.MonthYear))
            return ApplicationServiceResult<BudgetResponse>.BadRequest(
                "monthYear phai co dinh dang yyyy-MM.");
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var budget = await db.Budgets.Include(x => x.Category).SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return ApplicationServiceResult<BudgetResponse>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, budget.Version))
            return ApplicationServiceResult<BudgetResponse>.PreconditionFailed();
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForReferenceAsync(
            db, request.CategoryId, userContext.UserId, cancellationToken);
        if (category is null || !BudgetRules.CanUseCategory(category.Type))
            return ApplicationServiceResult<BudgetResponse>.BadRequest("Danh muc chi tieu khong hop le.");
        var duplicate = await db.Budgets.AnyAsync(
            x => x.Id != id && x.UserId == userContext.UserId &&
                 x.CategoryId == request.CategoryId && x.MonthYear == request.MonthYear,
            cancellationToken);
        if (duplicate)
            return ApplicationServiceResult<BudgetResponse>.Conflict(
                "Ngan sach cho danh muc va thang nay da ton tai.");
        budget.CategoryId = category.Id;
        budget.Category = category;
        budget.Amount = request.Amount;
        budget.MonthYear = request.MonthYear;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<BudgetResponse>.PreconditionFailed();
        }
        catch (DbUpdateException)
        {
            return ApplicationServiceResult<BudgetResponse>.Conflict(
                "Ngân sách cho danh mục và tháng này đã tồn tại.");
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<BudgetResponse>.Ok(budget.ToResponse(), budget.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (budget is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, budget.Version))
            return ApplicationServiceResult<object?>.PreconditionFailed();
        db.Budgets.Remove(budget);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<object?>.PreconditionFailed();
        }
        return ApplicationServiceResult<object?>.NoContent();
    }
}
