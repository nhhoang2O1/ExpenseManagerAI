package com.example.appquanlychitieu.util;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class CurrencyFormatterTest {
    @Test
    public void format_usesVietnameseGroupingAndCurrencySymbol() {
        assertEquals("0 \u20ab", CurrencyFormatter.format(0));
        assertEquals("1.234.567 \u20ab", CurrencyFormatter.format(1_234_567));
        assertEquals("-1.234.567 \u20ab", CurrencyFormatter.format(-1_234_567));
    }

    @Test
    public void formatNoSymbol_returnsOnlyGroupedDigits() {
        assertEquals("98.765.432", CurrencyFormatter.formatNoSymbol(98_765_432));
    }

    @Test
    public void formatWithSign_normalizesTheAmountAndUsesTransactionDirection() {
        assertEquals("- 42.000 \u20ab", CurrencyFormatter.formatWithSign(42_000, true));
        assertEquals("+ 42.000 \u20ab", CurrencyFormatter.formatWithSign(42_000, false));
        assertEquals("+ 42.000 \u20ab", CurrencyFormatter.formatWithSign(-42_000, false));
    }
}
