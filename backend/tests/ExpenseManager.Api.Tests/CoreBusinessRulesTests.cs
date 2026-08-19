using System.ComponentModel.DataAnnotations;
using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Tests;

public sealed class AuthInputRulesTests
{
    [Fact]
    public void Register_accepts_valid_name_email_and_password()
    {
        var errors = ContractTestValidation.Validate(new RegisterRequest(
            "Nguyễn Văn A", "user@example.com", "strong-password"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Register_rejects_invalid_email()
    {
        var errors = ContractTestValidation.Validate(new RegisterRequest(
            "Nguyễn Văn A", "not-an-email", "strong-password"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Email"));
    }

    [Fact]
    public void Register_rejects_password_shorter_than_eight_characters()
    {
        var errors = ContractTestValidation.Validate(new RegisterRequest(
            "Nguyễn Văn A", "user@example.com", "short"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Password"));
    }

    [Fact]
    public void Register_rejects_blank_name()
    {
        var errors = ContractTestValidation.Validate(new RegisterRequest(
            "   ", "user@example.com", "strong-password"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Name"));
    }

    [Fact]
    public void Login_rejects_invalid_email()
    {
        var errors = ContractTestValidation.Validate(
            new LoginRequest("invalid", "strong-password"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Email"));
    }

    [Fact]
    public void Login_rejects_empty_password()
    {
        var errors = ContractTestValidation.Validate(
            new LoginRequest("user@example.com", string.Empty));

        Assert.Contains(errors, error => error.MemberNames.Contains("Password"));
    }

    [Fact]
    public void Email_is_trimmed_and_normalized_to_lowercase()
    {
        Assert.Equal(
            "user@example.com",
            AuthInputRules.NormalizeEmail("  USER@EXAMPLE.COM  "));
    }

}

public sealed class BudgetRulesTests
{
    [Fact]
    public void Budget_rejects_zero_amount()
    {
        var errors = ContractTestValidation.Validate(
            new BudgetRequest(Guid.NewGuid(), 0, "2026-08"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void Budget_rejects_negative_amount()
    {
        var errors = ContractTestValidation.Validate(
            new BudgetRequest(Guid.NewGuid(), -1, "2026-08"));

        Assert.Contains(errors, error => error.MemberNames.Contains("Amount"));
    }

    [Theory]
    [InlineData("2026-01")]
    [InlineData("2026-12")]
    public void Budget_accepts_valid_month(string monthYear)
    {
        Assert.True(BudgetRules.IsValidMonthYear(monthYear));
    }

    [Theory]
    [InlineData("2026-00")]
    [InlineData("2026-13")]
    [InlineData("2026-8")]
    [InlineData("08-2026")]
    [InlineData("")]
    public void Budget_rejects_invalid_month(string monthYear)
    {
        Assert.False(BudgetRules.IsValidMonthYear(monthYear));
    }

    [Fact]
    public void Budget_accepts_expense_category()
    {
        Assert.True(BudgetRules.CanUseCategory(TransactionType.EXPENSE));
    }

    [Fact]
    public void Budget_rejects_income_category()
    {
        Assert.False(BudgetRules.CanUseCategory(TransactionType.INCOME));
    }

    [Theory]
    [InlineData(50_000, 39_999, null)]
    [InlineData(50_000, 40_000, BudgetAlertLevel.APPROACHING)]
    [InlineData(50_000, 49_999, BudgetAlertLevel.APPROACHING)]
    [InlineData(50_000, 50_000, BudgetAlertLevel.EXCEEDED)]
    [InlineData(50_000, 70_000, BudgetAlertLevel.EXCEEDED)]
    public void Budget_alert_uses_eighty_and_one_hundred_percent_thresholds(
        long budget,
        long spent,
        BudgetAlertLevel? expected)
    {
        Assert.Equal(expected, BudgetRules.AlertLevel(budget, spent));
    }

}

public sealed class GoalFundingRulesTests
{
    [Fact]
    public void Add_funds_rejects_zero_amount()
    {
        var errors = ContractTestValidation.Validate(new AddGoalFundsRequest(0));

        Assert.Contains(errors, error => error.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void Add_funds_rejects_negative_amount()
    {
        var errors = ContractTestValidation.Validate(new AddGoalFundsRequest(-1));

        Assert.Contains(errors, error => error.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void Add_funds_applies_full_request_when_below_remaining_amount()
    {
        var result = GoalFundingRules.Calculate(1_000_000, 200_000, 300_000);

        Assert.Equal(300_000, result.AppliedAmount);
        Assert.Equal(500_000, result.BalanceAfter);
        Assert.False(result.WasAlreadyFunded);
    }

    [Fact]
    public void Add_funds_rejects_request_above_remaining_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GoalFundingRules.Calculate(1_000_000, 800_000, 500_000));
    }

    [Fact]
    public void Add_funds_identifies_an_already_funded_goal()
    {
        var result = GoalFundingRules.Calculate(1_000_000, 1_000_000, 100_000);

        Assert.Equal(0, result.AppliedAmount);
        Assert.Equal(1_000_000, result.BalanceAfter);
        Assert.True(result.WasAlreadyFunded);
    }

}

public sealed class StatisticsRulesTests
{
    [Theory]
    [InlineData(5_000_000, 1_200_000, 3_800_000)]
    [InlineData(2_000_000, 0, 2_000_000)]
    [InlineData(0, 700_000, -700_000)]
    [InlineData(0, 0, 0)]
    public void Balance_is_income_minus_expense(
        long income,
        long expense,
        long expected)
    {
        Assert.Equal(expected, StatisticsRules.Balance(income, expense));
    }

}

public sealed class CategoryRulesTests
{
    [Fact]
    public void Category_name_is_trimmed()
    {
        Assert.Equal("Ăn uống", CategoryRules.NormalizeName("  Ăn uống  "));
    }

    [Fact]
    public void Category_blank_name_remains_empty_for_controller_rejection()
    {
        Assert.Empty(CategoryRules.NormalizeName("   "));
    }

    [Fact]
    public void Category_accepts_income_type()
    {
        Assert.True(CategoryRules.IsSupportedType(TransactionType.INCOME));
    }

    [Fact]
    public void Category_accepts_expense_type()
    {
        Assert.True(CategoryRules.IsSupportedType(TransactionType.EXPENSE));
    }

    [Fact]
    public void Category_rejects_unknown_type()
    {
        Assert.False(CategoryRules.IsSupportedType((TransactionType)999));
    }

    [Fact]
    public void Category_optional_metadata_is_trimmed()
    {
        Assert.Equal("#FF0000", CategoryRules.NormalizeOptionalText("  #FF0000  "));
        Assert.Equal("ic_food", CategoryRules.NormalizeOptionalText("  ic_food  "));
    }
}

internal static class ContractTestValidation
{
    public static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var type = value.GetType();
        var constructor = Assert.Single(type.GetConstructors());
        var results = new List<ValidationResult>();

        foreach (var parameter in constructor.GetParameters())
        {
            var property = type.GetProperty(
                parameter.Name!,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);
            Assert.NotNull(property);

            var attributes = parameter
                .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Cast<ValidationAttribute>()
                .ToArray();
            var context = new ValidationContext(value)
            {
                MemberName = property.Name
            };
            Validator.TryValidateValue(
                property.GetValue(value),
                context,
                results,
                attributes);
        }

        return results;
    }
}
