using System.Globalization;
using System.Text;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Services;

public interface IReportExportService
{
    byte[] CreateCsv(int year, int month, IReadOnlyList<Domain.Transaction> transactions);
    byte[] CreatePdf(int year, int month, IReadOnlyList<Domain.Transaction> transactions);
}

/// <summary>
/// Dependency-free CSV/PDF exports. XLSX remains implemented by
/// ExcelReportService; these formats intentionally share the same ordered
/// transaction query in ReportsController.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    public byte[] CreateCsv(int year, int month, IReadOnlyList<Domain.Transaction> transactions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("date,type,category,amount_vnd,store,note");
        foreach (var item in transactions)
        {
            AppendCsvRow(builder,
                item.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Type == TransactionType.INCOME ? "income" : "expense",
                item.Category.Name,
                item.Amount.ToString(CultureInfo.InvariantCulture),
                item.StoreName ?? string.Empty,
                item.Note ?? string.Empty);
        }

        var income = transactions.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount);
        var expense = transactions.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount);
        AppendCsvRow(builder, string.Empty, "total_income", string.Empty,
            income.ToString(CultureInfo.InvariantCulture), string.Empty, string.Empty);
        AppendCsvRow(builder, string.Empty, "total_expense", string.Empty,
            expense.ToString(CultureInfo.InvariantCulture), string.Empty, string.Empty);
        AppendCsvRow(builder, string.Empty, "balance", string.Empty,
            (income - expense).ToString(CultureInfo.InvariantCulture), string.Empty, string.Empty);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(builder.ToString());
    }

    public byte[] CreatePdf(int year, int month, IReadOnlyList<Domain.Transaction> transactions)
    {
        var lines = new List<string>
        {
            $"Expense Manager - {year}-{month:00}",
            "Date | Type | Category | Amount (VND) | Store",
            "------------------------------------------------------------"
        };
        lines.AddRange(transactions.Select(x =>
            $"{x.TransactionDate:yyyy-MM-dd} | {(x.Type == TransactionType.INCOME ? "IN" : "OUT")} | " +
            $"{x.Category.Name} | {x.Amount.ToString("N0", CultureInfo.InvariantCulture)} | {x.StoreName ?? ""}"));
        var income = transactions.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount);
        var expense = transactions.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount);
        lines.Add($"Income: {income.ToString("N0", CultureInfo.InvariantCulture)} VND");
        lines.Add($"Expense: {expense.ToString("N0", CultureInfo.InvariantCulture)} VND");
        lines.Add($"Balance: {(income - expense).ToString("N0", CultureInfo.InvariantCulture)} VND");

        // A small valid PDF writer is sufficient for an export artifact and
        // avoids adding a native/PDF dependency to the API container.
        var content = new StringBuilder("BT\n/F1 9 Tf\n50 800 Td\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdf(line)).Append(") Tj\n0 -14 Td\n");
        }
        content.Append("ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());

        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        var offsets = new List<long> { 0 };
        WriteObject(output, offsets, "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");
        WriteObject(output, offsets, "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n");
        WriteObject(output, offsets, "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>endobj\n");
        WriteObject(output, offsets, "4 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");
        offsets.Add(output.Position);
        writer.Write($"5 0 obj<< /Length {stream.Length} >>stream\n");
        writer.Flush();
        output.Write(stream, 0, stream.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();
        var xref = output.Position;
        writer.Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            writer.Write($"{offset:0000000000} 00000 n \n");
        writer.Write($"trailer<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        writer.Flush();
        return output.ToArray();
    }

    private static void AppendCsvRow(StringBuilder builder, params string[] values) =>
        builder.AppendLine(string.Join(',', values.Select(EscapeCsv)));

    private static string EscapeCsv(string value) =>
        value.Contains(',', StringComparison.Ordinal) || value.Contains('"') ||
        value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static string EscapePdf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Where(c => c <= 127)
            .Aggregate(new StringBuilder(), (builder, c) => builder.Append(c))
            .ToString();

    private static void WriteObject(Stream output, List<long> offsets, string value)
    {
        offsets.Add(output.Position);
        var bytes = Encoding.ASCII.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }
}
