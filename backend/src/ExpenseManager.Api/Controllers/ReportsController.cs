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
    [HttpGet("range.xlsx")]
    public async Task<IActionResult> RangeXlsx(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryValidateRange(from, to, out var start, out var end, out var error))
            return BadRequest(new { message = error });

        var transactions = await LoadTransactions(start, end, cancellationToken);
        var bytes = excelReportService.CreateRange(start, end, transactions);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bao-cao-{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx");
    }

    [HttpGet("range.pdf")]
    public async Task<IActionResult> RangePdf(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryValidateRange(from, to, out var start, out var end, out var error))
            return BadRequest(new { message = error });

        var transactions = await LoadTransactions(start, end, cancellationToken);
        return File(
            reportExportService.CreatePdf(start, end, transactions),
            "application/pdf",
            $"bao-cao-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
    }

    private async Task<List<Domain.Transaction>> LoadTransactions(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        await db.Transactions.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId &&
                        x.TransactionDate >= from && x.TransactionDate <= to)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    private static bool TryValidateRange(
        DateOnly? from,
        DateOnly? to,
        out DateOnly start,
        out DateOnly end,
        out string error)
    {
        start = default;
        end = default;
        error = string.Empty;

        if (!from.HasValue || !to.HasValue)
        {
            error = "from và to là bắt buộc.";
            return false;
        }

        start = from.Value;
        end = to.Value;
        var today = LocalToday();
        if (start > end)
        {
            error = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.";
            return false;
        }

        if (end > today)
        {
            error = "Không thể xuất báo cáo quá ngày hiện tại.";
            return false;
        }

        return true;
    }

    private static DateOnly LocalToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }
}
