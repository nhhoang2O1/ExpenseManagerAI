using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(ICategoriesApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        this.ToActionResult(await service.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.StatusCode == StatusCodes.Status201Created
            ? CreatedAtAction(nameof(GetAll), result.Value)
            : this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id, CategoryRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.UpdateAsync(
            id, request, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.DeleteAsync(
            id, ControllerContext.HttpContext?.Request.Headers["If-Match"].ToString(), cancellationToken)).Result!;
}
