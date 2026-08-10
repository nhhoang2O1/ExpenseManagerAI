namespace ExpenseManager.Api.Services;

public enum SemanticExpenseCategory
{
    FOOD_AND_DRINK,
    TRANSPORT,
    SHOPPING,
    HOUSING,
    ENTERTAINMENT,
    HEALTH,
    EDUCATION,
    BILLS,
    OTHER
}

public enum CategoryEvidenceSource
{
    MERCHANT,
    CONTENT,
    DOCUMENT_TYPE,
    PRODUCT,
    USER_HISTORY,
    NEGATIVE,
    COMPOSITION
}

public enum CategoryRuleKind
{
    STRONG_PHRASE,
    MODERATE_PHRASE,
    WEAK_TOKEN,
    MERCHANT_DESCRIPTOR,
    DOCUMENT_TYPE,
    NEGATIVE,
    NEUTRAL
}

public enum CategoryRuleScope
{
    MERCHANT,
    CONTENT,
    BOTH
}

public enum CategoryConfidenceTier
{
    LOW,
    MEDIUM,
    HIGH
}

public sealed record CategoryOcrLine(
    string Text,
    decimal Confidence,
    int Index);

public sealed record CategoryEvidence(
    SemanticExpenseCategory Category,
    decimal Contribution,
    CategoryEvidenceSource Source,
    CategoryRuleKind Kind,
    string RuleId,
    string MatchedText,
    IReadOnlyList<int> SourceLineIndexes,
    decimal MatchQuality,
    string Reason);

public sealed record CategoryCandidateAnalysis(
    SemanticExpenseCategory Category,
    decimal Score,
    decimal PositiveScore,
    decimal NegativeScore,
    IReadOnlyList<CategoryEvidence> Evidence);

public sealed record CategoryDecision(
    SemanticExpenseCategory? SemanticCategory,
    decimal TopScore,
    decimal RunnerUpScore,
    decimal Margin,
    decimal HeuristicConfidence,
    CategoryConfidenceTier ConfidenceTier,
    bool Accepted,
    string? RejectionReason);

public sealed record CategoryAnalysis(
    CategoryDecision Decision,
    IReadOnlyList<CategoryCandidateAnalysis> Candidates);

public sealed record CategoryClassifierInput(
    string? MerchantName,
    string RawText,
    IReadOnlyList<CategoryOcrLine> Lines,
    IReadOnlyList<CategoryEvidence>? HistoryEvidence = null,
    decimal OverallConfidence = 0.9m);

public sealed record CategoryPatternRule(
    string Id,
    SemanticExpenseCategory? Category,
    CategoryRuleKind Kind,
    CategoryEvidenceSource Source,
    CategoryRuleScope Scope,
    decimal Weight,
    IReadOnlyList<string> Patterns,
    IReadOnlyList<string> EmitsTags,
    string Reason,
    bool AllowFuzzy = false);

public sealed record CategoryCompositionRule(
    string Id,
    SemanticExpenseCategory Category,
    decimal Weight,
    IReadOnlyList<string> RequireAllTags,
    IReadOnlyList<string> RequireAnyTags,
    IReadOnlyList<string> ForbidTags,
    string Reason,
    IReadOnlyDictionary<SemanticExpenseCategory, decimal>? Penalties = null);

public sealed record MerchantBrandProfile(
    string Id,
    IReadOnlyList<string> Aliases,
    SemanticExpenseCategory? Category,
    decimal Contribution,
    IReadOnlyList<string> EmitsTags,
    string Reason);
