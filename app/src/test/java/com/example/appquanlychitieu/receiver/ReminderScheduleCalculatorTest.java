package com.example.appquanlychitieu.receiver;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.util.Calendar;
import java.util.GregorianCalendar;
import java.util.TimeZone;

public class ReminderScheduleCalculatorTest {
    private static final TimeZone UTC = TimeZone.getTimeZone("UTC");

    @Test
    public void newReminderUsesFutureOccurrenceInCurrentMonth() {
        Calendar now = utc(2026, Calendar.AUGUST, 5, 9, 30);

        long actual = ReminderScheduleCalculator.nextTriggerMillis(
                now, 20, 8, 15, false);

        assertEquals(utc(2026, Calendar.AUGUST, 20, 8, 15).getTimeInMillis(), actual);
    }

    @Test
    public void deliveredReminderAlwaysMovesToNextMonth() {
        Calendar now = utc(2026, Calendar.AUGUST, 5, 8, 0);

        long actual = ReminderScheduleCalculator.nextTriggerMillis(
                now, 5, 8, 0, true);

        assertEquals(utc(2026, Calendar.SEPTEMBER, 5, 8, 0).getTimeInMillis(), actual);
        assertTrue(actual > now.getTimeInMillis());
    }

    @Test
    public void dayThirtyOneClampsToThirtyDayMonth() {
        Calendar now = utc(2026, Calendar.MARCH, 31, 10, 0);

        long actual = ReminderScheduleCalculator.nextTriggerMillis(
                now, 31, 10, 0, true);

        assertEquals(utc(2026, Calendar.APRIL, 30, 10, 0).getTimeInMillis(), actual);
    }

    @Test
    public void dayThirtyOneClampsToLeapYearFebruary() {
        Calendar now = utc(2028, Calendar.JANUARY, 31, 10, 0);

        long actual = ReminderScheduleCalculator.nextTriggerMillis(
                now, 31, 10, 0, true);

        assertEquals(utc(2028, Calendar.FEBRUARY, 29, 10, 0).getTimeInMillis(), actual);
    }

    @Test
    public void sameCurrentMinuteCanFireInitialReminderWithoutBeingDeferred() {
        Calendar now = utc(2026, Calendar.AUGUST, 5, 8, 0);
        now.set(Calendar.SECOND, 30);

        long actual = ReminderScheduleCalculator.nextTriggerMillis(
                now, 5, 8, 0, false);

        assertEquals(utc(2026, Calendar.AUGUST, 5, 8, 0).getTimeInMillis(), actual);
    }

    private static Calendar utc(int year, int month, int day, int hour, int minute) {
        Calendar value = new GregorianCalendar(UTC);
        value.clear();
        value.set(year, month, day, hour, minute, 0);
        return value;
    }
}
