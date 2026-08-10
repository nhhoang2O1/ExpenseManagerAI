namespace ExpenseManager.Api.Services;

public sealed class CategoryClassifier
{
    public const decimal MinimumAcceptedScore = 12m;
    public const decimal MinimumScoreMargin = 5m;
    public const decimal StrongConflictMargin = 8m;

    private static readonly IReadOnlyDictionary<CategoryEvidenceSource, decimal> PositiveCaps =
        new Dictionary<CategoryEvidenceSource, decimal>
        {
            [CategoryEvidenceSource.MERCHANT] = 18m,
            [CategoryEvidenceSource.CONTENT] = 24m,
            [CategoryEvidenceSource.DOCUMENT_TYPE] = 18m,
            [CategoryEvidenceSource.PRODUCT] = 24m,
            [CategoryEvidenceSource.USER_HISTORY] = 12m,
            [CategoryEvidenceSource.COMPOSITION] = 20m
        };

    private readonly CategoryTextNormalizer _normalizer;
    private readonly CategoryRuleSet _ruleSet;

    public CategoryClassifier(
        CategoryTextNormalizer? normalizer = null,
        CategoryRuleSet? ruleSet = null)
    {
        _normalizer = normalizer ?? new CategoryTextNormalizer();
        _ruleSet = ruleSet ?? new CategoryRuleSet();
    }

    public CategoryAnalysis Analyze(CategoryClassifierInput input)
    {
        var lines = input.Lines.Count > 0
            ? input.Lines
            : input.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select((text, index) => new CategoryOcrLine(text, input.OverallConfidence, index))
                .ToList();
        var evidence = new List<CategoryEvidence>();
        var facts = new Dictionary<string, MatchedFact>(StringComparer.Ordinal);

        foreach (var rule in _ruleSet.PatternRules)
        {
            var match = BestMatch(rule, input.MerchantName, lines, input.OverallConfidence);
            if (match is null)
                continue;

            foreach (var tag in rule.EmitsTags)
            {
                if (!facts.TryGetValue(tag, out var existing) || existing.Quality < match.Quality)
                    facts[tag] = new MatchedFact(tag, match.Text, match.LineIndexes, match.Quality);
            }

            if (rule.Category is null || rule.Weight == 0)
                continue;

            var contribution = decimal.Round(
                rule.Weight * match.Quality * match.OcrQuality,
                2,
                MidpointRounding.AwayFromZero);
            evidence.Add(new CategoryEvidence(
                rule.Category.Value,
                contribution,
                rule.Source,
                rule.Kind,
                rule.Id,
                match.Text,
                match.LineIndexes,
                match.Quality,
                rule.Reason));
        }

        ApplyBrandProfiles(input.MerchantName, evidence, facts);
        ApplyCompositionRules(facts, evidence);
        if (input.HistoryEvidence is not null)
            evidence.AddRange(input.HistoryEvidence);

        var candidates = Enum.GetValues<SemanticExpenseCategory>()
            .Where(x => x != SemanticExpenseCategory.OTHER)
            .Select(category => BuildCandidate(category, evidence))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Category)
            .ToList();

        var top = candidates[0];
        var runnerUp = candidates[1];
        var margin = top.Score - runnerUp.Score;
        var reliableEvidence = top.Evidence.Any(item =>
            item.Contribution >= 6m && item.Source != CategoryEvidenceSource.NEGATIVE);
        var rejectionReason = DecisionRejection(top, runnerUp, margin, reliableEvidence);
        var accepted = rejectionReason is null;
        var confidence = HeuristicConfidence(top, margin);
        var tier = confidence >= 0.80m
            ? CategoryConfidenceTier.HIGH
            : confidence >= 0.60m
                ? CategoryConfidenceTier.MEDIUM
                : CategoryConfidenceTier.LOW;

