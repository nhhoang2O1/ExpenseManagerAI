using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Services;

public interface IExcelReportService
{
    byte[] CreateRange(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Domain.Transaction> transactions);
}

public sealed class ExcelReportService : IExcelReportService
{
    public byte[] CreateRange(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Domain.Transaction> transactions)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(archive, "[Content_Types].xml", ContentTypes);
            Add(archive, "_rels/.rels", PackageRelationships);
            Add(archive, "xl/workbook.xml", Workbook);
            Add(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
            Add(archive, "xl/styles.xml", Styles);
            Add(archive, "xl/worksheets/sheet1.xml", CreateSheet(from, to, transactions));
        }
        return output.ToArray();
    }

    private static string CreateSheet(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Domain.Transaction> transactions)
    {
        var xml = new StringBuilder();
        xml.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetViews><sheetView workbookViewId="0"/></sheetViews>
              <cols>
                <col min="1" max="1" width="13" customWidth="1"/>
                <col min="2" max="2" width="14" customWidth="1"/>
                <col min="3" max="3" width="22" customWidth="1"/>
                <col min="4" max="4" width="18" customWidth="1"/>
                <col min="5" max="5" width="22" customWidth="1"/>
                <col min="6" max="6" width="40" customWidth="1"/>
              </cols>
              <sheetData>
            """);
        xml.Append(Row(1, TextCell("A1", $"Bao cao thu chi tu {from:yyyy-MM-dd} den {to:yyyy-MM-dd}", 1)));
        xml.Append(Row(2,
            TextCell("A2", "Ngay", 1), TextCell("B2", "Loai", 1),
            TextCell("C2", "Danh muc", 1), TextCell("D2", "So tien (VND)", 1),
            TextCell("E2", "Cua hang", 1), TextCell("F2", "Ghi chu", 1)));

        var rowNumber = 3;
        foreach (var item in transactions)
        {
            xml.Append(Row(rowNumber,
                TextCell($"A{rowNumber}", item.TransactionDate.ToString("yyyy-MM-dd")),
                TextCell($"B{rowNumber}", item.Type == TransactionType.INCOME ? "Thu" : "Chi"),
                TextCell($"C{rowNumber}", item.Category.Name), NumberCell($"D{rowNumber}", item.Amount),
                TextCell($"E{rowNumber}", item.StoreName ?? string.Empty),
                TextCell($"F{rowNumber}", item.Note ?? string.Empty)));
            rowNumber++;
        }

        var income = transactions.Where(x => x.Type == TransactionType.INCOME).Sum(x => x.Amount);
        var expense = transactions.Where(x => x.Type == TransactionType.EXPENSE).Sum(x => x.Amount);
        rowNumber++;
        xml.Append(Row(rowNumber, TextCell($"C{rowNumber}", "Tong thu", 1), NumberCell($"D{rowNumber}", income)));
        rowNumber++;
        xml.Append(Row(rowNumber, TextCell($"C{rowNumber}", "Tong chi", 1), NumberCell($"D{rowNumber}", expense)));
        rowNumber++;
        xml.Append(Row(rowNumber, TextCell($"C{rowNumber}", "So du", 1), NumberCell($"D{rowNumber}", income - expense)));
        xml.Append("</sheetData><autoFilter ref=\"A2:F2\"/></worksheet>");
        return xml.ToString();
    }

    private static string Row(int number, params string[] cells) =>
        $"<row r=\"{number}\">{string.Concat(cells)}</row>";

    private static string TextCell(string reference, string value, int style = 0) =>
        $"<c r=\"{reference}\" t=\"inlineStr\" s=\"{style}\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>";

    private static string NumberCell(string reference, long value) =>
        $"<c r=\"{reference}\" s=\"2\"><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>";

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static void Add(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string PackageRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Giao dich" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1"><numFmt numFmtId="164" formatCode="#,##0"/></numFmts>
          <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
          <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
