package com.example.appquanlychitieu.ui.settings;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.fragment.app.Fragment;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.ui.auth.LoginActivity;
import com.example.appquanlychitieu.ui.reminder.ReminderActivity;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.util.ThemeManager;
import com.google.android.material.switchmaterial.SwitchMaterial;

import java.io.IOException;
import java.io.OutputStream;
import java.text.SimpleDateFormat;
import java.util.Calendar;
import java.util.Locale;

import okhttp3.ResponseBody;
import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class SettingsFragment extends Fragment {
    private final java.util.concurrent.ExecutorService ioExecutor =
            java.util.concurrent.Executors.newSingleThreadExecutor();
    private SessionManager sessionManager;
    private ActivityResultLauncher<String> exportReportLauncher;
    
    private View cardExportReport, cardLogout, cardReminders;
    private SwitchMaterial switchDarkMode;

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        exportReportLauncher = registerForActivityResult(
                new ActivityResultContracts.CreateDocument(
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
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

        cardExportReport = view.findViewById(R.id.card_export_report);
        cardLogout = view.findViewById(R.id.card_logout);
        cardReminders = view.findViewById(R.id.card_reminders);
        switchDarkMode = view.findViewById(R.id.switch_dark_mode);
        android.widget.TextView tvUsername = view.findViewById(R.id.tv_username);
        android.widget.TextView tvEmail = view.findViewById(R.id.tv_email);

        tvUsername.setText(sessionManager.getUserName());
        tvEmail.setText(sessionManager.getUserEmail());

        switchDarkMode.setChecked(ThemeManager.isDarkMode(requireContext()));
        switchDarkMode.setOnCheckedChangeListener((buttonView, isChecked) -> {
            ThemeManager.setDarkMode(requireContext(), isChecked);
        });

        cardExportReport.setOnClickListener(v -> {
            String fileName = "bao_cao_chi_tieu_"
                    + new SimpleDateFormat("yyyyMMdd_HHmm", Locale.US).format(new java.util.Date())
                    + ".xlsx";
            exportReportLauncher.launch(fileName);
        });

        cardReminders.setOnClickListener(v ->
                startActivity(new Intent(requireContext(), ReminderActivity.class)));

        cardLogout.setOnClickListener(v -> {
            new AlertDialog.Builder(requireContext())
                    .setTitle(R.string.logout)
                    .setMessage(R.string.confirm_logout)
                    .setPositiveButton(R.string.yes, (dialog, which) -> {
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

    private void exportReport(Uri uri) {
        android.content.Context appContext = requireContext().getApplicationContext();
        String successMessage = getString(R.string.export_report_success);
        String errorMessage = getString(R.string.export_report_error);
        Calendar calendar = Calendar.getInstance();
        ApiService apiService = ApiClient.getService(appContext);
        apiService.downloadMonthlyReport(
                calendar.get(Calendar.YEAR),
                calendar.get(Calendar.MONTH) + 1)
                .enqueue(new Callback<ResponseBody>() {
                    @Override
                    public void onResponse(
                            @NonNull Call<ResponseBody> call,
                            @NonNull Response<ResponseBody> response) {
                        if (!response.isSuccessful() || response.body() == null) {
                            showToast(errorMessage);
                            return;
                        }

                        ioExecutor.execute(() -> {
                            try (OutputStream outputStream =
                                         appContext.getContentResolver().openOutputStream(uri)) {
                                if (outputStream == null) {
                                    throw new IOException("Cannot open report output stream");
                                }
                                outputStream.write(response.body().bytes());
                                showToast(successMessage);
                            } catch (Exception e) {
                                showToast(errorMessage);
                            }
                        });
                    }

                    @Override
                    public void onFailure(
                            @NonNull Call<ResponseBody> call,
                            @NonNull Throwable throwable) {
                        showToast(errorMessage);
                    }
                });
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
