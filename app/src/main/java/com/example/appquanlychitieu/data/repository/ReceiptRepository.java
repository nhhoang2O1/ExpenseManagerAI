package com.example.appquanlychitieu.data.repository;

import android.content.Context;
import android.net.Uri;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.ContentUriRequestBody;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.ConfirmReceiptRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;
import java.io.IOException;

import okhttp3.MultipartBody;
import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class ReceiptRepository {
    private final Context appContext;
    private final ApiService apiService;
    private final RemoteCategoryRepository categoryRepository;
    private final Gson gson = new Gson();

    public ReceiptRepository(Context context) {
        appContext = context.getApplicationContext();
        apiService = ApiClient.getService(appContext);
        categoryRepository = new RemoteCategoryRepository(appContext);
    }

    public void upload(Uri imageUri, RemoteCallback<ReceiptDto> callback) {
        ContentUriRequestBody body = new ContentUriRequestBody(appContext, imageUri);
        MultipartBody.Part image = MultipartBody.Part.createFormData(
                "file",
                "receipt.jpg",
                body);
        enqueueReceipt(apiService.uploadReceipt(image), callback);
    }

    public void process(String receiptId, RemoteCallback<ReceiptDto> callback) {
        enqueueReceipt(apiService.processReceipt(receiptId), callback);
    }

    public void get(String receiptId, RemoteCallback<ReceiptDto> callback) {
        enqueueReceipt(apiService.getReceipt(receiptId), callback);
    }

    public void retry(String receiptId, RemoteCallback<ReceiptDto> callback) {
        enqueueReceipt(apiService.retryReceipt(receiptId), callback);
    }

    public void confirm(
            String receiptId,
            ConfirmReceiptRequestDto request,
            RemoteCallback<TransactionDto> callback) {
        apiService.confirmReceipt(receiptId, request).enqueue(new Callback<TransactionDto>() {
            @Override
            public void onResponse(Call<TransactionDto> call, Response<TransactionDto> response) {
                if (response.isSuccessful() && response.body() != null) {
                    callback.onSuccess(response.body());
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<TransactionDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    public void getExpenseCategories(RemoteCallback<List<CategoryDto>> callback) {
        categoryRepository.getCategories("EXPENSE", callback);
    }

    private void enqueueReceipt(Call<ReceiptDto> call, RemoteCallback<ReceiptDto> callback) {
        call.enqueue(new Callback<ReceiptDto>() {
            @Override
            public void onResponse(Call<ReceiptDto> call, Response<ReceiptDto> response) {
                if (response.isSuccessful() && response.body() != null) {
                    callback.onSuccess(response.body());
                } else {
                    ReceiptDto failedReceipt = parseFailedReceipt(response);
                    if (failedReceipt != null) {
                        callback.onSuccess(failedReceipt);
                        return;
                    }
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<ReceiptDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private ReceiptDto parseFailedReceipt(Response<ReceiptDto> response) {
        if (response.errorBody() == null) {
            return null;
        }
        try {
            ReceiptDto receipt = gson.fromJson(response.errorBody().string(), ReceiptDto.class);
            return receipt != null && receipt.id != null && receipt.status != null
                    ? receipt : null;
        } catch (IOException | RuntimeException ignored) {
            return null;
        }
    }

}
