using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Services;

namespace ExpenseManager.Api.Tests;

public sealed class CategoryClassifierTests
{
    private readonly CategoryClassifier _classifier = new();

    [Theory]
    [InlineData("Trà-sữa  Oolong", "TRA SUA OOLONG")]
    [InlineData("ĐIỆN, nước & Internet", "DIEN NUOC INTERNET")]
    [InlineData("  Phí---quản lý\tchung cư ", "PHI QUAN LY CHUNG CU")]
    public void Normalizer_handles_vietnamese_unicode_punctuation_and_space(
        string input,
        string expected)
    {
        Assert.Equal(expected, new CategoryTextNormalizer().Normalize(input));
    }

    [Fact]
    public void Phrase_matching_uses_token_boundaries_not_substrings()
    {
        var normalizer = new CategoryTextNormalizer();

        Assert.True(normalizer.Match("CƠM PHẦN", "COM").Matched);
        Assert.False(normalizer.Match("THÀNH CÔNG", "COM").Matched);
    }

    [Fact]
    public void Conservative_fuzzy_matching_accepts_one_long_ocr_typo_only()
    {
        var normalizer = new CategoryTextNormalizer();

        Assert.True(normalizer.Match("PHARMACV", "PHARMACY", allowFuzzy: true).Matched);
        Assert.False(normalizer.Match("TFA", "TEA", allowFuzzy: true).Matched);
        Assert.False(normalizer.Match("PHXRMXCV", "PHARMACY", allowFuzzy: true).Matched);
    }

    [Theory]
    [MemberData(nameof(SyntheticExamples))]
    public void Synthetic_examples_generalize_without_known_brands(
        string merchant,
        string rawText,
        SemanticExpenseCategory expected)
    {
        var result = _classifier.Analyze(Input(merchant, rawText));

        Assert.True(result.Decision.Accepted, Debug(result));
        Assert.Equal(expected, result.Decision.SemanticCategory);
    }

    [Theory]
    [InlineData("The Tea Cafe", "DINE IN\nTEA\nTABLE NO 4", SemanticExpenseCategory.FOOD_AND_DRINK)]
    [InlineData("Green Supermarket", "SKU 123\nTEA BOTTLE\nUNIT PRICE 12000", SemanticExpenseCategory.SHOPPING)]
    [InlineData("Milk Coffee Shop", "TAKE AWAY\nMILK\nSIZE L", SemanticExpenseCategory.FOOD_AND_DRINK)]
    [InlineData("Daily Minimart", "BARCODE\nMILK BOX\nMEMBER PRICE", SemanticExpenseCategory.SHOPPING)]
    [InlineData("City Museum", "ENTRANCE TICKET\nADULT", SemanticExpenseCategory.ENTERTAINMENT)]
    [InlineData("Intercity Bus Operator", "BUS TICKET\nPASSENGER", SemanticExpenseCategory.TRANSPORT)]
    [InlineData("Good Health Pharmacy", "PRESCRIPTION\nMEDICINE TABLET", SemanticExpenseCategory.HEALTH)]
    [InlineData("Value Supermarket", "SKU\nVITAMIN BOX\nUNIT PRICE", SemanticExpenseCategory.SHOPPING)]
    [InlineData("North Language School", "STUDENT ID\nCOURSE FEE", SemanticExpenseCategory.EDUCATION)]
    [InlineData("Central Bookstore", "SKU\nBOOK\nUNIT PRICE", SemanticExpenseCategory.SHOPPING)]
    [InlineData("Home Maintenance", "HOME REPAIR WORK ORDER\nLABOR CHARGE", SemanticExpenseCategory.HOUSING)]
    [InlineData("Tools Hardware Store", "SKU\nREPAIR KIT\nUNIT PRICE", SemanticExpenseCategory.SHOPPING)]
    [InlineData("City Water Supply", "WATER BILL\nBILLING PERIOD\nMETER NUMBER", SemanticExpenseCategory.BILLS)]
    [InlineData("Fresh Supermarket", "BARCODE\nWATER BOTTLE\nUNIT PRICE", SemanticExpenseCategory.SHOPPING)]
    [InlineData("Fast Internet Service Provider", "INTERNET BILL\nBILLING PERIOD\nCUSTOMER CODE", SemanticExpenseCategory.BILLS)]
    [InlineData("Galaxy Internet Cafe", "PLAY TIME 2 HOURS\nGAME CREDIT", SemanticExpenseCategory.ENTERTAINMENT)]
    public void Context_resolves_ambiguous_tokens(
        string merchant,
        string rawText,
        SemanticExpenseCategory expected)
    {
        var result = _classifier.Analyze(Input(merchant, rawText));

        Assert.True(result.Decision.Accepted, Debug(result));
        Assert.Equal(expected, result.Decision.SemanticCategory);
    }

