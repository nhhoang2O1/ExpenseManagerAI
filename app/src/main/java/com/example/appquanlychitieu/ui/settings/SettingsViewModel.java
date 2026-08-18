package com.example.appquanlychitieu.ui.settings;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;

import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.ReportRepository;
import com.example.appquanlychitieu.data.repository.AuthRepository;
import com.example.appquanlychitieu.data.repository.AccountRepository;
import com.example.appquanlychitieu.data.remote.dto.ProfileDto;

import okhttp3.ResponseBody;

public final class SettingsViewModel extends AndroidViewModel {
    private final ReportRepository reports;
    private final AuthRepository auth;
    private final AccountRepository account;

    public SettingsViewModel(@NonNull Application application) {
        super(application);
        reports = new ReportRepository(application);
        auth = new AuthRepository(application);
        account = new AccountRepository(application);
    }

    public void export(
            String from,
            String to,
            String format,
            RemoteCallback<ResponseBody> callback) {
        reports.download(from, to, format, callback);
    }

    public void logout(String refreshToken) { auth.logout(refreshToken); }
    public void getProfile(RemoteCallback<ProfileDto> callback) { account.getProfile(callback); }
    public void updateProfile(String name, RemoteCallback<ProfileDto> callback) {
        account.updateProfile(name, callback);
    }

    public void updateFinancialCycle(int startDay, RemoteCallback<ProfileDto> callback) {
        account.updateFinancialCycle(startDay, callback);
    }
    public void changePassword(String current, String next, RemoteCallback<Void> callback) {
        account.changePassword(current, next, callback);
    }
    public void requestEmailChange(String email, String password, RemoteCallback<Void> callback) {
        account.requestEmailChange(email, password, callback);
    }
    public void confirmEmailChange(String code, RemoteCallback<ProfileDto> callback) {
        account.confirmEmailChange(code, callback);
    }
    public void deleteAccount(String password, RemoteCallback<Void> callback) {
        account.deleteAccount(password, callback);
    }
}
