package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.ChangePasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.DeleteAccountRequestDto;
import com.example.appquanlychitieu.data.remote.dto.EmailChangeConfirmRequestDto;
import com.example.appquanlychitieu.data.remote.dto.EmailChangeRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ProfileDto;
import com.example.appquanlychitieu.data.remote.dto.UpdateProfileRequestDto;
import com.example.appquanlychitieu.data.remote.dto.UpdateFinancialCycleRequestDto;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public final class AccountRepository {
    private final ApiService api;

    public AccountRepository(Context context) { api = ApiClient.getService(context); }

    public void getProfile(RemoteCallback<ProfileDto> callback) {
        enqueueProfile(api.getProfile(), callback);
    }

    public void updateProfile(String name, RemoteCallback<ProfileDto> callback) {
        enqueueProfile(api.updateProfile(new UpdateProfileRequestDto(name)), callback);
    }

    public void updateFinancialCycle(int startDay, RemoteCallback<ProfileDto> callback) {
        enqueueProfile(api.updateFinancialCycle(new UpdateFinancialCycleRequestDto(startDay)), callback);
    }

    public void changePassword(String current, String next, RemoteCallback<Void> callback) {
        enqueueVoid(api.changePassword(new ChangePasswordRequestDto(current, next)), callback);
    }

    public void requestEmailChange(String email, String password, RemoteCallback<Void> callback) {
        enqueueVoid(api.requestEmailChange(new EmailChangeRequestDto(email, password)), callback);
    }

    public void confirmEmailChange(String code, RemoteCallback<ProfileDto> callback) {
        enqueueProfile(api.confirmEmailChange(new EmailChangeConfirmRequestDto(code)), callback);
    }

    public void deleteAccount(String password, RemoteCallback<Void> callback) {
        enqueueVoid(api.deleteAccount(new DeleteAccountRequestDto(password)), callback);
    }

    private void enqueueProfile(Call<ProfileDto> call, RemoteCallback<ProfileDto> callback) {
        call.enqueue(new Callback<ProfileDto>() {
            @Override public void onResponse(Call<ProfileDto> call, Response<ProfileDto> response) {
                if (response.isSuccessful() && response.body() != null) callback.onSuccess(response.body());
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }
            @Override public void onFailure(Call<ProfileDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
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
}
