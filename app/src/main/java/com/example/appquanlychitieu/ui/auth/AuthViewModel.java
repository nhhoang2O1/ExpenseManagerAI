package com.example.appquanlychitieu.ui.auth;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;

import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.repository.AuthRepository;

public final class AuthViewModel extends AndroidViewModel {
    private final AuthRepository repository;

    public AuthViewModel(@NonNull Application application) {
        super(application);
        repository = new AuthRepository(application);
    }

    public void login(String email, String password, RemoteCallback<AuthResponseDto> callback) {
        repository.login(email, password, callback);
    }

    public void register(String name, String email, String password,
                         RemoteCallback<Void> callback) {
        repository.register(name, email, password, callback);
    }

    public void confirmRegistration(String email, String code,
                                    RemoteCallback<AuthResponseDto> callback) {
        repository.confirmRegistration(email, code, callback);
    }

    public void forgotPassword(String email, RemoteCallback<Void> callback) {
        repository.forgotPassword(email, callback);
    }

    public void resetPassword(String email, String code, String newPassword,
                              RemoteCallback<Void> callback) {
        repository.resetPassword(email, code, newPassword, callback);
    }
}
