package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public final class RemoteCategoryRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteCategoryRepository(Context context) {
        apiService = ApiClient.getService(context.getApplicationContext());
    }

    public void getCategories(String type, RemoteCallback<List<CategoryDto>> callback) {
        getCategories(type, callback, false);
    }

    public void getCategories(String type, RemoteCallback<List<CategoryDto>> callback, boolean includeInactive) {
        apiService.getCategories().enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<CategoryDto> result = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        CategoryDto category = gson.fromJson(item, CategoryDto.class);
                        if ((type == null || type.equalsIgnoreCase(category.type))
                                && (includeInactive || category.isActive)) result.add(category);
                    }
                    callback.onSuccess(result);
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

    public void create(CategoryRequestDto request, RemoteCallback<CategoryDto> callback) {
        enqueue(apiService.createCategory(request), callback);
    }

    public void update(CategoryDto category, CategoryRequestDto request,
                       RemoteCallback<CategoryDto> callback) {
        enqueue(apiService.updateCategory(category.id, quote(category.version), request), callback);
    }

    public void delete(CategoryDto category, RemoteCallback<Void> callback) {
        apiService.deleteCategory(category.id, quote(category.version)).enqueue(new Callback<Void>() {
            @Override public void onResponse(Call<Void> call, Response<Void> response) {
                if (response.isSuccessful()) callback.onSuccess(null);
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }
            @Override public void onFailure(Call<Void> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private void enqueue(Call<CategoryDto> call, RemoteCallback<CategoryDto> callback) {
        call.enqueue(new Callback<CategoryDto>() {
            @Override public void onResponse(Call<CategoryDto> call, Response<CategoryDto> response) {
                if (response.isSuccessful() && response.body() != null) callback.onSuccess(response.body());
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }
            @Override public void onFailure(Call<CategoryDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private String quote(long version) { return "\"" + version + "\""; }

    private JsonArray resolveArray(JsonElement body) {
        if (body.isJsonArray()) return body.getAsJsonArray();
        JsonObject object = body.getAsJsonObject();
        for (String key : new String[]{"items", "data", "results"}) {
            if (object.has(key) && object.get(key).isJsonArray()) {
                return object.getAsJsonArray(key);
            }
        }
        return new JsonArray();
    }
}
