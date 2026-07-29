package com.example.appquanlychitieu.util;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.util.Calendar;
import java.util.TimeZone;

public class DateUtilsTest {
    private TimeZone originalTimeZone;

    @Before
    public void useApplicationTimeZone() {
        originalTimeZone = TimeZone.getDefault();
        TimeZone.setDefault(TimeZone.getTimeZone("Asia/Ho_Chi_Minh"));
    }

    @After
    public void restoreTimeZone() {
        TimeZone.setDefault(originalTimeZone);
    }

    @Test
    public void formatters_returnExpectedVietnameseDateRepresentations() {
        long timestamp = localTimestamp(2026, Calendar.JULY, 9, 14, 30, 0, 0);

        assertEquals("09/07/2026", DateUtils.formatDate(timestamp));
        assertEquals("2026-07", DateUtils.formatMonthYear(timestamp));
        assertEquals("Th\u00e1ng 07/2026", DateUtils.formatDisplayMonth(timestamp));
        assertEquals("09 thg 07", DateUtils.formatDayMonth(timestamp));
    }

    @Test
    public void monthBoundaries_handleLeapYearAndLastMillisecond() {
        Calendar start = calendar(DateUtils.getStartOfMonth(2024, Calendar.FEBRUARY));
        Calendar end = calendar(DateUtils.getEndOfMonth(2024, Calendar.FEBRUARY));

        assertCalendar(start, 2024, Calendar.FEBRUARY, 1, 0, 0, 0, 0);
        assertCalendar(end, 2024, Calendar.FEBRUARY, 29, 23, 59, 59, 999);
    }

    @Test
    public void dayBoundaries_andSameDayComparison_areCalendarBased() {
        long afternoon = localTimestamp(2026, Calendar.DECEMBER, 31, 16, 45, 12, 345);
        Calendar start = calendar(DateUtils.getStartOfDay(afternoon));
        Calendar end = calendar(DateUtils.getEndOfDay(afternoon));

        assertCalendar(start, 2026, Calendar.DECEMBER, 31, 0, 0, 0, 0);
        assertCalendar(end, 2026, Calendar.DECEMBER, 31, 23, 59, 59, 999);
        assertTrue(DateUtils.isSameDay(afternoon, end.getTimeInMillis()));
        assertFalse(DateUtils.isSameDay(
                afternoon,
                localTimestamp(2027, Calendar.JANUARY, 1, 0, 0, 0, 0)));
    }

    @Test
    public void relativeLabels_distinguishTodayYesterdayAndOlderDates() {
        Calendar yesterday = Calendar.getInstance();
        yesterday.add(Calendar.DAY_OF_YEAR, -1);
        Calendar older = Calendar.getInstance();
        older.add(Calendar.DAY_OF_YEAR, -2);

        assertEquals("H\u00f4m nay", DateUtils.getRelativeDateLabel(System.currentTimeMillis()));
        assertEquals("H\u00f4m qua", DateUtils.getRelativeDateLabel(yesterday.getTimeInMillis()));
        assertEquals(
                DateUtils.formatDate(older.getTimeInMillis()),
                DateUtils.getRelativeDateLabel(older.getTimeInMillis()));
    }

    private static long localTimestamp(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            int millisecond) {
        Calendar calendar = Calendar.getInstance();
        calendar.clear();
        calendar.set(year, month, day, hour, minute, second);
        calendar.set(Calendar.MILLISECOND, millisecond);
        return calendar.getTimeInMillis();
    }

    private static Calendar calendar(long timestamp) {
        Calendar calendar = Calendar.getInstance();
        calendar.setTimeInMillis(timestamp);
        return calendar;
    }

    private static void assertCalendar(
            Calendar calendar,
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            int millisecond) {
        assertEquals(year, calendar.get(Calendar.YEAR));
        assertEquals(month, calendar.get(Calendar.MONTH));
        assertEquals(day, calendar.get(Calendar.DAY_OF_MONTH));
        assertEquals(hour, calendar.get(Calendar.HOUR_OF_DAY));
        assertEquals(minute, calendar.get(Calendar.MINUTE));
        assertEquals(second, calendar.get(Calendar.SECOND));
        assertEquals(millisecond, calendar.get(Calendar.MILLISECOND));
    }
}
