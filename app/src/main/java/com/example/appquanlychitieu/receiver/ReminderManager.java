package com.example.appquanlychitieu.receiver;

import android.annotation.SuppressLint;
import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.util.Log;

import com.example.appquanlychitieu.data.model.Reminder;

import java.util.Calendar;
import java.util.HashSet;
import java.util.Set;

import com.google.gson.Gson;
import com.example.appquanlychitieu.util.SessionManager;

public class ReminderManager {
    private static final String STORE_NAME = "scheduled_reminders";
    private static final String KEY_IDS = "ids";
    private static final String KEY_PREFIX = "reminder_";
    private static final Gson GSON = new Gson();

    public static void scheduleReminder(Context context, Reminder reminder) {
        scheduleReminder(context, reminder, false);
    }

    /** Schedules the next monthly occurrence after the current alarm was delivered. */
    static void rescheduleAfterDelivery(Context context, Reminder reminder) {
        scheduleReminder(context, reminder, true);
    }

    private static void scheduleReminder(
            Context context,
            Reminder reminder,
            boolean afterDelivery) {
        if (reminder.getUserId() <= 0) {
            Log.w("ReminderManager", "Skipping schedule because reminder has no valid owner");
            return;
        }
        persistSchedule(context, reminder);
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null) return;

        Intent intent = new Intent(context, ReminderReceiver.class);
        intent.putExtra("reminder_id", reminder.getId());
        intent.putExtra("reminder_content", reminder.getContent());
        intent.putExtra("reminder_day", reminder.getDayOfMonth());
        intent.putExtra("reminder_hour", reminder.getHour());
        intent.putExtra("reminder_minute", reminder.getMinute());
        intent.putExtra("reminder_active", reminder.isActive());
        intent.putExtra("reminder_user_id", reminder.getUserId());

        int flags = PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE;

        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                context,
                (int) reminder.getId(),
                intent,
                flags
        );

        Calendar now = Calendar.getInstance();
        long alarmTime = ReminderScheduleCalculator.nextTriggerMillis(
                now,
                reminder.getDayOfMonth(),
                reminder.getHour(),
                reminder.getMinute(),
                afterDelivery);
        Log.d("ReminderManager", "Scheduled alarm for reminder "
                + reminder.getId() + " at " + alarmTime);

        try {
            boolean canScheduleExact = true;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                canScheduleExact = alarmManager.canScheduleExactAlarms();
            }

            if (canScheduleExact) {
                alarmManager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
            } else {
                alarmManager.set(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
            }
        } catch (SecurityException e) {
            Log.e("ReminderManager", "Exact alarm permission denied. Falling back to inexact.", e);
            alarmManager.set(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
        }
    }

    public static void cancelReminder(Context context, Reminder reminder) {
        removeSchedule(context, reminder.getUserId(), reminder.getId());
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null) return;

        Intent intent = new Intent(context, ReminderReceiver.class);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE;

        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                context,
                (int) reminder.getId(),
                intent,
                flags
        );

        alarmManager.cancel(pendingIntent);
        pendingIntent.cancel();
    }

    public static void reschedulePersisted(Context context) {
        long userId = new SessionManager(context.getApplicationContext()).getUserId();
        if (userId <= 0) return;
        SharedPreferences preferences = preferences(context, userId);
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        for (String id : ids) {
            String json = preferences.getString(KEY_PREFIX + id, null);
            if (json == null) continue;
            try {
                Reminder reminder = GSON.fromJson(json, Reminder.class);
                if (reminder != null && reminder.isActive()) scheduleReminder(context, reminder);
            } catch (RuntimeException exception) {
                Log.w("ReminderManager", "Ignoring invalid persisted reminder " + id, exception);
            }
        }
    }

    private static void persistSchedule(Context context, Reminder reminder) {
        SharedPreferences preferences = preferences(context, reminder.getUserId());
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        String id = Long.toString(reminder.getId());
        ids.add(id);
        preferences.edit()
                .putStringSet(KEY_IDS, ids)
                .putString(KEY_PREFIX + id, GSON.toJson(reminder))
                .apply();
    }

    private static void removeSchedule(Context context, long userId, long reminderId) {
        if (userId <= 0) return;
        SharedPreferences preferences = preferences(context, userId);
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        String id = Long.toString(reminderId);
        ids.remove(id);
        preferences.edit()
                .putStringSet(KEY_IDS, ids)
                .remove(KEY_PREFIX + id)
                .apply();
    }

    /** Cancels and removes only the alarms owned by the specified user. */
    @SuppressLint("ApplySharedPref") // User isolation requires cleanup before session switching.
    public static void clearForUser(Context context, long userId) {
        if (userId <= 0) return;
        SharedPreferences preferences = preferences(context, userId);
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        for (String id : ids) {
            String json = preferences.getString(KEY_PREFIX + id, null);
            if (json == null) continue;
            try {
                Reminder reminder = GSON.fromJson(json, Reminder.class);
                if (reminder != null) cancelAlarmOnly(context, reminder);
            } catch (RuntimeException exception) {
                Log.w("ReminderManager", "Ignoring invalid reminder during cleanup", exception);
            }
        }
        preferences.edit().clear().commit();
    }

    private static void cancelAlarmOnly(Context context, Reminder reminder) {
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null) return;
        Intent intent = new Intent(context, ReminderReceiver.class);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE;
        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                context, (int) reminder.getId(), intent, flags);
        alarmManager.cancel(pendingIntent);
        pendingIntent.cancel();
    }

    private static SharedPreferences preferences(Context context, long userId) {
        return context.getApplicationContext().getSharedPreferences(
                STORE_NAME + "_user_" + userId,
                Context.MODE_PRIVATE);
    }
}
