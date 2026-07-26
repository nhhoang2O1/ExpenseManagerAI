package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.mapper.RemoteTransactionMapper;
import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionRequestDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class RemoteTransactionRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteTransactionRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void getTransactions(long cacheUserId, RemoteCallback<List<Transaction>> callback) {
        loadTransactionsPage(cacheUserId, 1, 100, new ArrayList<>(), callback);
    }

    private void loadTransactionsPage(
            long cacheUserId,
            int page,
            int pageSize,
            List<Transaction> accumulated,
            RemoteCallback<List<Transaction>> callback) {
        apiService.getTransactions(page, pageSize).enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<Transaction> mapped = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        TransactionDto dto = gson.fromJson(item, TransactionDto.class);
                        mapped.add(RemoteTransactionMapper.toLocalView(dto, cacheUserId));
                    }
                    accumulated.addAll(mapped);
                    JsonObject object = response.body().isJsonObject()
                            ? response.body().getAsJsonObject() : null;
                    int totalPages = readInt(object, "totalPages", "total_pages");
                    int currentPage = readInt(object, "page");
                    if (currentPage <= 0) currentPage = page;
                    boolean hasNext = totalPages > currentPage
                            || (totalPages <= 0 && mapped.size() == pageSize);
                    if (hasNext && page < 10_000) {
                        loadTransactionsPage(cacheUserId, page + 1, pageSize, accumulated, callback);
                    } else {
                        callback.onSuccess(new ArrayList<>(accumulated));
                    }
                } catch (RuntimeException exception) {
                    callback.onError(ApiResponseHelper.fromFailure(exception));
                }
            }

            @Override
            public void onFailure(Call<JsonElement> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private int readInt(JsonObject object, String... keys) {
        if (object == null) return 0;
        for (String key : keys) {
            if (!object.has(key) || object.get(key).isJsonNull()) continue;
            try { return object.get(key).getAsInt(); }
            catch (RuntimeException ignored) { }
        }
        return 0;
    }

    public void getCategories(
            String type,
            RemoteCallback<List<CategoryDto>> callback) {
        apiService.getCategories().enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<CategoryDto> categories = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        CategoryDto category = gson.fromJson(item, CategoryDto.class);
                        if (type == null || type.equalsIgnoreCase(category.type)) {
                            categories.add(category);
                        }
                    }
                    callback.onSuccess(categories);
                } catch (RuntimeException exception) {
                    callback.onError(ApiResponseHelper.fromFailure(exception));
                }
            }

            @Override
            public void onFailure(Call<JsonElement> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    public void create(
            TransactionRequestDto request,
            RemoteCallback<TransactionDto> callback) {
        enqueueTransaction(apiService.createTransaction(UUID.randomUUID().toString(), request), callback);
    }

    public void update(
            String transactionId,
            long version,
            TransactionRequestDto request,
            RemoteCallback<TransactionDto> callback) {
        enqueueTransaction(apiService.updateTransaction(transactionId, quote(version), request), callback);
    }

    public void delete(String transactionId, long version, RemoteCallback<Void> callback) {
        apiService.deleteTransaction(transactionId, quote(version)).enqueue(new Callback<Void>() {
            @Override
            public void onResponse(Call<Void> call, Response<Void> response) {
                if (response.isSuccessful()) {
                    callback.onSuccess(null);
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<Void> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private String quote(long version) { return "\"" + version + "\""; }

    private void enqueueTransaction(
            Call<TransactionDto> call,
            RemoteCallback<TransactionDto> callback) {
        call.enqueue(new Callback<TransactionDto>() {
            @Override
            public void onResponse(
                    Call<TransactionDto> call,
                    Response<TransactionDto> response) {
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

    private JsonArray resolveArray(JsonElement body) {
        if (body.isJsonArray()) {
            return body.getAsJsonArray();
        }
        JsonObject object = body.getAsJsonObject();
        for (String key : new String[]{"items", "data", "results"}) {
            if (object.has(key) && object.get(key).isJsonArray()) {
                return object.getAsJsonArray(key);
            }
        }
        return new JsonArray();
    }
}