    [Fact]
    public void Unknown_merchant_with_only_weak_token_is_rejected()
    {
        var result = _classifier.Analyze(Input("A New Place", "TEA\nTOTAL 20.000"));

        Assert.False(result.Decision.Accepted);
        Assert.Null(result.Decision.SemanticCategory);
        Assert.StartsWith("TOP_SCORE_BELOW", result.Decision.RejectionReason);
    }

    [Fact]
    public void City_address_does_not_turn_thanh_pho_into_food_evidence()
    {
        var result = _classifier.Analyze(Input(
            "Unknown Store",
            "74 Nguyen Khang, Thanh Pho Ha Noi\nTOTAL 10000"));
        var food = result.Candidates.Single(x =>
            x.Category == SemanticExpenseCategory.FOOD_AND_DRINK);

        Assert.Empty(food.Evidence);
        Assert.False(result.Decision.Accepted);
    }

    [Fact]
    public void Tea_named_merchant_needs_order_context_and_never_accepts_by_name_alone()
    {
        var merchantOnly = _classifier.Analyze(Input("Moon Tea", "TOTAL 20000"));
        var withOrder = _classifier.Analyze(Input(
            "Moon Tea",
            "MANG VỀ\nTEA\nSIZE L\nTOTAL 20000"));

        Assert.False(merchantOnly.Decision.Accepted);
        Assert.True(withOrder.Decision.Accepted, Debug(withOrder));
        Assert.Equal(SemanticExpenseCategory.FOOD_AND_DRINK,
            withOrder.Decision.SemanticCategory);
    }

    [Fact]
    public void Equal_strong_ticket_contexts_are_rejected_as_ambiguous()
    {
        var rules = new[]
        {
            new CategoryPatternRule("equal-food", SemanticExpenseCategory.FOOD_AND_DRINK,
                CategoryRuleKind.STRONG_PHRASE, CategoryEvidenceSource.CONTENT,
                CategoryRuleScope.CONTENT, 15m, ["AMBIGUOUS SIGNAL"], [], "Equal signal."),
            new CategoryPatternRule("equal-shopping", SemanticExpenseCategory.SHOPPING,
                CategoryRuleKind.STRONG_PHRASE, CategoryEvidenceSource.CONTENT,
                CategoryRuleScope.CONTENT, 15m, ["AMBIGUOUS SIGNAL"], [], "Equal signal.")
        };
        var classifier = new CategoryClassifier(
            ruleSet: new CategoryRuleSet(rules, [], []));
        var result = classifier.Analyze(Input("Unknown Counter", "AMBIGUOUS SIGNAL"));

        Assert.False(result.Decision.Accepted);
        Assert.Null(result.Decision.SemanticCategory);
        Assert.Contains("MARGIN", result.Decision.RejectionReason);
    }

    [Fact]
    public void Negative_evidence_is_visible_in_candidate_debug_output()
    {
        var result = _classifier.Analyze(Input(
            "Heritage Center",
            "ENTRANCE TICKET\nADMISSION\nTOTAL 100000"));
        var food = result.Candidates.Single(x =>
            x.Category == SemanticExpenseCategory.FOOD_AND_DRINK);

        Assert.True(food.NegativeScore < 0);
        Assert.Contains(food.Evidence, x =>
            x.Source == CategoryEvidenceSource.NEGATIVE &&
            x.RuleId.StartsWith("admission-ticket"));
    }

    [Fact]
    public void Composition_evidence_exposes_rule_id_and_source_lines()
    {
        var result = _classifier.Analyze(Input(
            "Local Coffee Shop",
            "DINE IN\nCAPPUCCINO\nTOTAL 50000"));
        var food = result.Candidates.Single(x =>
            x.Category == SemanticExpenseCategory.FOOD_AND_DRINK);

        var composition = Assert.Single(food.Evidence.Where(x =>
            x.RuleId == "food-service-product"));
        Assert.Equal(CategoryEvidenceSource.COMPOSITION, composition.Source);
        Assert.NotEmpty(composition.SourceLineIndexes);
    }

    [Fact]
    public void Brand_profile_is_capped_and_cannot_accept_by_itself()
    {
        var profile = new MerchantBrandProfile(
            "opaque-venue",
            ["OPAQUE VENUE"],
            SemanticExpenseCategory.ENTERTAINMENT,
            99m,
            [],
            "Optional brand prior.");
        var classifier = new CategoryClassifier(
            ruleSet: new CategoryRuleSet(brandProfiles: [profile]));

        var result = classifier.Analyze(Input("Opaque Venue", "TOTAL 50000"));
        var evidence = result.Candidates
            .Single(x => x.Category == SemanticExpenseCategory.ENTERTAINMENT)
            .Evidence.Single(x => x.RuleId == "brand:opaque-venue");

        Assert.Equal(4m, evidence.Contribution);
        Assert.False(result.Decision.Accepted);
    }

