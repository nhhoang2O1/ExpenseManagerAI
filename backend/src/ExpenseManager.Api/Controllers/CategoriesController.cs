using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(AppDbContext db, IUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await db.Categories.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderBy(x => x.Type).ThenBy(x => x.Name)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Type, x.Color, x.Icon, x.Version))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        CategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!CategoryRules.IsSupportedType(request.Type))
            return BadRequest(new { message = "Loại danh mục không hợp lệ." });

        var name = CategoryRules.NormalizeName(request.Name);
        if (name.Length == 0)
            return BadRequest(new { message = "Tên danh mục không được để trống." });

        var duplicate = await db.Categories.AnyAsync(
            x => x.UserId == userContext.UserId && x.Name == name && x.Type == request.Type,
            cancellationToken);
        if (duplicate)
            return Conflict(new { message = "Danh mục đã tồn tại." });

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
            return Conflict(new { message = "Danh mục đã tồn tại." });
        }
        OptimisticConcurrency.WriteEtag(this, category.Version);
        return CreatedAtAction(nameof(GetAll), category.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        CategoryRequest request,
        CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForMutationAsync(
            db, id, userContext.UserId, cancellationToken);
        if (category is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, category.Version))
            return OptimisticConcurrency.PreconditionFailed(this);

        if (!CategoryRules.IsSupportedType(request.Type))
            return BadRequest(new { message = "Loại danh mục không hợp lệ." });

        var name = CategoryRules.NormalizeName(request.Name);
        if (name.Length == 0)
            return BadRequest(new { message = "Tên danh mục không được để trống." });

        if (category.Type != request.Type)
        {
            var isReferenced = await db.Transactions.AnyAsync(
                    x => x.CategoryId == id && x.UserId == userContext.UserId,
                    cancellationToken) ||
                await db.Budgets.AnyAsync(
                    x => x.CategoryId == id && x.UserId == userContext.UserId,
                    cancellationToken);
            if (isReferenced)
                return Conflict(new
                {
                    message = "Không thể đổi loại danh mục đang được dùng bởi giao dịch hoặc ngân sách."
                });
        }

        var duplicate = await db.Categories.AnyAsync(
            x => x.UserId == userContext.UserId && x.Id != id &&
                 x.Name == name && x.Type == request.Type,
            cancellationToken);
        if (duplicate)
            return Conflict(new { message = "Danh mục đã tồn tại." });

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
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Danh mục đã tồn tại." });
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        OptimisticConcurrency.WriteEtag(this, category.Version);
        return Ok(category.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var category = await FinanceDatabaseLocks.GetOwnedCategoryForMutationAsync(
            db, id, userContext.UserId, cancellationToken);
        if (category is null)
            return NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(this, category.Version))
            return OptimisticConcurrency.PreconditionFailed(this);
        var hasTransactions = await db.Transactions.AnyAsync(
            x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken);
        var hasBudgets = await db.Budgets.AnyAsync(
            x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken);
        if (hasTransactions || hasBudgets)
            return Conflict(new
            {
                message = "Không thể xóa danh mục đang được dùng bởi giao dịch hoặc ngân sách."
            });

        db.Categories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OptimisticConcurrency.PreconditionFailed(this);
        }
        catch (DbUpdateException)
        {
            // A transaction or budget may have started referencing the category
            // after the checks above. The database FK remains the final guard.
            return Conflict(new
            {
                message = "Không thể xóa danh mục đang được dùng bởi giao dịch hoặc ngân sách."
            });
        }
        if (databaseTransaction is not null)
            await databaseTransaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
