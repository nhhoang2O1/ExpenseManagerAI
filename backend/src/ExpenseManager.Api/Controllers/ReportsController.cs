using ExpenseManager.Api.Data;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(
    AppDbContext db,
    IUserContext userContext,
    IExcelReportService excelReportService,
    IReportExportService reportExportService) : ControllerBase
{
    [HttpGet("monthly.xlsx")]
    public async Task<IActionResult> Monthly(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { message = "year hoặc month không hợp lệ." });

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var transactions = await db.Transactions.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId &&
                        x.TransactionDate >= start && x.TransactionDate < end)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var bytes = excelReportService.CreateMonthly(year, month, transactions);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bao-cao-{year}-{month:00}.xlsx");
    }

    [HttpGet("monthly.csv")]
    public Task<IActionResult> MonthlyCsv(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken) =>
        ExportTextAsync(year, month, "csv", cancellationToken);

    [HttpGet("monthly.pdf")]
    public Task<IActionResult> MonthlyPdf(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken) =>
        ExportTextAsync(year, month, "pdf", cancellationToken);

    private async Task<IActionResult> ExportTextAsync(
        int year,
        int month,
        string format,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { message = "year or month is invalid." });

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var transactions = await db.Transactions.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId &&
                        x.TransactionDate >= start && x.TransactionDate < end)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (format == "csv")
            return File(
                reportExportService.CreateCsv(year, month, transactions),
                "text/csv; charset=utf-8",
                $"bao-cao-{year}-{month:00}.csv");

        return File(
            reportExportService.CreatePdf(year, month, transactions),
            "application/pdf",
            $"bao-cao-{year}-{month:00}.pdf");
    }
}