    public static IEnumerable<object[]> SyntheticExamples()
    {
        // FOOD_AND_DRINK
        yield return Case("Quán Ăn Cô Ba", "BÚN BÒ\nBÀN SỐ 12", SemanticExpenseCategory.FOOD_AND_DRINK);
        yield return Case("Morning Coffee Shop", "DINE IN\nCAPPUCCINO", SemanticExpenseCategory.FOOD_AND_DRINK);
        yield return Case("Pizza Express", "TAKE AWAY\nCOMBO MEAL", SemanticExpenseCategory.FOOD_AND_DRINK);
        yield return Case("Canteen Đại Phát", "CƠM PHẦN\nTABLE NO 3", SemanticExpenseCategory.FOOD_AND_DRINK);
        yield return Case("Fresh Cup Cafe", "MILK TEA\nSIZE L\nTOPPING", SemanticExpenseCategory.FOOD_AND_DRINK);

        // TRANSPORT
        yield return Case("Taxi Thành Công", "TAXI FARE\nPICKUP\nDROP OFF", SemanticExpenseCategory.TRANSPORT);
        yield return Case("City Bus Operator", "BUS TICKET\nPASSENGER", SemanticExpenseCategory.TRANSPORT);
        yield return Case("National Railway", "TRAIN TICKET\nCOACH 2", SemanticExpenseCategory.TRANSPORT);
        yield return Case("Central Parking", "PARKING FEE\nVEHICLE NUMBER", SemanticExpenseCategory.TRANSPORT);
        yield return Case("South Petrol Station", "FUEL PURCHASE\nDIESEL 20 LITRES", SemanticExpenseCategory.TRANSPORT);

        // SHOPPING
        yield return Case("Happy Supermarket", "SKU\nMILK BOX\nNOODLE PACK\nUNIT PRICE", SemanticExpenseCategory.SHOPPING);
        yield return Case("Urban Fashion Store", "QUẦN ÁO\nRETURN POLICY", SemanticExpenseCategory.SHOPPING);
        yield return Case("Tech Electronics Store", "TAI NGHE\nWARRANTY", SemanticExpenseCategory.SHOPPING);
        yield return Case("Daily Minimart", "BARCODE\nSOAP BOX\nMEMBER PRICE", SemanticExpenseCategory.SHOPPING);
        yield return Case("Home Hardware Store", "SƠN HỘP\nSKU\nUNIT PRICE", SemanticExpenseCategory.SHOPPING);

        // HOUSING
        yield return Case("Chủ Nhà", "PHIẾU THU TIỀN THUÊ PHÒNG THÁNG 08", SemanticExpenseCategory.HOUSING);
        yield return Case("Ban Quản Lý Chung Cư", "PHÍ QUẢN LÝ CHUNG CƯ", SemanticExpenseCategory.HOUSING);
        yield return Case("Minh Tâm Home Maintenance", "SỬA CHỮA NHÀ\nWORK ORDER", SemanticExpenseCategory.HOUSING);
        yield return Case("Home Air Conditioning Service", "AIR CONDITIONER REPAIR\nLABOR CHARGE", SemanticExpenseCategory.HOUSING);
        yield return Case("An Bình Home Cleaning Service", "HOME CLEANING SERVICE\nAPARTMENT", SemanticExpenseCategory.HOUSING);

        // ENTERTAINMENT
        yield return Case("City Museum", "ADMISSION TICKET\nADULT", SemanticExpenseCategory.ENTERTAINMENT);
        yield return Case("Star Cinema", "MOVIE TICKET\nSCREEN 4", SemanticExpenseCategory.ENTERTAINMENT);
        yield return Case("Summer Concert", "EVENT TICKET\nSEAT A12", SemanticExpenseCategory.ENTERTAINMENT);
        yield return Case("Happy Theme Park", "ENTRANCE TICKET\nVISITOR", SemanticExpenseCategory.ENTERTAINMENT);
        yield return Case("SingNow Karaoke", "KARAOKE ROOM CHARGE\n2 HOURS", SemanticExpenseCategory.ENTERTAINMENT);

        // HEALTH
        yield return Case("Phòng Khám An Tâm", "PHÍ KHÁM BỆNH", SemanticExpenseCategory.HEALTH);
        yield return Case("Nhà Thuốc Hòa Bình", "PRESCRIPTION MEDICINE", SemanticExpenseCategory.HEALTH);
        yield return Case("Dental Care Clinic", "DENTAL TREATMENT\nX RAY", SemanticExpenseCategory.HEALTH);
        yield return Case("Medical Laboratory 24", "LAB TEST\nPATIENT ID", SemanticExpenseCategory.HEALTH);
        yield return Case("Vaccination Center", "VACCINATION\nCONSULTATION FEE", SemanticExpenseCategory.HEALTH);

        // EDUCATION
        yield return Case("Trường Ngoại Ngữ Á Châu", "HỌC PHÍ THÁNG 8\nSTUDENT ID", SemanticExpenseCategory.EDUCATION);
        yield return Case("Học Viện Kỹ Năng", "COURSE FEE", SemanticExpenseCategory.EDUCATION);
        yield return Case("Exam Center", "EXAM FEE RECEIPT\nSTUDENT ID", SemanticExpenseCategory.EDUCATION);
        yield return Case("Sao Mai Tutoring Center", "TUTORING FEE", SemanticExpenseCategory.EDUCATION);
        yield return Case("Technical College", "ENROLLMENT FEE\nSTUDENT ID", SemanticExpenseCategory.EDUCATION);

        // BILLS
        yield return Case("City Power Company", "ELECTRICITY BILL\nBILLING PERIOD\nMETER NUMBER", SemanticExpenseCategory.BILLS);
        yield return Case("City Water Supply", "WATER BILL\nMETER READING\nM3 CONSUMPTION", SemanticExpenseCategory.BILLS);
        yield return Case("Fast Internet Service Provider", "INTERNET BILL\nBILLING PERIOD\nCUSTOMER CODE", SemanticExpenseCategory.BILLS);
        yield return Case("Mobile Operator", "MOBILE POSTPAID BILL\nCURRENT CHARGE", SemanticExpenseCategory.BILLS);
        yield return Case("Cable Provider", "CABLE TV BILL\nMONTHLY SUBSCRIPTION", SemanticExpenseCategory.BILLS);
    }

