using System.Text;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;

namespace ExpenseManager.Api.Tests;

public sealed class PdfReportTests
{
    [Fact]
    public void Range_report_is_a_complete_pdf_and_can_emit_a_qa_sample()
    {
        var user = new User
        {
            Name = "Người dùng báo cáo",
            Email = "pdf-report@example.com",
            PasswordHash = "hash"
        };
        var category = new Category
        {
            UserId = user.Id,
            User = user,
            Name = "Ăn uống",
            Type = TransactionType.EXPENSE
        };
        var transaction = new Domain.Transaction
        {
            UserId = user.Id,
            User = user,
            CategoryId = category.Id,
            Category = category,
            Amount = 125_000,
            Type = TransactionType.EXPENSE,
            TransactionDate = new DateOnly(2026, 8, 16),
            StoreName = "Cửa hàng Việt",
            Note = "Bữa sáng"
        };

        var bytes = new ReportExportService().CreatePdf(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 16),
            [transaction]);
        var ascii = Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", ascii);
        Assert.Contains("xref", ascii);
        Assert.EndsWith("%%EOF", ascii);
        Assert.Contains("125,000", ascii);
        Assert.Contains("An uong", ascii);
        Assert.Contains("Cua hang Viet", ascii);

        var samplePath = Environment.GetEnvironmentVariable("REPORT_PDF_SAMPLE_PATH");
        if (!string.IsNullOrWhiteSpace(samplePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
            File.WriteAllBytes(samplePath, bytes);
        }
    }
}
