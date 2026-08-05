package com.example.appquanlychitieu.receiver;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.example.appquanlychitieu.data.model.Reminder;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.util.Collections;

@RunWith(AndroidJUnit4.class)
public class ReminderUserIsolationIntegrationTest {
    private static final long USER_A = 92_001L;
    private static final long USER_B = 92_002L;

    private Context context;

    @Before
    public void setUp() {
        context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        preferences(USER_A).edit().clear().commit();
        preferences(USER_B).edit().clear().commit();
    }

    @After
    public void tearDown() {
        ReminderManager.clearForUser(context, USER_A);
        ReminderManager.clearForUser(context, USER_B);
    }

    @Test
    public void clearingUserADoesNotRemoveUserBReminder() {
        Reminder reminderA = reminder(82_001L, USER_A, "Hóa đơn của A");
        Reminder reminderB = reminder(82_002L, USER_B, "Hóa đơn của B");
        ReminderManager.scheduleReminder(context, reminderA);
        ReminderManager.scheduleReminder(context, reminderB);

        ReminderManager.clearForUser(context, USER_A);

        assertFalse(ids(USER_A).contains(Long.toString(reminderA.getId())));
        assertTrue(ids(USER_B).contains(Long.toString(reminderB.getId())));
    }

    private Reminder reminder(long reminderId, long userId, String content) {
        Reminder reminder = new Reminder(content, 20, 8, 0, userId, true);
        reminder.setId(reminderId);
        return reminder;
    }

    private java.util.Set<String> ids(long userId) {
        return preferences(userId).getStringSet("ids", Collections.emptySet());
    }

    private SharedPreferences preferences(long userId) {
        return context.getSharedPreferences(
                "scheduled_reminders_user_" + userId,
                Context.MODE_PRIVATE);
    }
}
