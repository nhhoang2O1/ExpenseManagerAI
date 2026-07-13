using System.IO.Compression;
using System.Xml.Linq;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;

namespace ExpenseManager.Api.Tests;

public sealed class ExcelReportTests
{
    [Fact]
    public void Monthly_report_contains_required_openxml_parts_and_parseable_xml()
    {
        var user = new User
        {
            Name = "Report User",
            Email = "report@example.com",
            PasswordHash = "hash"
        };
        var category = new Category
        {
            UserId = user.Id,
            User = user,
            Name = "Food",
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
            TransactionDate = new DateOnly(2026, 7, 9),
            StoreName = "Circle K"
        };

        var bytes = new ExcelReportService().CreateMonthly(2026, 7, [transaction]);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var requiredEntries = new[]
        {
            "[Content_Types].xml",
            "_rels/.rels",
            "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels",
            "xl/styles.xml",
            "xl/worksheets/sheet1.xml"
        };

        foreach (var path in requiredEntries)
        {
            var entry = archive.GetEntry(path);
            Assert.NotNull(entry);
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            Assert.NotNull(document.Root);
        }
    }
}
