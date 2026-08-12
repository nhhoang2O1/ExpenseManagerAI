using ExpenseManager.Api.Data;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public sealed record ReportFileResult(byte[] Content, string ContentType, string FileName);

public interface IReportsApplicationService
{
    Task<ApplicationServiceResult<ReportFileResult>> CreateExcelAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<ReportFileResult>> CreatePdfAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}

public sealed class ReportsApplicationService(
    AppDbContext db,
    IUserContext userContext,
    IExcelReportService excelReportService,
    IReportExportService reportExportService) : IReportsApplicationService
{
    public async Task<ApplicationServiceResult<ReportFileResult>> CreateExcelAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        if (!TryValidateRange(from, to, out var start, out var end, out var error))
            return ApplicationServiceResult<ReportFileResult>.BadRequest(error);
        var transactions = await LoadTransactions(start, end, cancellationToken);
        return ApplicationServiceResult<ReportFileResult>.Ok(new ReportFileResult(
            excelReportService.CreateRange(start, end, transactions),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bao-cao-{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx"));
    }

    public async Task<ApplicationServiceResult<ReportFileResult>> CreatePdfAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        if (!TryValidateRange(from, to, out var start, out var end, out var error))
            return ApplicationServiceResult<ReportFileResult>.BadRequest(error);
        var transactions = await LoadTransactions(start, end, cancellationToken);
        return ApplicationServiceResult<ReportFileResult>.Ok(new ReportFileResult(
            reportExportService.CreatePdf(start, end, transactions),
            "application/pdf",
            $"bao-cao-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf"));
    }

    private async Task<List<Domain.Transaction>> LoadTransactions(
        DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.Transactions.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == userContext.UserId &&
                        x.TransactionDate >= from && x.TransactionDate <= to)
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    private static bool TryValidateRange(DateOnly? from, DateOnly? to,
        out DateOnly start, out DateOnly end, out string error)
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
        if (start > end)
        {
            error = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.";
            return false;
        }
        if (end > LocalToday())
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
