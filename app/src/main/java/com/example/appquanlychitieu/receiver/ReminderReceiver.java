package com.example.appquanlychitieu.receiver;

import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.util.Log;

import androidx.core.app.ActivityCompat;
import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;

import com.example.appquanlychitieu.MainActivity;
import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Reminder;

public class ReminderReceiver extends BroadcastReceiver {
    private static final String CHANNEL_ID = "reminder_channel";

    @Override
    public void onReceive(Context context, Intent intent) {
        long reminderId = intent.getLongExtra("reminder_id", -1);
        String content = intent.getStringExtra("reminder_content");
        int day = intent.getIntExtra("reminder_day", 1);
        int hour = intent.getIntExtra("reminder_hour", 8);
        int minute = intent.getIntExtra("reminder_minute", 0);
        boolean active = intent.getBooleanExtra("reminder_active", true);

        if (reminderId != -1 && content != null) {
            showNotification(context, (int) reminderId, content);

            if (active) {
                Reminder reminder = new Reminder(content, day, hour, minute, -1, true);
                reminder.setId(reminderId);
                ReminderManager.scheduleReminder(context, reminder);
                Log.d("ReminderReceiver", "Rescheduled reminder " + reminderId);
            }
        }
    }

    private void showNotification(Context context, int notificationId, String content) {
        createNotificationChannel(context);

        Intent intent = new Intent(context, MainActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            flags |= PendingIntent.FLAG_IMMUTABLE;
        }
        
        PendingIntent pendingIntent = PendingIntent.getActivity(context, notificationId, intent, flags);

        NotificationCompat.Builder builder = new NotificationCompat.Builder(context, CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_budget) 
                .setContentTitle("Đến hạn thanh toán")
                .setContentText(content)
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setContentIntent(pendingIntent)
                .setAutoCancel(true);

        NotificationManagerCompat notificationManager = NotificationManagerCompat.from(context);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ActivityCompat.checkSelfPermission(context, android.Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                return;
            }
        }
        
        notificationManager.notify(notificationId, builder.build());
    }

    private void createNotificationChannel(Context context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            CharSequence name = "Nhắc nhở hoá đơn";
            String description = "Kênh thông báo nhắc nhở hằng tháng";
            int importance = NotificationManager.IMPORTANCE_HIGH;
            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, name, importance);
            channel.setDescription(description);
            NotificationManager notificationManager = context.getSystemService(NotificationManager.class);
            if (notificationManager != null) {
                notificationManager.createNotificationChannel(channel);
            }
        }
    }
}
