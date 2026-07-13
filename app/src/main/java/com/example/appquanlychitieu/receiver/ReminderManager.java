package com.example.appquanlychitieu.receiver;

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

public class ReminderManager {
    private static final String STORE_NAME = "scheduled_reminders";
    private static final String KEY_IDS = "ids";
    private static final String KEY_PREFIX = "reminder_";
    private static final Gson GSON = new Gson();

    public static void scheduleReminder(Context context, Reminder reminder) {
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

        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            flags |= PendingIntent.FLAG_IMMUTABLE;
        }

        PendingIntent pendingIntent = PendingIntent.getBroadcast(
                context,
                (int) reminder.getId(),
                intent,
                flags
        );

        Calendar calendar = Calendar.getInstance();
        calendar.setTimeInMillis(System.currentTimeMillis());

        int currentDay = calendar.get(Calendar.DAY_OF_MONTH);
        int currentMonth = calendar.get(Calendar.MONTH);
        int currentYear = calendar.get(Calendar.YEAR);
        
        calendar.set(Calendar.HOUR_OF_DAY, reminder.getHour());
        calendar.set(Calendar.MINUTE, reminder.getMinute());
        calendar.set(Calendar.SECOND, 0);

        int maxDaysInCurrentMonth = calendar.getActualMaximum(Calendar.DAY_OF_MONTH);
        int targetDay = Math.min(reminder.getDayOfMonth(), maxDaysInCurrentMonth);
        calendar.set(Calendar.DAY_OF_MONTH, targetDay);

        // Nếu thời gian hẹn nằm trong quá khứ hơn 1 phút thì mới đẩy sang tháng sau
        // Còn nếu vừa mới cài trùng phút hiện tại thì cho phép chạy luôn (thông báo ngay)
        if (calendar.getTimeInMillis() < System.currentTimeMillis() - 60000) {
            calendar.add(Calendar.MONTH, 1);
            
            int maxDaysInNextMonth = calendar.getActualMaximum(Calendar.DAY_OF_MONTH);
            int targetDayNextMonth = Math.min(reminder.getDayOfMonth(), maxDaysInNextMonth);
            calendar.set(Calendar.DAY_OF_MONTH, targetDayNextMonth);
        }

        long alarmTime = calendar.getTimeInMillis();
        Log.d("ReminderManager", "Scheduled alarm for reminder " + reminder.getId() + " at " + calendar.getTime().toString());

        try {
            boolean canScheduleExact = true;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                canScheduleExact = alarmManager.canScheduleExactAlarms();
            }

            if (canScheduleExact && Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                alarmManager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
            } else if (canScheduleExact && Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
                alarmManager.setExact(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
            } else {
                alarmManager.set(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
            }
        } catch (SecurityException e) {
            Log.e("ReminderManager", "Exact alarm permission denied. Falling back to inexact.", e);
            alarmManager.set(AlarmManager.RTC_WAKEUP, alarmTime, pendingIntent);
        }
    }

    public static void cancelReminder(Context context, Reminder reminder) {
        removeSchedule(context, reminder.getId());
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null) return;

        Intent intent = new Intent(context, ReminderReceiver.class);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            flags |= PendingIntent.FLAG_IMMUTABLE;
        }

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
        SharedPreferences preferences = context.getSharedPreferences(STORE_NAME, Context.MODE_PRIVATE);
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
        SharedPreferences preferences = context.getSharedPreferences(STORE_NAME, Context.MODE_PRIVATE);
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        String id = Long.toString(reminder.getId());
        ids.add(id);
        preferences.edit()
                .putStringSet(KEY_IDS, ids)
                .putString(KEY_PREFIX + id, GSON.toJson(reminder))
                .apply();
    }

    private static void removeSchedule(Context context, long reminderId) {
        SharedPreferences preferences = context.getSharedPreferences(STORE_NAME, Context.MODE_PRIVATE);
        Set<String> ids = new HashSet<>(preferences.getStringSet(KEY_IDS, new HashSet<>()));
        String id = Long.toString(reminderId);
        ids.remove(id);
        preferences.edit()
                .putStringSet(KEY_IDS, ids)
                .remove(KEY_PREFIX + id)
                .apply();
    }
}
