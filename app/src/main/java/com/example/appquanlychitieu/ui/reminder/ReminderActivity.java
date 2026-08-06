package com.example.appquanlychitieu.ui.reminder;

import android.app.Dialog;
import android.app.TimePickerDialog;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.lifecycle.ViewModelProvider;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Reminder;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.receiver.ReminderManager;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.example.appquanlychitieu.util.SessionManager;
import com.google.android.material.appbar.MaterialToolbar;
import com.google.android.material.bottomsheet.BottomSheetDialog;
import com.google.android.material.snackbar.Snackbar;

import java.util.Locale;

public class ReminderActivity extends AppCompatActivity {
    private static final int NOTIFICATION_PERMISSION_CODE = 1001;

    private ReminderViewModel viewModel;
    private SessionManager sessionManager;
    private ReminderAdapter adapter;

    private RecyclerView rvReminders;
    private View tvEmpty;

    private TextView tvDialogTitle;
    private TextView tvSelectedTime;
    private EditText etContent;
    private EditText etDay;
    private Button btnCancel;
    private Button btnSave;

    private int selectedHour = 8;
    private int selectedMinute = 0;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_reminder);
        EdgeToEdgeHelper.applySystemBars(findViewById(R.id.root_reminders));

        sessionManager = new SessionManager(this);
        viewModel = new ViewModelProvider(this).get(ReminderViewModel.class);

        MaterialToolbar toolbar = findViewById(R.id.toolbar);
        toolbar.setNavigationOnClickListener(v -> finish());

        rvReminders = findViewById(R.id.rv_reminders);
        tvEmpty = findViewById(R.id.tv_empty);

        adapter = new ReminderAdapter();
        rvReminders.setAdapter(adapter);

        viewModel.getReminders(sessionManager.getUserId()).observe(this, reminders -> {
            adapter.setReminders(reminders);
            boolean empty = reminders == null || reminders.isEmpty();
            tvEmpty.setVisibility(empty ? View.VISIBLE : View.GONE);
            rvReminders.setVisibility(empty ? View.GONE : View.VISIBLE);
        });

        adapter.setOnReminderClickListener(new ReminderAdapter.OnReminderClickListener() {
            @Override
            public void onReminderClick(Reminder reminder) {
                showAddEditDialog(reminder);
            }

            @Override
            public void onReminderLongClick(Reminder reminder) {
                confirmDelete(reminder);
            }

            @Override
            public void onReminderSwitchToggle(Reminder reminder, boolean isChecked) {
                boolean previous = reminder.isActive();
                reminder.setActive(isChecked);
                viewModel.update(reminder, new RemoteCallback<Reminder>() {
                    @Override
                    public void onSuccess(Reminder saved) {
                        runOnUiThread(() -> {
                            if (saved.isActive()) {
                                checkNotificationPermission();
                                ReminderManager.scheduleReminder(ReminderActivity.this, saved);
                                Snackbar.make(rvReminders, R.string.reminder_enabled, Snackbar.LENGTH_SHORT).show();
                            } else {
                                ReminderManager.cancelReminder(ReminderActivity.this, saved);
                                Snackbar.make(rvReminders, R.string.reminder_disabled, Snackbar.LENGTH_SHORT).show();
                            }
                        });
                    }

                    @Override
                    public void onError(ApiError error) {
                        reminder.setActive(previous);
                        runOnUiThread(() -> {
                            adapter.notifyReminderChanged(reminder);
                            Toast.makeText(ReminderActivity.this, error.getMessage(), Toast.LENGTH_SHORT).show();
                        });
                    }
                });
            }

            @Override
            public void onDeleteClick(Reminder reminder) {
                confirmDelete(reminder);
            }
        });

        findViewById(R.id.fab_add_reminder).setOnClickListener(v -> showAddEditDialog(null));
    }

    private void confirmDelete(Reminder reminder) {
        new AlertDialog.Builder(this)
                .setTitle(R.string.delete_reminder)
                .setMessage(R.string.confirm_delete_reminder)
                .setPositiveButton(R.string.delete, (dialog, which) -> {
                    viewModel.delete(reminder, new RemoteCallback<Void>() {
                        @Override
                        public void onSuccess(Void value) {
                            runOnUiThread(() -> {
                                ReminderManager.cancelReminder(ReminderActivity.this, reminder);
                                Snackbar.make(rvReminders, R.string.reminder_deleted, Snackbar.LENGTH_SHORT).show();
                            });
                        }

                        @Override
                        public void onError(ApiError error) {
                            runOnUiThread(() -> Toast.makeText(
                                    ReminderActivity.this, error.getMessage(), Toast.LENGTH_SHORT).show());
                        }
                    });
                })
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    private void checkNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                ActivityCompat.checkSelfPermission(this, android.Manifest.permission.POST_NOTIFICATIONS)
                        != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(
                    this,
                    new String[]{android.Manifest.permission.POST_NOTIFICATIONS},
                    NOTIFICATION_PERMISSION_CODE);
        }
    }

    private void showAddEditDialog(Reminder reminder) {
        Dialog dialog = new BottomSheetDialog(this);
        dialog.setContentView(R.layout.dialog_add_reminder);
        if (dialog.getWindow() != null) {
            dialog.getWindow().setLayout(
                    android.view.ViewGroup.LayoutParams.MATCH_PARENT,
                    android.view.ViewGroup.LayoutParams.WRAP_CONTENT);
            dialog.getWindow().setSoftInputMode(
                    android.view.WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        }

        tvDialogTitle = dialog.findViewById(R.id.tv_dialog_title);
        etContent = dialog.findViewById(R.id.et_content);
        etDay = dialog.findViewById(R.id.et_day);
        tvSelectedTime = dialog.findViewById(R.id.tv_selected_time);
        btnCancel = dialog.findViewById(R.id.btn_cancel);
        btnSave = dialog.findViewById(R.id.btn_save);

        if (reminder != null) {
            tvDialogTitle.setText(R.string.edit_reminder);
            etContent.setText(reminder.getContent());
            etDay.setText(String.valueOf(reminder.getDayOfMonth()));
            selectedHour = reminder.getHour();
            selectedMinute = reminder.getMinute();
        } else {
            tvDialogTitle.setText(R.string.add_reminder);
            selectedHour = 8;
            selectedMinute = 0;
        }

        tvSelectedTime.setText(String.format(Locale.ROOT,
                "%02d:%02d", selectedHour, selectedMinute));
        tvSelectedTime.setOnClickListener(v -> new TimePickerDialog(
                this,
                (view, hourOfDay, minute) -> {
                    selectedHour = hourOfDay;
                    selectedMinute = minute;
                    tvSelectedTime.setText(String.format(Locale.ROOT,
                            "%02d:%02d", selectedHour, selectedMinute));
                },
                selectedHour,
                selectedMinute,
                true).show());

        btnCancel.setOnClickListener(v -> dialog.dismiss());
        btnSave.setOnClickListener(v -> saveReminder(dialog, reminder));

        dialog.show();
    }

    private void saveReminder(Dialog dialog, Reminder reminder) {
        String content = etContent.getText().toString().trim();
        String dayStr = etDay.getText().toString().trim();

        if (TextUtils.isEmpty(content)) {
            etContent.setError(getString(R.string.reminder_content_required));
            return;
        }
        if (TextUtils.isEmpty(dayStr)) {
            etDay.setError(getString(R.string.reminder_day_required));
            return;
        }

        int day;
        try {
            day = Integer.parseInt(dayStr);
        } catch (NumberFormatException exception) {
            etDay.setError(getString(R.string.reminder_day_invalid));
            return;
        }
        if (day < 1 || day > 31) {
            etDay.setError(getString(R.string.reminder_day_invalid));
            return;
        }

        if (reminder == null) {
            Reminder newReminder = new Reminder(content, day, selectedHour, selectedMinute, sessionManager.getUserId(), true);
            viewModel.insert(newReminder, new RemoteCallback<Reminder>() {
                @Override
                public void onSuccess(Reminder value) {
                    runOnUiThread(() -> {
                        ReminderManager.scheduleReminder(ReminderActivity.this, value);
                        checkNotificationPermission();
                        Snackbar.make(rvReminders, R.string.reminder_added, Snackbar.LENGTH_SHORT).show();
                    });
                }

                @Override
                public void onError(ApiError error) {
                    runOnUiThread(() -> Toast.makeText(ReminderActivity.this, error.getMessage(), Toast.LENGTH_SHORT).show());
                }
            });
        } else {
            reminder.setContent(content);
            reminder.setDayOfMonth(day);
            reminder.setHour(selectedHour);
            reminder.setMinute(selectedMinute);
            viewModel.update(reminder, new RemoteCallback<Reminder>() {
                @Override
                public void onSuccess(Reminder saved) {
                    runOnUiThread(() -> {
                        if (saved.isActive()) ReminderManager.scheduleReminder(ReminderActivity.this, saved);
                        else ReminderManager.cancelReminder(ReminderActivity.this, saved);
                        Snackbar.make(rvReminders, R.string.reminder_updated, Snackbar.LENGTH_SHORT).show();
                    });
                }

                @Override
                public void onError(ApiError error) {
                    runOnUiThread(() -> Toast.makeText(
                            ReminderActivity.this, error.getMessage(), Toast.LENGTH_SHORT).show());
                    viewModel.refresh(sessionManager.getUserId());
                }
            });
        }

        dialog.dismiss();
    }
}
