package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.model.Budget;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.BudgetDto;
import com.example.appquanlychitieu.data.remote.dto.BudgetRequestDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class RemoteBudgetRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteBudgetRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void getBudgets(String monthYear, long cacheUserId, RemoteCallback<List<Budget>> callback) {
        apiService.getBudgets(monthYear).enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<Budget> budgets = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        budgets.add(toLocal(gson.fromJson(item, BudgetDto.class), cacheUserId));
                    }
                    callback.onSuccess(budgets);
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

    public void create(String categoryId, long amount, String monthYear, RemoteCallback<Budget> callback) {
        enqueue(apiService.createBudget(new BudgetRequestDto(categoryId, amount, monthYear)), callback);
    }

    public void update(String budgetId, long version, String categoryId, long amount,
                       String monthYear, RemoteCallback<Budget> callback) {
        enqueue(apiService.updateBudget(budgetId, quote(version),
                new BudgetRequestDto(categoryId, amount, monthYear)), callback);
    }

    public void delete(String budgetId, long version, RemoteCallback<Void> callback) {
        apiService.deleteBudget(budgetId, quote(version)).enqueue(new Callback<Void>() {
            @Override
            public void onResponse(Call<Void> call, Response<Void> response) {
                if (response.isSuccessful()) callback.onSuccess(null);
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }

            @Override
            public void onFailure(Call<Void> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private void enqueue(Call<BudgetDto> call, RemoteCallback<Budget> callback) {
        call.enqueue(new Callback<BudgetDto>() {
            @Override
            public void onResponse(Call<BudgetDto> call, Response<BudgetDto> response) {
                if (response.isSuccessful() && response.body() != null) {
                    callback.onSuccess(toLocal(response.body(), 0));
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<BudgetDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private Budget toLocal(BudgetDto dto, long cacheUserId) {
        String categoryId = dto.categoryId == null ? "" : dto.categoryId;
        Budget budget = new Budget(categoryId.hashCode(), amount(dto.amount), dto.monthYear, cacheUserId);
        budget.setId(dto.id == null ? 0L : dto.id.hashCode());
        budget.setRemoteId(dto.id);
        budget.setRemoteCategoryId(dto.categoryId);
        budget.setRemoteCategoryName(dto.categoryName);
        budget.setRemoteCategoryColor(dto.categoryColor);
        budget.setRemoteCategoryIcon(dto.categoryIcon);
        budget.setVersion(dto.version);
        return budget;
    }

    private String quote(long version) { return "\"" + version + "\""; }

    private long amount(Long value) {
        return value == null ? 0L : value;
    }

    private JsonArray resolveArray(JsonElement body) {
        if (body.isJsonArray()) return body.getAsJsonArray();
        JsonObject object = body.getAsJsonObject();
        for (String key : new String[]{"items", "data", "results"}) {
            if (object.has(key) && object.get(key).isJsonArray()) return object.getAsJsonArray(key);
        }
        return new JsonArray();
    }
}
