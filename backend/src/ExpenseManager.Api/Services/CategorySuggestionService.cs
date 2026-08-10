using System.Text.Json;
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
/// Maps the explainable semantic decision made by CategoryClassifier to an
/// expense category owned by the current user. The HTTP contract remains the
/// same as V1; medium/low-confidence decisions are not auto-selected because
/// the current Android client treats every returned id as authoritative.
/// </summary>
public sealed class CategorySuggestionService : ICategorySuggestionService
{
    private readonly AppDbContext _db;
    private readonly CategoryTextNormalizer _normalizer;
    private readonly UserCategoryMapper _mapper;
    private readonly CategoryHistoryScorer _historyScorer;
    private readonly CategoryClassifier _classifier;

    public CategorySuggestionService(AppDbContext db)
    {
        _db = db;
        _normalizer = new CategoryTextNormalizer();
        _mapper = new UserCategoryMapper(_normalizer);
        _historyScorer = new CategoryHistoryScorer();
        _classifier = new CategoryClassifier(_normalizer, new CategoryRuleSet());
    }

    public async Task<CategorySuggestion?> SuggestAsync(
        Guid userId,
        OcrResult? ocrResult,
        CancellationToken cancellationToken)
    {
        if (ocrResult is null)
            return null;

        var prepared = await AnalyzeCoreAsync(userId, ocrResult, cancellationToken);
        var decision = prepared.Analysis.Decision;
        if (!decision.Accepted || decision.SemanticCategory is null ||
            decision.ConfidenceTier != CategoryConfidenceTier.HIGH)
            return null;

        var category = _mapper.FindCategory(
            decision.SemanticCategory.Value,
            prepared.Categories);
        if (category is null)
            return null;

        var winningCandidate = prepared.Analysis.Candidates
            .Single(x => x.Category == decision.SemanticCategory.Value);
        var reason = string.Join(
            " ",
            winningCandidate.Evidence
                .Where(x => x.Contribution > 0)
                .OrderByDescending(x => x.Contribution)
                .Take(2)
                .Select(x => x.Reason));
        if (string.IsNullOrWhiteSpace(reason))
            reason = "Gợi ý từ bằng chứng trên hóa đơn; vui lòng kiểm tra trước khi xác nhận.";

        return new CategorySuggestion(
            category.Id,
            category.Name,
            decision.HeuristicConfidence,
            reason);
    }

    /// <summary>
    /// Internal-debug friendly analysis used by unit/regression tests. It is
    /// not exposed by any controller or HTTP response.
    /// </summary>
    public async Task<CategoryAnalysis?> AnalyzeAsync(
        Guid userId,
        OcrResult? ocrResult,
        CancellationToken cancellationToken)
    {
        if (ocrResult is null)
            return null;
        return (await AnalyzeCoreAsync(userId, ocrResult, cancellationToken)).Analysis;
    }

    private async Task<PreparedAnalysis> AnalyzeCoreAsync(
        Guid userId,
        OcrResult ocrResult,
        CancellationToken cancellationToken)
    {
        var categories = await _db.Categories.AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == TransactionType.EXPENSE)
            .ToListAsync(cancellationToken);
        var historyEvidence = await HistoryEvidenceAsync(
            userId,
            ocrResult,
            categories,
            cancellationToken);
        var lines = ReadLines(ocrResult.LinesJson, ocrResult.RawText, ocrResult.OverallConfidence);
        var analysis = _classifier.Analyze(new CategoryClassifierInput(
            ocrResult.StoreName,
            ocrResult.RawText,
            lines,
            historyEvidence,
            ocrResult.OverallConfidence));
        return new PreparedAnalysis(analysis, categories);
    }

    private async Task<IReadOnlyList<CategoryEvidence>> HistoryEvidenceAsync(
        Guid userId,
        OcrResult ocrResult,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var merchant = _normalizer.Normalize(ocrResult.StoreName);
        if (merchant.Length < 3)
            return [];

        var categoryById = categories.ToDictionary(x => x.Id);
        var history = await _db.Transactions.AsNoTracking()
            .Where(x => x.UserId == userId &&
                x.Type == TransactionType.EXPENSE &&
                x.StoreName != null &&
                x.ReceiptId != ocrResult.ReceiptId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new { x.StoreName, x.CategoryId })
            .ToListAsync(cancellationToken);
        var semanticHistory = history
            .Where(x => _normalizer.Normalize(x.StoreName) == merchant)
            .Select(x => categoryById.TryGetValue(x.CategoryId, out var category)
                ? _mapper.ToSemantic(category)
                : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        return _historyScorer.Score(semanticHistory);
    }

    private static IReadOnlyList<CategoryOcrLine> ReadLines(
        string linesJson,
        string rawText,
        decimal overallConfidence)
    {
        try
        {
            using var document = JsonDocument.Parse(linesJson);
            var result = new List<CategoryOcrLine>();
            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("text", out var textProperty))
                    continue;
                var text = textProperty.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var confidence = item.TryGetProperty("confidence", out var confidenceProperty) &&
                                 confidenceProperty.TryGetDecimal(out var parsedConfidence)
                    ? parsedConfidence
                    : overallConfidence;
                result.Add(new CategoryOcrLine(text, confidence, index++));
            }

            if (result.Count > 0)
                return result;
        }
        catch (JsonException)
        {
            // Older records may contain malformed line JSON. Raw text remains
            // sufficient for a safe, layout-free classification attempt.
        }

        return rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select((text, index) => new CategoryOcrLine(text, overallConfidence, index))
            .ToList();
    }

    private sealed record PreparedAnalysis(
        CategoryAnalysis Analysis,
        IReadOnlyList<Category> Categories);
}
