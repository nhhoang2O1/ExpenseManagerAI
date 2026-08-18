package com.example.appquanlychitieu.util;

import java.time.LocalDate;
import java.time.format.DateTimeFormatter;

/** Date calculations shared by screens that display a user's financial cycle. */
public final class FinancialCycleUtils {
    private static final DateTimeFormatter KEY_FORMAT = DateTimeFormatter.ofPattern("yyyy-MM");

    private FinancialCycleUtils() { }

    public static LocalDate startFor(LocalDate date, int configuredDay) {
        int day = Math.max(1, Math.min(31, configuredDay));
        int effectiveDay = Math.min(day, date.lengthOfMonth());
        if (date.getDayOfMonth() >= effectiveDay) {
            return date.withDayOfMonth(effectiveDay);
        }
        LocalDate previous = date.minusMonths(1);
        return previous.withDayOfMonth(Math.min(day, previous.lengthOfMonth()));
    }

    public static LocalDate endFor(LocalDate cycleStart, int configuredDay) {
        LocalDate next = cycleStart.plusMonths(1);
        return next.withDayOfMonth(Math.min(Math.max(1, Math.min(31, configuredDay)), next.lengthOfMonth()))
                .minusDays(1);
    }

    public static String keyFor(LocalDate date, int configuredDay) {
        return startFor(date, configuredDay).format(KEY_FORMAT);
    }

    public static LocalDate cycleStartForMonth(int year, int zeroBasedMonth, int configuredDay) {
        LocalDate monthEnd = LocalDate.of(year, zeroBasedMonth + 1, 1)
                .withDayOfMonth(LocalDate.of(year, zeroBasedMonth + 1, 1).lengthOfMonth());
        return startFor(monthEnd, configuredDay);
    }
}
