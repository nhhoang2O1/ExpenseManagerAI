package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.remote.dto.LoginRequestDto;
import com.example.appquanlychitieu.data.remote.dto.LogoutRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ForgotPasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ResetPasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.RegisterRequestDto;
import com.example.appquanlychitieu.data.remote.dto.RegistrationConfirmationRequestDto;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class AuthRepository {
    private final ApiService apiService;

    public AuthRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void login(String email, String password, RemoteCallback<AuthResponseDto> callback) {
        enqueue(apiService.login(new LoginRequestDto(email, password)), callback);
    }

    public void register(
            String name,
            String email,
            String password,
            RemoteCallback<Void> callback) {
        enqueueVoid(apiService.register(new RegisterRequestDto(name, email, password)), callback);
    }

    public void confirmRegistration(String email, String code, RemoteCallback<AuthResponseDto> callback) {
        enqueue(apiService.confirmRegistration(new RegistrationConfirmationRequestDto(email, code)), callback);
    }

    public void logout(String refreshToken) {
        if (refreshToken == null || refreshToken.trim().isEmpty()) return;
        apiService.logout(new LogoutRequestDto(refreshToken)).enqueue(new Callback<Void>() {
            @Override public void onResponse(Call<Void> call, Response<Void> response) { }
            @Override public void onFailure(Call<Void> call, Throwable throwable) { }
        });
    }

    public void forgotPassword(String email, RemoteCallback<Void> callback) {
        enqueueVoid(apiService.forgotPassword(new ForgotPasswordRequestDto(email)), callback);
    }

    public void resetPassword(String email, String code, String newPassword,
                              RemoteCallback<Void> callback) {
        enqueueVoid(apiService.resetPassword(
                new ResetPasswordRequestDto(email, code, newPassword)), callback);
    }

    private void enqueueVoid(Call<Void> call, RemoteCallback<Void> callback) {
        call.enqueue(new Callback<Void>() {
            @Override public void onResponse(Call<Void> call, Response<Void> response) {
                if (response.isSuccessful()) callback.onSuccess(null);
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }
            @Override public void onFailure(Call<Void> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private void enqueue(Call<AuthResponseDto> call, RemoteCallback<AuthResponseDto> callback) {
        call.enqueue(new Callback<AuthResponseDto>() {
            @Override
            public void onResponse(Call<AuthResponseDto> call, Response<AuthResponseDto> response) {
                AuthResponseDto body = response.body();
                if (response.isSuccessful() && body != null
                        && body.resolvedToken() != null
                        && !body.resolvedToken().trim().isEmpty()) {
                    callback.onSuccess(body);
                } else if (response.isSuccessful()) {
                    callback.onError(new ApiError(
                            response.code(),
                            "May chu khong tra ve JWT",
                            false));
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<AuthResponseDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }
}
