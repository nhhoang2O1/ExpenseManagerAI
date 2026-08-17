using System.Globalization;
using System.Text;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Services;

public interface IReportExportService
{
    byte[] CreatePdf(DateOnly from, DateOnly to, IReadOnlyList<Domain.Transaction> transactions);
}

public sealed class ReportExportService : IReportExportService
{
    public byte[] CreatePdf(DateOnly from, DateOnly to, IReadOnlyList<Domain.Transaction> transactions)
    {
        var lines = new List<string>
        {
            $"Bao cao thu chi - {from:yyyy-MM-dd} den {to:yyyy-MM-dd}"
        };

        foreach (var month in transactions
                     .GroupBy(x => new { x.TransactionDate.Year, x.TransactionDate.Month })
                     .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month))
        {
            lines.Add($"THANG {month.Key.Year}-{month.Key.Month:00}");
            lines.Add("Ngay | Loai | Danh muc | So tien (VND) | Cua hang");
            lines.Add("------------------------------------------------------------");
            lines.AddRange(month.Select(x =>
                $"{x.TransactionDate:yyyy-MM-dd} | {(x.Type == TransactionType.INCOME ? "THU" : "CHI")} | " +
                $"{x.Category.Name} | {x.Amount.ToString("N0", CultureInfo.InvariantCulture)} | {x.StoreName ?? ""}"));
            var income = month.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount);
            var expense = month.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount);
            lines.Add($"Tong thu thang: {income.ToString("N0", CultureInfo.InvariantCulture)} VND");
            lines.Add($"Tong chi thang: {expense.ToString("N0", CultureInfo.InvariantCulture)} VND");
            lines.Add($"Con lai trong thang: {(income - expense).ToString("N0", CultureInfo.InvariantCulture)} VND");
            lines.Add(string.Empty);
        }

        var totalIncome = transactions.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount);
        var totalExpense = transactions.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount);
        lines.Add("TONG CONG");
        lines.Add($"Tong thu: {totalIncome.ToString("N0", CultureInfo.InvariantCulture)} VND");
        lines.Add($"Tong chi: {totalExpense.ToString("N0", CultureInfo.InvariantCulture)} VND");
        lines.Add($"Con lai: {(totalIncome - totalExpense).ToString("N0", CultureInfo.InvariantCulture)} VND");

        const int linesPerPage = 50;
        var pages = new List<List<string>>();
        var page = NewPageHeader(from, to);
        foreach (var line in lines.Skip(1))
        {
            if (page.Count >= linesPerPage)
            {
                pages.Add(page);
                page = NewPageHeader(from, to);
            }
            page.Add(line);
        }
        if (page.Count > 1 || pages.Count == 0) pages.Add(page);
        return BuildPdf(pages);
    }

    private static List<string> NewPageHeader(DateOnly from, DateOnly to) =>
        [$"Bao cao thu chi - {from:yyyy-MM-dd} den {to:yyyy-MM-dd}"];

    private static byte[] BuildPdf(IReadOnlyList<List<string>> pages)
    {
        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        WriteObject(output, offsets, "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");
        var pageReferences = string.Join(" ", pages.Select((_, index) => $"{4 + index * 2} 0 R"));
        WriteObject(output, offsets, $"2 0 obj<< /Type /Pages /Kids [{pageReferences}] /Count {pages.Count} >>endobj\n");
        WriteObject(output, offsets, "3 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");

        for (var index = 0; index < pages.Count; index++)
        {
            var pageId = 4 + index * 2;
            var contentId = pageId + 1;
            WriteObject(output, offsets,
                $"{pageId} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>endobj\n");
            var content = new StringBuilder("BT\n/F1 9 Tf\n50 800 Td\n");
            foreach (var line in pages[index])
                content.Append('(').Append(EscapePdf(line)).Append(") Tj\n0 -14 Td\n");
            content.Append("ET\n");
            var stream = Encoding.ASCII.GetBytes(content.ToString());
            offsets.Add(output.Position);
            WriteAscii(output, $"{contentId} 0 obj<< /Length {stream.Length} >>stream\n");
            output.Write(stream, 0, stream.Length);
            WriteAscii(output, "\nendstream\nendobj\n");
        }

        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output, $"trailer<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static string EscapePdf(string value)
    {
        var ascii = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            ascii.Append(character switch { 'đ' => 'd', 'Đ' => 'D', _ => character });
        }
        return ascii.ToString().Normalize(NormalizationForm.FormC)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Where(character => character <= 127)
            .Aggregate(new StringBuilder(), (builder, character) => builder.Append(character))
            .ToString();
    }

    private static void WriteObject(Stream output, List<long> offsets, string value)
    {
        offsets.Add(output.Position);
        WriteAscii(output, value);
    }

    private static void WriteAscii(Stream output, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }
}
