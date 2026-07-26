using System.Globalization;
using System.Text;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public sealed record CategorySuggestion(
    Guid CategoryId,
    string CategoryName,
    decimal Confidence,
    string Reason);

public interface ICategorySuggestionService
{
    Task<CategorySuggestion?> SuggestAsync(
        Guid userId,
        OcrResult? ocrResult,
        CancellationToken cancellationToken);
}

/// <summary>
/// Suggests an expense category without relying on merchant-specific parsers.
/// Confirmed user history has priority; semantic receipt keywords are the fallback.
/// </summary>
public sealed class CategorySuggestionService(AppDbContext db) : ICategorySuggestionService
{
    private static readonly IReadOnlyDictionary<string, string[]> KeywordGroups =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AN UONG"] = ["TRA SUA", "CA PHE", "COFFEE", "DO UONG", "NUOC UONG", "PHO", "COM", "BUN", "BANH", "MON AN", "THUC AN", "RESTAURANT", "FOOD"],
            ["DI CHUYEN"] = ["XANG", "DAU DIESEL", "TAXI", "CUOC XE", "VE XE", "GUI XE", "PARKING", "CAU DUONG"],
            ["MUA SAM"] = ["SIEU THI", "QUAN AO", "MY PHAM", "GIA DUNG", "CUA HANG TIEN LOI", "SHOPPING"],
            ["NHA O"] = ["TIEN DIEN", "TIEN NUOC", "INTERNET", "THUE NHA", "CHUNG CU"],
            ["GIAI TRI"] = ["VE PHIM", "RAP CHIEU", "KARAOKE", "GAME", "GIAI TRI"],
            ["SUC KHOE"] = ["NHA THUOC", "THUOC", "DUOC PHAM", "BENH VIEN", "KHAM BENH"],
            ["GIAO DUC"] = ["HOC PHI", "KHOA HOC", "SACH", "VAN PHONG PHAM", "TRUONG HOC"]
        };

    public async Task<CategorySuggestion?> SuggestAsync(
        Guid userId,
        OcrResult? ocrResult,
        CancellationToken cancellationToken)
    {
        if (ocrResult is null)
            return null;

        var categories = await db.Categories.AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == TransactionType.EXPENSE)
            .ToListAsync(cancellationToken);
        if (categories.Count == 0)
            return null;

        var normalizedStore = Normalize(ocrResult.StoreName);
        if (normalizedStore.Length >= 3)
        {
            var history = await db.Transactions.AsNoTracking()
                .Where(x => x.UserId == userId
                    && x.Type == TransactionType.EXPENSE
                    && x.StoreName != null)
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.CreatedAt)
                .Take(500)
                .Select(x => new { x.StoreName, x.CategoryId })
                .ToListAsync(cancellationToken);

            var learned = history
                .Where(x => Normalize(x.StoreName) == normalizedStore)
                .GroupBy(x => x.CategoryId)
                .Select(x => new { CategoryId = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();
            var learnedCategory = learned is null
                ? null
                : categories.FirstOrDefault(x => x.Id == learned.CategoryId);
            if (learnedCategory is not null)
            {
                return new CategorySuggestion(
                    learnedCategory.Id,
                    learnedCategory.Name,
                    learned!.Count >= 2 ? 0.98m : 0.94m,
                    "Dựa trên danh mục bạn đã chọn cho cửa hàng này trước đây.");
            }
        }

        var content = Normalize($"{ocrResult.StoreName}\n{ocrResult.RawText}");
        var matches = KeywordGroups
            .Select(group => new
            {
                Family = group.Key,
                Score = group.Value.Count(keyword => content.Contains(keyword, StringComparison.Ordinal))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();
        if (matches.Count == 0 || (matches.Count > 1 && matches[0].Score == matches[1].Score))
            return null;

        var match = matches[0];
        var category = categories.FirstOrDefault(x => Normalize(x.Name) == match.Family);
        if (category is null)
            return null;

        return new CategorySuggestion(
            category.Id,
            category.Name,
            Math.Min(0.95m, 0.82m + (match.Score - 1) * 0.04m),
            "Gợi ý từ nội dung trên hóa đơn; vui lòng kiểm tra trước khi xác nhận.");
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            var normalized = char.IsLetterOrDigit(character) ? character : ' ';
            if (normalized == ' ')
            {
                if (previousWasSpace)
                    continue;
                previousWasSpace = true;
            }
            else
            {
                previousWasSpace = false;
            }
            result.Append(normalized);
        }
        return result.ToString().Trim();
    }
}
