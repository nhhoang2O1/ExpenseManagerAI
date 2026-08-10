using System.Globalization;
using System.Text;

namespace ExpenseManager.Api.Services;

public sealed record CategoryTextMatch(
    bool Matched,
    decimal Quality,
    string MatchedText)
{
    public static readonly CategoryTextMatch None = new(false, 0m, string.Empty);
}

public sealed class CategoryTextNormalizer
{
    public string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var compatibility = value.Normalize(NormalizationForm.FormKC)
            .Replace('Đ', 'D')
            .Replace('đ', 'd');
        var decomposed = compatibility.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                result.Append(' ');
                previousWasSpace = true;
            }
        }

        return result.ToString().Trim();
    }

    public IReadOnlyList<string> Tokens(string? value) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public CategoryTextMatch Match(
        string? input,
        string pattern,
        bool allowFuzzy = false)
    {
        var inputTokens = Tokens(input);
        var patternTokens = Tokens(pattern);
        if (inputTokens.Count == 0 || patternTokens.Count == 0 ||
            patternTokens.Count > inputTokens.Count)
            return CategoryTextMatch.None;

        for (var start = 0; start <= inputTokens.Count - patternTokens.Count; start++)
        {
            var exact = true;
            for (var index = 0; index < patternTokens.Count; index++)
            {
                if (inputTokens[start + index] == patternTokens[index])
                    continue;
                exact = false;
                break;
            }

            if (exact)
                return new CategoryTextMatch(
                    true,
                    1m,
                    string.Join(' ', inputTokens.Skip(start).Take(patternTokens.Count)));
        }

        if (!allowFuzzy)
            return CategoryTextMatch.None;

        // OCR typo tolerance is intentionally conservative: only one token may
        // differ, and short tokens never use edit-distance matching.
        for (var start = 0; start <= inputTokens.Count - patternTokens.Count; start++)
        {
            var fuzzyTokens = 0;
            var valid = true;
            for (var index = 0; index < patternTokens.Count; index++)
            {
                var actual = inputTokens[start + index];
                var expected = patternTokens[index];
                if (actual == expected)
                    continue;

                var minimumLength = patternTokens.Count == 1 ? 7 : 5;
                if (expected.Length < minimumLength || actual.Length < minimumLength ||
                    EditDistance(actual, expected, maximum: 1) > 1)
                {
                    valid = false;
                    break;
                }

                fuzzyTokens++;
                if (fuzzyTokens > 1)
                {
                    valid = false;
                    break;
                }
            }

            if (valid && fuzzyTokens == 1)
                return new CategoryTextMatch(
                    true,
                    0.78m,
                    string.Join(' ', inputTokens.Skip(start).Take(patternTokens.Count)));
        }

        return CategoryTextMatch.None;
    }

    private static int EditDistance(string left, string right, int maximum)
    {
        if (Math.Abs(left.Length - right.Length) > maximum)
            return maximum + 1;

        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            var current = new int[right.Length + 1];
            current[0] = leftIndex;
            var rowMinimum = current[0];
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitution = previous[rightIndex - 1] +
                    (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1);
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    substitution);
                rowMinimum = Math.Min(rowMinimum, current[rightIndex]);
            }

            if (rowMinimum > maximum)
                return maximum + 1;
            previous = current;
        }

        return previous[right.Length];
    }
}
