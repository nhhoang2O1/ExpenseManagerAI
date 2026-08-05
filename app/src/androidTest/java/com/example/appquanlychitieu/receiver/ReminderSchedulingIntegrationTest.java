package com.example.appquanlychitieu.receiver;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;

@RunWith(AndroidJUnit4.class)
public class ReminderSchedulingIntegrationTest {
    private static final long USER_ID = 91_001L;
    private static final long REMINDER_ID = 81_001L;

    private Context context;

    @Before
    public void setUp() {
        context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        preferences(USER_ID).edit().clear().commit();
        preferences(-1).edit().clear().commit();
    }

    @After
    public void tearDown() {
        ReminderManager.clearForUser(context, USER_ID);
        preferences(-1).edit().clear().commit();
    }

    @Test
    public void receiverReschedulesDeliveredReminderForItsOwnerOnly() {
        Intent delivered = new Intent(context, ReminderReceiver.class)
                .putExtra("reminder_id", REMINDER_ID)
                .putExtra("reminder_content", "Thanh toán tiền điện")
                .putExtra("reminder_day", 31)
                .putExtra("reminder_hour", 8)
                .putExtra("reminder_minute", 0)
                .putExtra("reminder_active", true)
                .putExtra("reminder_user_id", USER_ID);

        new ReminderReceiver().onReceive(context, delivered);

        assertTrue(preferences(USER_ID).getStringSet("ids", java.util.Collections.emptySet())
                .contains(Long.toString(REMINDER_ID)));
        assertFalse(preferences(-1).contains("reminder_" + REMINDER_ID));
    }

    @Test
    public void invalidOwnerIsNeverPersistedUnderUserMinusOne() {
        com.example.appquanlychitieu.data.model.Reminder reminder =
                new com.example.appquanlychitieu.data.model.Reminder(
                        "Invalid owner", 5, 8, 0, -1, true);
        reminder.setId(REMINDER_ID);

        ReminderManager.scheduleReminder(context, reminder);

        assertFalse(preferences(-1).contains("reminder_" + REMINDER_ID));
        assertTrue(preferences(-1).getStringSet("ids", java.util.Collections.emptySet()).isEmpty());
    }

    private SharedPreferences preferences(long userId) {
        return context.getSharedPreferences(
                "scheduled_reminders_user_" + userId,
                Context.MODE_PRIVATE);
    }
}
