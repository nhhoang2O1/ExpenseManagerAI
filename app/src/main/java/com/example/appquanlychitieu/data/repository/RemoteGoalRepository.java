package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.model.Goal;
import com.example.appquanlychitieu.data.model.GoalHistory;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AddGoalFundsRequestDto;
import com.example.appquanlychitieu.data.remote.dto.AvailableBalanceDto;
import com.example.appquanlychitieu.data.remote.dto.CompleteGoalRequestDto;
import com.example.appquanlychitieu.data.remote.dto.GoalDto;
import com.example.appquanlychitieu.data.remote.dto.GoalHistoryDto;
import com.example.appquanlychitieu.data.remote.dto.GoalRequestDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Locale;
import java.util.List;
import java.util.TimeZone;
import java.util.UUID;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class RemoteGoalRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteGoalRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void getGoals(long cacheUserId, RemoteCallback<List<Goal>> callback) {
        apiService.getGoals().enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<Goal> goals = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        goals.add(toLocal(gson.fromJson(item, GoalDto.class), cacheUserId));
                    }
                    callback.onSuccess(goals);
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

    public void create(Goal goal, RemoteCallback<Goal> callback) {
        enqueue(apiService.createGoal(toRequest(goal)), goal.getUserId(), callback);
    }

    public void update(Goal goal, RemoteCallback<Goal> callback) {
        if (goal.getRemoteId() == null || goal.getRemoteId().trim().isEmpty()) return;
        enqueue(apiService.updateGoal(goal.getRemoteId(), quote(goal.getVersion()), toRequest(goal)),
                goal.getUserId(), callback);
    }

    public void addFunds(Goal goal, long amount, RemoteCallback<Goal> callback) {
        if (goal.getRemoteId() == null || goal.getRemoteId().trim().isEmpty()) return;
        apiService.addGoalFunds(goal.getRemoteId(), UUID.randomUUID().toString(), quote(goal.getVersion()),
                new AddGoalFundsRequestDto(amount))
                .enqueue(new Callback<GoalDto>() {
                    @Override
                    public void onResponse(Call<GoalDto> call, Response<GoalDto> response) {
                        if (response.isSuccessful() && response.body() != null) {
                            callback.onSuccess(toLocal(response.body(), goal.getUserId()));
                        } else {
                            callback.onError(ApiResponseHelper.fromResponse(response));
                        }
                    }

                    @Override
                    public void onFailure(Call<GoalDto> call, Throwable throwable) {
                        callback.onError(ApiResponseHelper.fromFailure(throwable));
                    }
                });
    }

    public void getAvailableBalance(int year, int month, RemoteCallback<AvailableBalanceDto> callback) {
        apiService.getAvailableBalance(year, month).enqueue(new Callback<AvailableBalanceDto>() {
            @Override public void onResponse(Call<AvailableBalanceDto> call,
                                             Response<AvailableBalanceDto> response) {
                if (response.isSuccessful() && response.body() != null)
                    callback.onSuccess(response.body());
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }

            @Override public void onFailure(Call<AvailableBalanceDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    public void complete(Goal goal, String categoryId, String transactionDate,
                         RemoteCallback<Goal> callback) {
        if (goal.getRemoteId() == null || goal.getRemoteId().trim().isEmpty()) return;
        CompleteGoalRequestDto request = new CompleteGoalRequestDto(
                categoryId, transactionDate, "Hoàn thành mục tiêu: " + goal.getName());
        enqueue(apiService.completeGoal(goal.getRemoteId(), UUID.randomUUID().toString(),
                        quote(goal.getVersion()), request), goal.getUserId(), callback);
    }

    public void cancel(Goal goal, RemoteCallback<Goal> callback) {
        if (goal.getRemoteId() == null || goal.getRemoteId().trim().isEmpty()) return;
        enqueue(apiService.cancelGoal(goal.getRemoteId(), quote(goal.getVersion())),
                goal.getUserId(), callback);
    }

    public void delete(Goal goal, RemoteCallback<Void> callback) {
        if (goal.getRemoteId() == null || goal.getRemoteId().trim().isEmpty()) return;
        apiService.deleteGoal(goal.getRemoteId(), quote(goal.getVersion())).enqueue(new Callback<Void>() {
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

    public void getHistory(String remoteGoalId, long localGoalId, RemoteCallback<List<GoalHistory>> callback) {
        apiService.getGoalHistory(remoteGoalId).enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<GoalHistory> history = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        history.add(toLocalHistory(gson.fromJson(item, GoalHistoryDto.class), localGoalId));
                    }
                    callback.onSuccess(history);
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

    private void enqueue(Call<GoalDto> call, long cacheUserId, RemoteCallback<Goal> callback) {
        call.enqueue(new Callback<GoalDto>() {
            @Override
            public void onResponse(Call<GoalDto> call, Response<GoalDto> response) {
                if (response.isSuccessful() && response.body() != null) {
                    callback.onSuccess(toLocal(response.body(), cacheUserId));
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<GoalDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private GoalRequestDto toRequest(Goal goal) {
        return new GoalRequestDto(
                goal.getName(),
                goal.getTargetAmount(),
                goal.getCurrentAmount());
    }

    private Goal toLocal(GoalDto dto, long cacheUserId) {
        Goal goal = new Goal(dto.name, amount(dto.targetAmount), amount(dto.currentAmount), cacheUserId);
        goal.setId(dto.id == null ? 0L : dto.id.hashCode());
        goal.setRemoteId(dto.id);
        goal.setVersion(dto.version);
        goal.setStatus(dto.status);
        goal.setCompletedAt(dto.completedAt);
        goal.setCompletionTransactionId(dto.completionTransactionId);
        return goal;
    }

    private GoalHistory toLocalHistory(GoalHistoryDto dto, long localGoalId) {
        GoalHistory history = new GoalHistory(localGoalId, amount(dto.amountAdded), parseDate(dto.date));
        history.setId(dto.id == null ? 0L : dto.id.hashCode());
        history.setRemoteId(dto.id);
        history.setRemoteGoalId(dto.goalId);
        history.setActionType(dto.actionType);
        return history;
    }

    private long parseDate(String value) {
        if (value == null) return System.currentTimeMillis();
        try {
            SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US);
            format.setTimeZone(TimeZone.getTimeZone("UTC"));
            String normalized = value.endsWith("Z") ? value.substring(0, value.length() - 1) : value;
            return format.parse(normalized).getTime();
        } catch (ParseException | RuntimeException exception) {
            return System.currentTimeMillis();
        }
    }

    private long amount(Long value) {
        return value == null ? 0L : value;
    }

    private String quote(long version) { return "\"" + version + "\""; }

    private JsonArray resolveArray(JsonElement body) {
        if (body.isJsonArray()) return body.getAsJsonArray();
        JsonObject object = body.getAsJsonObject();
        for (String key : new String[]{"items", "data", "results"}) {
            if (object.has(key) && object.get(key).isJsonArray()) return object.getAsJsonArray(key);
        }
        return new JsonArray();
    }
}
