package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.model.CategorySummary;
import com.example.appquanlychitieu.data.model.MonthlySummary;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryStatisticDto;
import com.example.appquanlychitieu.data.remote.dto.MonthlyStatisticDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class RemoteStatisticsRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteStatisticsRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void getCategorySummary(
            String from,
            String to,
            RemoteCallback<List<CategorySummary>> callback) {
        apiService.getStatisticsByCategory(from, to).enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<CategorySummary> summaries = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        CategoryStatisticDto dto = gson.fromJson(item, CategoryStatisticDto.class);
                        if (!"EXPENSE".equalsIgnoreCase(dto.type)) {
                            continue;
                        }
                        CategorySummary summary = new CategorySummary();
                        summary.setCategoryId(dto.categoryId == null ? 0L : dto.categoryId.hashCode());
                        summary.setCategoryName(dto.categoryName);
                        summary.setCategoryColor(emptyToDefault(dto.categoryColor, "#6B7280"));
                        summary.setCategoryIcon(emptyToDefault(dto.categoryIcon, "ic_other"));
                        summary.setTotalAmount(amount(dto.total));
                        summary.setTransactionCount(dto.transactionCount);
                        summaries.add(summary);
                    }
                    callback.onSuccess(summaries);
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

    public void getMonthlySummary(
            int year,
            RemoteCallback<List<MonthlySummary>> callback) {
        apiService.getMonthlyStatistics(year).enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<MonthlySummary> summaries = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        MonthlyStatisticDto dto = gson.fromJson(item, MonthlyStatisticDto.class);
                        MonthlySummary summary = new MonthlySummary();
                        summary.setMonthYear(String.format(Locale.ROOT, "%04d-%02d", dto.year, dto.month));
                        summary.setTotalIncome(amount(dto.income));
                        summary.setTotalExpense(amount(dto.expense));
                        summary.setTotalSavings(amount(dto.savings));
                        summaries.add(summary);
                    }
                    summaries.sort((first, second) ->
                            second.getMonthYear().compareTo(first.getMonthYear()));
                    callback.onSuccess(summaries);
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

    private long amount(Long value) {
        return value == null ? 0L : value;
    }

    private String emptyToDefault(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value;
    }
}
