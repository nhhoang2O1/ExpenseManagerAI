package com.example.appquanlychitieu.ui.settings;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.app.DatePickerDialog;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts.StartActivityForResult;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.ui.auth.LoginActivity;
import com.example.appquanlychitieu.ui.reminder.ReminderActivity;
import com.example.appquanlychitieu.ui.category.CategoryActivity;
import com.example.appquanlychitieu.receiver.ReminderManager;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.ProfileDto;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.util.ThemeManager;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.switchmaterial.SwitchMaterial;

import java.io.IOException;
import java.io.OutputStream;
import java.util.Calendar;
import java.util.Locale;

import okhttp3.ResponseBody;

public class SettingsFragment extends Fragment {
    private final java.util.concurrent.ExecutorService ioExecutor =
            java.util.concurrent.Executors.newSingleThreadExecutor();
    private SessionManager sessionManager;
    private ActivityResultLauncher<Intent> exportReportLauncher;
    private SettingsViewModel viewModel;
    private String exportFromDate;
    private String exportToDate;
    private String exportFormat = "xlsx";
    private byte[] pendingReport;
    
    private View cardExportReport, cardLogout, cardReminders;
    private SwitchMaterial switchDarkMode;
    private TextView tvUsername, tvEmail;

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        exportReportLauncher = registerForActivityResult(
                new StartActivityForResult(),
                result -> {
                    Intent data = result.getData();
                    if (result.getResultCode() == Activity.RESULT_OK
                            && data != null && data.getData() != null) {
                        saveReport(data.getData());
                    } else {
                        pendingReport = null;
                    }
                });
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_settings, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        sessionManager = new SessionManager(requireContext());
        viewModel = new ViewModelProvider(this).get(SettingsViewModel.class);

        cardExportReport = view.findViewById(R.id.card_export_report);
        cardLogout = view.findViewById(R.id.card_logout);
        cardReminders = view.findViewById(R.id.card_reminders);
        switchDarkMode = view.findViewById(R.id.switch_dark_mode);
        tvUsername = view.findViewById(R.id.tv_username);
        tvEmail = view.findViewById(R.id.tv_email);

        tvUsername.setText(sessionManager.getUserName());
        tvEmail.setText(sessionManager.getUserEmail());
        view.findViewById(R.id.card_profile).setOnClickListener(v -> showAccountActions());
        refreshProfile();

        switchDarkMode.setChecked(ThemeManager.isDarkMode(requireContext()));
        switchDarkMode.setOnCheckedChangeListener((buttonView, isChecked) -> {
            ThemeManager.setDarkMode(requireContext(), isChecked);
        });

        cardExportReport.setOnClickListener(v -> chooseReport());

        cardReminders.setOnClickListener(v ->
                startActivity(new Intent(requireContext(), ReminderActivity.class)));
        view.findViewById(R.id.card_categories).setOnClickListener(v ->
                startActivity(new Intent(requireContext(), CategoryActivity.class)));

