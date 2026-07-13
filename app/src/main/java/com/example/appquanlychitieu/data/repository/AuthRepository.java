package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.remote.dto.LoginRequestDto;
import com.example.appquanlychitieu.data.remote.dto.RegisterRequestDto;

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
            RemoteCallback<AuthResponseDto> callback) {
        enqueue(apiService.register(new RegisterRequestDto(name, email, password)), callback);
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
