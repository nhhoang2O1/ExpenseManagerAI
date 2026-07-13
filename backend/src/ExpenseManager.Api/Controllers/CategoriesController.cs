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
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Type, x.Color, x.Icon))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
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
            Color = request.Color?.Trim(),
            Icon = request.Icon?.Trim()
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), category.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (category is null)
            return NotFound();

        var name = request.Name.Trim();
        var duplicate = await db.Categories.AnyAsync(
            x => x.UserId == userContext.UserId && x.Id != id &&
                 x.Name == name && x.Type == request.Type,
            cancellationToken);
        if (duplicate)
            return Conflict(new { message = "Danh mục đã tồn tại." });

        category.Name = name;
        category.Type = request.Type;
        category.Color = request.Color?.Trim();
        category.Icon = request.Icon?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(category.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (category is null)
            return NotFound();
        if (await db.Transactions.AnyAsync(
                x => x.CategoryId == id && x.UserId == userContext.UserId, cancellationToken))
            return Conflict(new { message = "Không thể xóa danh mục đang có giao dịch." });

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