    private static object[] Case(
        string merchant,
        string rawText,
        SemanticExpenseCategory expected) => [merchant, rawText, expected];

    private static CategoryClassifierInput Input(string merchant, string rawText)
    {
        var lines = rawText.Split('\n')
            .Select((text, index) => new CategoryOcrLine(text, 0.95m, index))
            .ToList();
        return new CategoryClassifierInput(merchant, rawText, lines, OverallConfidence: 0.95m);
    }

    private static string Debug(CategoryAnalysis result) =>
        string.Join(" | ", result.Candidates.Take(3).Select(candidate =>
            $"{candidate.Category}={candidate.Score}:" +
            string.Join(',', candidate.Evidence.Select(x => $"{x.RuleId}:{x.Contribution}")))) +
        $" rejection={result.Decision.RejectionReason}";
}

public sealed class CategoryHistoryScorerTests
{
    [Theory]
    [InlineData(1, 1, 2.4)]
    [InlineData(2, 2, 4.8)]
    [InlineData(3, 3, 7.2)]
    [InlineData(5, 5, 12.0)]
    [InlineData(5, 9, 1.33)]
    [InlineData(4, 9, 0.0)]
    public void History_uses_count_and_distribution(int categoryCount, int total, double expected)
    {
        var values = Enumerable.Repeat(SemanticExpenseCategory.FOOD_AND_DRINK, categoryCount)
            .Concat(Enumerable.Repeat(SemanticExpenseCategory.SHOPPING, total - categoryCount))
            .ToList();

        var evidence = new CategoryHistoryScorer().Score(values)
            .SingleOrDefault(x => x.Category == SemanticExpenseCategory.FOOD_AND_DRINK);

        Assert.Equal((decimal)expected, evidence?.Contribution ?? 0m);
    }
}

public sealed class UserCategoryMapperTests
{
    private readonly CategoryTextNormalizer _normalizer = new();

    [Fact]
    public void Maps_canonical_and_normalized_alias_names()
    {
        var mapper = new UserCategoryMapper(_normalizer);
        var user = CategorySuggestionServiceTests.NewUser("mapper@example.com");
        var category = CategorySuggestionServiceTests.NewCategory(user, "  ĂN UỐNG  ");

        Assert.Equal(SemanticExpenseCategory.FOOD_AND_DRINK, mapper.ToSemantic(category));
        Assert.Same(category, mapper.FindCategory(
            SemanticExpenseCategory.FOOD_AND_DRINK, [category]));
    }

    [Fact]
    public void Renamed_unknown_custom_category_is_not_guessed()
    {
        var mapper = new UserCategoryMapper(_normalizer);
        var user = CategorySuggestionServiceTests.NewUser("mapper-custom@example.com");
        var category = CategorySuggestionServiceTests.NewCategory(user, "Đi cà phê cuối tuần");

        Assert.Null(mapper.ToSemantic(category));
        Assert.Null(mapper.FindCategory(
            SemanticExpenseCategory.FOOD_AND_DRINK, [category]));
    }
}
