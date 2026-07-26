package com.example.appquanlychitieu.util;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.Locale;

/** Formats whole Vietnamese đồng values; monetary values never cross this API as floating point. */
public final class CurrencyFormatter {
    private static final DecimalFormat FORMATTER;

    static {
        DecimalFormatSymbols symbols = new DecimalFormatSymbols(new Locale("vi", "VN"));
        symbols.setGroupingSeparator('.');
        FORMATTER = new DecimalFormat("#,###", symbols);
    }

    private CurrencyFormatter() { }

    public static String format(long amount) {
        return FORMATTER.format(amount) + " ₫";
    }

    public static String formatNoSymbol(long amount) {
        return FORMATTER.format(amount);
    }

    public static String formatWithSign(long amount, boolean isExpense) {
        String prefix = isExpense ? "- " : "+ ";
        return prefix + format(Math.abs(amount));
    }
}
