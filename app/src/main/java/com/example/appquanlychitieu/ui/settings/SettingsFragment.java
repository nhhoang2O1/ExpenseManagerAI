package com.example.appquanlychitieu.ui.settings;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.InputFilter;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.app.DatePickerDialog;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
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
import com.google.android.material.switchmaterial.SwitchMaterial;

import java.io.IOException;
import java.io.OutputStream;
import java.text.SimpleDateFormat;
import java.util.Calendar;
import java.util.Locale;

import okhttp3.ResponseBody;

public class SettingsFragment extends Fragment {
    private final java.util.concurrent.ExecutorService ioExecutor =
            java.util.concurrent.Executors.newSingleThreadExecutor();
    private SessionManager sessionManager;
    private ActivityResultLauncher<String> exportReportLauncher;
    private SettingsViewModel viewModel;
    private int exportYear;
    private int exportMonth;
    private String exportFormat = "xlsx";
    
    private View cardExportReport, cardLogout, cardReminders;
    private SwitchMaterial switchDarkMode;
    private TextView tvUsername, tvEmail;

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        exportReportLauncher = registerForActivityResult(
                new ActivityResultContracts.CreateDocument("*/*"),
                uri -> {
                    if (uri != null) {
                        exportReport(uri);
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
        String[] actions = {"Sửa tên", "Đổi mật khẩu", "Đổi email", "Xóa tài khoản"};
        new AlertDialog.Builder(requireContext()).setTitle("Tài khoản")
                .setItems(actions, (dialog, which) -> {
                    if (which == 0) editName();
                    else if (which == 1) changePassword();
                    else if (which == 2) changeEmail();
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

    private void changeEmail() {
        EditText email = input("Email mới", false);
        EditText password = input("Mật khẩu hiện tại", true);
        new AlertDialog.Builder(requireContext()).setTitle("Đổi email")
                .setView(form(email, password)).setPositiveButton("Gửi mã", (d, w) ->
                        viewModel.requestEmailChange(email.getText().toString().trim(),
                                password.getText().toString(), new RemoteCallback<Void>() {
                                    @Override public void onSuccess(Void value) { confirmEmailCode(); }
                                    @Override public void onError(ApiError error) { showToast(error.getMessage()); }
                                }))
                .setNegativeButton(R.string.cancel, null).show();
    }

    private void confirmEmailCode() {
        EditText code = input("Mã xác nhận 6 số", false);
        code.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);
        code.setFilters(new InputFilter[]{new InputFilter.LengthFilter(6)});
        AlertDialog dialog = new AlertDialog.Builder(requireContext()).setTitle("Xác nhận email").setView(code)
                .setPositiveButton(R.string.save, (d, w) -> viewModel.confirmEmailChange(
                        code.getText().toString().trim(), profileCallback()))
                .setNegativeButton(R.string.cancel, null).create();
        dialog.setOnShowListener(ignored -> {
            android.widget.Button save = dialog.getButton(AlertDialog.BUTTON_POSITIVE);
            Runnable updateButtonState = () -> save.setEnabled(
                    code.getText() != null && code.getText().toString().matches("\\d{6}"));
            updateButtonState.run();
            code.addTextChangedListener(new TextWatcher() {
                @Override public void beforeTextChanged(CharSequence value, int start, int count, int after) { }

                @Override public void onTextChanged(CharSequence value, int start, int before, int count) {
                    updateButtonState.run();
                }

                @Override public void afterTextChanged(Editable value) { }
            });
        });
        dialog.show();
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

    private void exportReport(Uri uri) {
        android.content.Context appContext = requireContext().getApplicationContext();
        String successMessage = getString(R.string.export_report_success);
        String errorMessage = getString(R.string.export_report_error);
        viewModel.export(exportYear, exportMonth, exportFormat,
                new RemoteCallback<ResponseBody>() {
                    @Override
                    public void onSuccess(ResponseBody body) {
                        ioExecutor.execute(() -> {
                            try (OutputStream outputStream =
                                         appContext.getContentResolver().openOutputStream(uri)) {
                                if (outputStream == null) {
                                    throw new IOException("Cannot open report output stream");
                                }
                                outputStream.write(body.bytes());
                                showToast(successMessage);
                            } catch (Exception e) {
                                showToast(errorMessage);
                            }
                        });
                    }

                    @Override
                    public void onError(ApiError error) {
                        showToast(errorMessage);
                    }
                });
    }

    private void chooseReport() {
        String[] formats = {"XLSX", "CSV", "PDF"};
        new AlertDialog.Builder(requireContext())
                .setTitle("Chá»n Ä‘á»‹nh dáº¡ng")
                .setItems(formats, (dialog, which) -> {
                    exportFormat = formats[which].toLowerCase(Locale.US);
                    Calendar now = Calendar.getInstance();
                    new DatePickerDialog(requireContext(), (picker, year, month, day) -> {
                        exportYear = year;
                        exportMonth = month + 1;
                        exportReportLauncher.launch(String.format(
                                Locale.US,
                                "bao_cao_%04d_%02d.%s",
                                exportYear,
                                exportMonth,
                                exportFormat));
                    }, now.get(Calendar.YEAR), now.get(Calendar.MONTH), 1).show();
                })
                .show();
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