        cardLogout.setOnClickListener(v -> {
            new AlertDialog.Builder(requireContext())
                    .setTitle(R.string.logout)
                    .setMessage(R.string.confirm_logout)
                    .setPositiveButton(R.string.yes, (dialog, which) -> {
                        long userId = sessionManager.getUserId();
                        String refreshToken = sessionManager.getRefreshToken();
                        // Remote logout is best effort; local cleanup must always complete.
                        viewModel.logout(refreshToken);
                        ReminderManager.clearForUser(requireContext(), userId);
                        sessionManager.logout();
                        Intent intent = new Intent(requireContext(), LoginActivity.class);
                        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                        startActivity(intent);
                        requireActivity().finish();
                    })
                    .setNegativeButton(R.string.no, null)
                    .show();
        });
    }

    private void refreshProfile() {
        viewModel.getProfile(new RemoteCallback<ProfileDto>() {
            @Override public void onSuccess(ProfileDto value) { applyProfile(value); }
            @Override public void onError(ApiError error) { /* cached identity remains visible */ }
        });
    }

    private void applyProfile(ProfileDto profile) {
        if (!isAdded() || profile == null) return;
        sessionManager.updateIdentity(profile.name, profile.email);
        tvUsername.setText(profile.name);
        tvEmail.setText(profile.email);
    }

    private void showAccountActions() {
        String[] actions = {"Sửa tên", "Đổi mật khẩu", "Xóa tài khoản"};
        new AlertDialog.Builder(requireContext()).setTitle("Tài khoản")
                .setItems(actions, (dialog, which) -> {
                    if (which == 0) editName();
                    else if (which == 1) changePassword();
                    else deleteAccount();
                }).show();
    }

    private EditText input(String hint, boolean password) {
        EditText value = new EditText(requireContext());
        value.setHint(hint);
        int padding = Math.round(12 * getResources().getDisplayMetrics().density);
        value.setPadding(padding, padding, padding, padding);
        if (password) value.setInputType(android.text.InputType.TYPE_CLASS_TEXT |
                android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD);
        return value;
    }

    private LinearLayout form(EditText... fields) {
        LinearLayout layout = new LinearLayout(requireContext());
        layout.setOrientation(LinearLayout.VERTICAL);
        int padding = Math.round(24 * getResources().getDisplayMetrics().density);
        layout.setPadding(padding, 0, padding, 0);
        for (EditText field : fields) layout.addView(field);
        return layout;
    }

    private void editName() {
        EditText name = input("Tên hiển thị", false);
        name.setText(sessionManager.getUserName());
        new AlertDialog.Builder(requireContext()).setTitle("Sửa hồ sơ").setView(name)
                .setPositiveButton(R.string.save, (d, w) -> viewModel.updateProfile(
                        name.getText().toString().trim(), profileCallback()))
                .setNegativeButton(R.string.cancel, null).show();
    }

    private void changePassword() {
        EditText current = input("Mật khẩu hiện tại", true);
        EditText next = input("Mật khẩu mới", true);
        new AlertDialog.Builder(requireContext()).setTitle("Đổi mật khẩu")
                .setView(form(current, next)).setPositiveButton(R.string.save, (d, w) ->
                        viewModel.changePassword(current.getText().toString(), next.getText().toString(),
                                actionCallback("Đã đổi mật khẩu. Vui lòng đăng nhập lại.", true)))
                .setNegativeButton(R.string.cancel, null).show();
    }

    private void deleteAccount() {
        EditText password = input("Nhập mật khẩu để xác nhận", true);
        new AlertDialog.Builder(requireContext()).setTitle("Xóa tài khoản")
                .setMessage("Dữ liệu sẽ bị xóa vĩnh viễn.").setView(password)
                .setPositiveButton(R.string.delete, (d, w) -> viewModel.deleteAccount(
                        password.getText().toString(), actionCallback("Đã xóa tài khoản", true)))
                .setNegativeButton(R.string.cancel, null).show();
    }

    private RemoteCallback<ProfileDto> profileCallback() {
        return new RemoteCallback<ProfileDto>() {
            @Override public void onSuccess(ProfileDto value) {
                applyProfile(value);
                showToast("Đã cập nhật tài khoản");
            }
            @Override public void onError(ApiError error) { showToast(error.getMessage()); }
        };
    }

    private RemoteCallback<Void> actionCallback(String message, boolean endSession) {
        return new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) {
                showToast(message);
                if (endSession) finishSession();
            }
            @Override public void onError(ApiError error) { showToast(error.getMessage()); }
        };
    }

    private void finishSession() {
        ReminderManager.clearForUser(requireContext(), sessionManager.getUserId());
        sessionManager.logout();
        Intent intent = new Intent(requireContext(), LoginActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        requireActivity().finish();
    }

    private void downloadReport() {
        String errorMessage = getString(R.string.export_report_error);
        viewModel.export(exportFromDate, exportToDate, exportFormat,
                new RemoteCallback<ResponseBody>() {
                    @Override
                    public void onSuccess(ResponseBody body) {
                        if (ioExecutor.isShutdown()) {
                            body.close();
                            return;
                        }
                        ioExecutor.execute(() -> {
                            try {
                                pendingReport = body.bytes();
                                new Handler(Looper.getMainLooper()).post(() -> {
                                    if (!isAdded() || pendingReport == null) return;
                                    Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT)
                                            .addCategory(Intent.CATEGORY_OPENABLE)
                                            .setType("pdf".equalsIgnoreCase(exportFormat)
                                                    ? "application/pdf"
                                                    : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                                            .putExtra(Intent.EXTRA_TITLE, String.format(
                                                    Locale.US, "bao_cao_%s_%s.%s",
                                                    exportFromDate, exportToDate, exportFormat));
                                    exportReportLauncher.launch(intent);
                                });
                            } catch (Exception e) {
                                pendingReport = null;
                                showToast(errorMessage);
                            }
                        });
                    }

                    @Override
                    public void onError(ApiError error) {
                        String detail = error == null ? null : error.getMessage();
                        showToast(detail == null || detail.trim().isEmpty()
                                ? errorMessage : detail);
                    }
                });
    }

    private void saveReport(Uri uri) {
        android.content.Context appContext = requireContext().getApplicationContext();
        byte[] report = pendingReport;
        pendingReport = null;
        if (report == null) {
            showToast(getString(R.string.export_report_error));
            return;
        }
        String successMessage = getString(R.string.export_report_success);
        String errorMessage = getString(R.string.export_report_error);
        ioExecutor.execute(() -> {
            try (OutputStream outputStream = appContext.getContentResolver().openOutputStream(uri)) {
                if (outputStream == null) throw new IOException("Cannot open report output stream");
                outputStream.write(report);
                showToast(successMessage);
            } catch (Exception exception) {
                showToast(errorMessage);
            }
        });
    }

    private void chooseReport() {
        String[] formats = {"XLSX", "PDF"};
        int checkedItem = Math.max(0, java.util.Arrays.asList(formats)
                .indexOf(exportFormat.toUpperCase(Locale.US)));
        final int[] selectedItem = {checkedItem};

        android.widget.RadioGroup formatGroup = new android.widget.RadioGroup(requireContext());
        formatGroup.setOrientation(android.widget.RadioGroup.VERTICAL);
        int optionPadding = (int) (8 * getResources().getDisplayMetrics().density);
        formatGroup.setPadding(optionPadding, 0, optionPadding, 0);
        for (int index = 0; index < formats.length; index++) {
            android.widget.RadioButton option = new android.widget.RadioButton(requireContext());
            option.setText(formats[index]);
            option.setTextSize(16);
            option.setPadding(optionPadding, optionPadding, optionPadding, optionPadding);
            option.setId(android.view.View.generateViewId());
            int optionIndex = index;
            option.setOnClickListener(v -> selectedItem[0] = optionIndex);
            formatGroup.addView(option,
                    new android.widget.RadioGroup.LayoutParams(
                            android.view.ViewGroup.LayoutParams.MATCH_PARENT,
                            android.view.ViewGroup.LayoutParams.WRAP_CONTENT));
            if (index == checkedItem) option.setChecked(true);
        }

        new AlertDialog.Builder(requireContext())
                .setTitle(R.string.choose_report_format)
                .setMessage(R.string.choose_report_format_description)
                .setView(formatGroup)
                .setNegativeButton(R.string.cancel, null)
                .setPositiveButton(R.string.continue_action, (dialog, which) -> {
                    exportFormat = formats[selectedItem[0]].toLowerCase(Locale.US);
                    showReportDateRangePicker();
                })
                .show();
    }

    private void showReportDateRangePicker() {
        Calendar now = Calendar.getInstance();
        Calendar firstOfMonth = (Calendar) now.clone();
        firstOfMonth.set(Calendar.DAY_OF_MONTH, 1);
        showReportFromPicker(firstOfMonth, now);
    }

    private void showReportFromPicker(Calendar defaultDate, Calendar today) {
        DatePickerDialog picker = new DatePickerDialog(requireContext(), (dialog, year, month, day) -> {
            Calendar from = Calendar.getInstance();
            from.set(year, month, day, 0, 0, 0);
            showReportToPicker(from, today);
        }, defaultDate.get(Calendar.YEAR), defaultDate.get(Calendar.MONTH),
                defaultDate.get(Calendar.DAY_OF_MONTH));
        picker.setTitle("Chọn ngày bắt đầu");
        picker.getDatePicker().setMaxDate(today.getTimeInMillis());
        picker.show();
    }

    private void showReportToPicker(Calendar from, Calendar today) {
        DatePickerDialog picker = new DatePickerDialog(requireContext(), (dialog, year, month, day) -> {
            Calendar to = Calendar.getInstance();
            to.set(year, month, day, 0, 0, 0);
            if (to.before(from)) {
                showToast("Ngày kết thúc phải sau ngày bắt đầu");
                return;
            }
            exportFromDate = formatReportDate(from);
            exportToDate = formatReportDate(to);
            downloadReport();
        }, today.get(Calendar.YEAR), today.get(Calendar.MONTH), today.get(Calendar.DAY_OF_MONTH));
        picker.setTitle("Chọn ngày kết thúc");
        picker.getDatePicker().setMinDate(from.getTimeInMillis());
        picker.getDatePicker().setMaxDate(today.getTimeInMillis());
        picker.show();
    }

    private String formatReportDate(Calendar date) {
        return String.format(Locale.US, "%04d-%02d-%02d",
                date.get(Calendar.YEAR), date.get(Calendar.MONTH) + 1,
                date.get(Calendar.DAY_OF_MONTH));
    }

    private void showToast(String message) {
        new Handler(Looper.getMainLooper()).post(() -> {
            if (isAdded()) {
                Toast.makeText(requireContext(), message, Toast.LENGTH_SHORT).show();
            }
        });
    }

    @Override
    public void onDestroy() {
        ioExecutor.shutdown();
        super.onDestroy();
    }
}
