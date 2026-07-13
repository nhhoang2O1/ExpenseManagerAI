package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
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
                        if (type == null || type.equalsIgnoreCase(category.type)) result.add(category);
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
