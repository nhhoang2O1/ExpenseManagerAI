package com.example.appquanlychitieu.receiver;

import android.content.Context;

import com.example.appquanlychitieu.data.model.Reminder;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteReminderRepository;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.List;

/** Reconciles the per-user alarm store with the authoritative backend list. */
public final class ReminderSync {
    private ReminderSync() {}

    public static void sync(Context context, Runnable finished) {
        Context app = context.getApplicationContext();
        SessionManager session = new SessionManager(app);
        long userId = session.getUserId();
        if (userId <= 0 || !session.hasAuthToken()) {
            ReminderManager.reschedulePersisted(app);
            if (finished != null) finished.run();
            return;
        }

        new RemoteReminderRepository(app).getReminders(userId, new RemoteCallback<List<Reminder>>() {
            @Override
            public void onSuccess(List<Reminder> reminders) {
                ReminderManager.clearForUser(app, userId);
                if (reminders != null) {
                    for (Reminder reminder : reminders) {
                        if (reminder.isActive()) ReminderManager.scheduleReminder(app, reminder);
                    }
                }
                if (finished != null) finished.run();
            }

            @Override
            public void onError(ApiError error) {
                ReminderManager.reschedulePersisted(app);
                if (finished != null) finished.run();
            }
        });
    }
}