        return new CategoryAnalysis(
            new CategoryDecision(
                accepted ? top.Category : null,
                top.Score,
                runnerUp.Score,
                margin,
                confidence,
                tier,
                accepted,
                rejectionReason),
            candidates);
    }

    private PatternMatch? BestMatch(
        CategoryPatternRule rule,
        string? merchant,
        IReadOnlyList<CategoryOcrLine> lines,
        decimal overallConfidence)
    {
        var matches = new List<PatternMatch>();
        if (rule.Scope is CategoryRuleScope.MERCHANT or CategoryRuleScope.BOTH)
        {
            foreach (var pattern in rule.Patterns)
            {
                var match = _normalizer.Match(merchant, pattern, rule.AllowFuzzy);
                if (match.Matched)
                    matches.Add(new PatternMatch(
                        match.MatchedText,
                        [],
                        match.Quality,
                        OcrFactor(overallConfidence)));
            }
        }

        if (rule.Scope is CategoryRuleScope.CONTENT or CategoryRuleScope.BOTH)
        {
            foreach (var line in lines)
            {
                foreach (var pattern in rule.Patterns)
                {
                    var match = _normalizer.Match(line.Text, pattern, rule.AllowFuzzy);
                    if (match.Matched)
                        matches.Add(new PatternMatch(
                            match.MatchedText,
                            [line.Index],
                            match.Quality,
                            OcrFactor(line.Confidence)));
                }
            }
        }

        return matches
            .OrderByDescending(x => x.Quality * x.OcrQuality)
            .ThenByDescending(x => x.Text.Length)
            .FirstOrDefault();
    }

    private void ApplyBrandProfiles(
        string? merchant,
        ICollection<CategoryEvidence> evidence,
        IDictionary<string, MatchedFact> facts)
    {
        foreach (var profile in _ruleSet.BrandProfiles)
        {
            var match = profile.Aliases
                .Select(alias => _normalizer.Match(merchant, alias))
                .FirstOrDefault(item => item.Matched);
            if (match is null || !match.Matched)
                continue;

            foreach (var tag in profile.EmitsTags)
                facts[tag] = new MatchedFact(tag, match.MatchedText, [], match.Quality);

            if (profile.Category is null || profile.Contribution <= 0)
                continue;

            evidence.Add(new CategoryEvidence(
                profile.Category.Value,
                Math.Min(4m, profile.Contribution),
                CategoryEvidenceSource.MERCHANT,
                CategoryRuleKind.WEAK_TOKEN,
                $"brand:{profile.Id}",
                match.MatchedText,
                [],
                match.Quality,
                profile.Reason));
        }
    }

    private void ApplyCompositionRules(
        IReadOnlyDictionary<string, MatchedFact> facts,
        ICollection<CategoryEvidence> evidence)
    {
        foreach (var rule in _ruleSet.CompositionRules)
        {
            if (rule.RequireAllTags.Any(tag => !facts.ContainsKey(tag)) ||
                (rule.RequireAnyTags.Count > 0 && rule.RequireAnyTags.All(tag => !facts.ContainsKey(tag))) ||
                rule.ForbidTags.Any(facts.ContainsKey))
                continue;

            var usedTags = rule.RequireAllTags
                .Concat(rule.RequireAnyTags.Where(facts.ContainsKey))
                .Distinct(StringComparer.Ordinal)
                .Select(tag => facts[tag])
                .ToList();
            var quality = usedTags.Count == 0 ? 1m : usedTags.Average(x => x.Quality);
            var text = string.Join(" + ", usedTags.Select(x => x.Text).Distinct());
            var lineIndexes = usedTags.SelectMany(x => x.LineIndexes).Distinct().Order().ToList();
            evidence.Add(new CategoryEvidence(
                rule.Category,
                decimal.Round(rule.Weight * quality, 2, MidpointRounding.AwayFromZero),
                CategoryEvidenceSource.COMPOSITION,
                CategoryRuleKind.STRONG_PHRASE,
                rule.Id,
                text,
                lineIndexes,
                quality,
                rule.Reason));

            if (rule.Penalties is null)
                continue;
            foreach (var penalty in rule.Penalties)
            {
                evidence.Add(new CategoryEvidence(
                    penalty.Key,
                    penalty.Value,
                    CategoryEvidenceSource.NEGATIVE,
                    CategoryRuleKind.NEGATIVE,
                    $"{rule.Id}:negative:{penalty.Key}",
                    text,
                    lineIndexes,
                    quality,
                    rule.Reason));
            }
        }
    }

    private static CategoryCandidateAnalysis BuildCandidate(
        SemanticExpenseCategory category,
        IReadOnlyList<CategoryEvidence> allEvidence)
    {
        var items = allEvidence.Where(x => x.Category == category).ToList();
        var positive = items
            .Where(x => x.Contribution > 0)
            .GroupBy(x => x.Source)
            .Sum(group => Math.Min(
                PositiveCaps.TryGetValue(group.Key, out var cap) ? cap : 24m,
                group.Sum(x => x.Contribution)));
        var negative = Math.Max(-20m, items.Where(x => x.Contribution < 0).Sum(x => x.Contribution));
        return new CategoryCandidateAnalysis(
            category,
            decimal.Round(positive + negative, 2),
            decimal.Round(positive, 2),
            decimal.Round(negative, 2),
            items.OrderByDescending(x => x.Contribution).ToList());
    }

    private static string? DecisionRejection(
        CategoryCandidateAnalysis top,
        CategoryCandidateAnalysis runnerUp,
        decimal margin,
        bool reliableEvidence)
    {
        if (top.Score < MinimumAcceptedScore)
            return $"TOP_SCORE_BELOW_{MinimumAcceptedScore:0}";
        if (!reliableEvidence)
            return "ONLY_WEAK_EVIDENCE";
        if (margin < MinimumScoreMargin)
            return $"MARGIN_BELOW_{MinimumScoreMargin:0}";
        if (runnerUp.Score >= MinimumAcceptedScore && margin < StrongConflictMargin)
            return "STRONG_EVIDENCE_CONFLICT";
        return null;
    }

    private static decimal HeuristicConfidence(
        CategoryCandidateAnalysis top,
        decimal margin)
    {
        var strength = Math.Min(1m, Math.Max(0m, top.Score) / 24m);
        var separation = Math.Min(1m, Math.Max(0m, margin) / 12m);
        var positiveEvidence = top.Evidence.Where(x => x.Contribution > 0).ToList();
        var quality = positiveEvidence.Count == 0
            ? 0m
            : positiveEvidence.Average(x => x.MatchQuality);
        var independentSources = positiveEvidence.Select(x => x.Source).Distinct().Count();
        var agreement = Math.Min(1m, independentSources / 3m);
        return decimal.Round(
            Math.Clamp(
                0.35m * strength + 0.30m * separation + 0.25m * quality + 0.10m * agreement,
                0m,
                0.95m),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static decimal OcrFactor(decimal confidence) =>
        0.8m + 0.2m * Math.Clamp(confidence, 0m, 1m);

    private sealed record PatternMatch(
        string Text,
        IReadOnlyList<int> LineIndexes,
        decimal Quality,
        decimal OcrQuality);

    private sealed record MatchedFact(
        string Tag,
        string Text,
        IReadOnlyList<int> LineIndexes,
        decimal Quality);
}
