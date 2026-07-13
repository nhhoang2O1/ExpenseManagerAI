package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.model.Reminder;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.ReminderDto;
import com.example.appquanlychitieu.data.remote.dto.ReminderRequestDto;
import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import java.util.ArrayList;
import java.util.List;

import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public class RemoteReminderRepository {
    private final ApiService apiService;
    private final Gson gson = new Gson();

    public RemoteReminderRepository(Context context) {
        apiService = ApiClient.getService(context);
    }

    public void getReminders(long cacheUserId, RemoteCallback<List<Reminder>> callback) {
        apiService.getReminders().enqueue(new Callback<JsonElement>() {
            @Override
            public void onResponse(Call<JsonElement> call, Response<JsonElement> response) {
                if (!response.isSuccessful() || response.body() == null) {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                    return;
                }
                try {
                    List<Reminder> reminders = new ArrayList<>();
                    for (JsonElement item : resolveArray(response.body())) {
                        reminders.add(toLocal(gson.fromJson(item, ReminderDto.class), cacheUserId));
                    }
                    callback.onSuccess(reminders);
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

    public void create(Reminder reminder, RemoteCallback<Reminder> callback) {
        enqueue(apiService.createReminder(toRequest(reminder)), reminder.getUserId(), callback);
    }

    public void update(Reminder reminder, RemoteCallback<Reminder> callback) {
        if (reminder.getRemoteId() == null || reminder.getRemoteId().trim().isEmpty()) return;
        enqueue(apiService.updateReminder(reminder.getRemoteId(), toRequest(reminder)), reminder.getUserId(), callback);
    }

    public void delete(Reminder reminder, RemoteCallback<Void> callback) {
        if (reminder.getRemoteId() == null || reminder.getRemoteId().trim().isEmpty()) return;
        apiService.deleteReminder(reminder.getRemoteId()).enqueue(new Callback<Void>() {
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

    private void enqueue(Call<ReminderDto> call, long cacheUserId, RemoteCallback<Reminder> callback) {
        call.enqueue(new Callback<ReminderDto>() {
            @Override
            public void onResponse(Call<ReminderDto> call, Response<ReminderDto> response) {
                if (response.isSuccessful() && response.body() != null) {
                    callback.onSuccess(toLocal(response.body(), cacheUserId));
                } else {
                    callback.onError(ApiResponseHelper.fromResponse(response));
                }
            }

            @Override
            public void onFailure(Call<ReminderDto> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }

    private ReminderRequestDto toRequest(Reminder reminder) {
        return new ReminderRequestDto(
                reminder.getContent(),
                reminder.getDayOfMonth(),
                reminder.getHour(),
                reminder.getMinute(),
                reminder.isActive());
    }

    private Reminder toLocal(ReminderDto dto, long cacheUserId) {
        Reminder reminder = new Reminder(
                dto.content,
                dto.dayOfMonth,
                dto.hour,
                dto.minute,
                cacheUserId,
                dto.isActive);
        reminder.setId(dto.id == null ? 0L : dto.id.hashCode());
        reminder.setRemoteId(dto.id);
        return reminder;
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
