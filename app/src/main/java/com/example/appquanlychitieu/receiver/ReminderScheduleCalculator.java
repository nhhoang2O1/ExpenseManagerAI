package com.example.appquanlychitieu.receiver;

import java.util.Calendar;

/** Pure monthly schedule calculation shared by Android alarms and JVM tests. */
final class ReminderScheduleCalculator {
    private ReminderScheduleCalculator() {}

    static long nextTriggerMillis(
            Calendar currentTime,
            int requestedDay,
            int hour,
            int minute,
            boolean afterDelivery) {
        long now = currentTime.getTimeInMillis();
        Calendar candidate = (Calendar) currentTime.clone();
        candidate.set(Calendar.HOUR_OF_DAY, hour);
        candidate.set(Calendar.MINUTE, minute);
        candidate.set(Calendar.SECOND, 0);
        candidate.set(Calendar.MILLISECOND, 0);
        setValidDay(candidate, requestedDay);

        if (afterDelivery) {
            do {
                moveToNextMonth(candidate, requestedDay);
            } while (candidate.getTimeInMillis() <= now);
        } else if (candidate.getTimeInMillis() < now - 60_000L) {
            do {
                moveToNextMonth(candidate, requestedDay);
            } while (candidate.getTimeInMillis() <= now);
        }
        return candidate.getTimeInMillis();
    }

    private static void moveToNextMonth(Calendar calendar, int requestedDay) {
        calendar.set(Calendar.DAY_OF_MONTH, 1);
        calendar.add(Calendar.MONTH, 1);
        setValidDay(calendar, requestedDay);
    }

    private static void setValidDay(Calendar calendar, int requestedDay) {
        int maxDays = calendar.getActualMaximum(Calendar.DAY_OF_MONTH);
        calendar.set(Calendar.DAY_OF_MONTH, Math.min(requestedDay, maxDays));
    }
}
