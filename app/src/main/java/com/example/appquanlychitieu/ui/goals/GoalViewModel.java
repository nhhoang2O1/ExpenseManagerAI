package com.example.appquanlychitieu.ui.goals;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.Goal;
import com.example.appquanlychitieu.data.model.GoalHistory;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteGoalRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.List;

public class GoalViewModel extends AndroidViewModel {
    private final RemoteGoalRepository repository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<List<Goal>> goals = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<List<GoalHistory>> history = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState = new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> error = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();
    private boolean hasLoaded;

    public GoalViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteGoalRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        refreshGoals();
    }

    public long getUserId() { return userId; }
    public boolean usesRemote() { return authenticated; }
    public LiveData<List<Goal>> getGoals() { return goals; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getError() { return error; }
    public LiveData<String> getFeedback() { return feedback; }

    public void insertGoal(Goal goal) {
        repository.create(goal, new RefreshCallback("Đã tạo mục tiêu"));
    }

    public void updateGoal(Goal goal) {
        repository.update(goal, new RefreshCallback("Đã cập nhật mục tiêu"));
    }

    public void addFunds(Goal goal, long amount) {
        repository.addFunds(goal, amount, new RefreshCallback("Đã cập nhật tiến độ"));
    }

    public void deleteGoal(Goal goal) {
        repository.delete(goal, new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) {
                feedback.setValue("Đã xóa mục tiêu");
                refreshGoals();
            }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }

    public LiveData<List<GoalHistory>> getHistoryForGoal(String remoteGoalId, long localGoalId) {
        if (remoteGoalId == null || remoteGoalId.trim().isEmpty()) {
            history.setValue(new ArrayList<>());
            error.setValue("Không tìm thấy mục tiêu trên máy chủ");
            return history;
        }
        repository.getHistory(remoteGoalId, localGoalId, new RemoteCallback<List<GoalHistory>>() {
            @Override public void onSuccess(List<GoalHistory> value) {
                history.setValue(value == null ? new ArrayList<>() : value);
            }
            @Override public void onError(ApiError apiError) {
                error.setValue(apiError.getMessage());
                history.setValue(new ArrayList<>());
            }
        });
        return history;
    }

    public LiveData<List<GoalHistory>> getHistoryForGoal(long goalId) {
        return history;
    }

    public void refreshGoals() {
        if (!authenticated) {
            loadState.setValue(LoadState.ERROR);
            error.setValue("Phiên đăng nhập không hợp lệ");
            return;
        }
        if (!hasLoaded) loadState.setValue(LoadState.LOADING);
        repository.getGoals(userId, new RemoteCallback<List<Goal>>() {
            @Override public void onSuccess(List<Goal> value) {
                hasLoaded = true;
                List<Goal> result = value == null ? new ArrayList<>() : value;
                goals.setValue(result);
                error.setValue(null);
                loadState.setValue(result.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
            }
            @Override public void onError(ApiError apiError) {
                error.setValue(apiError.getMessage());
                if (!hasLoaded) loadState.setValue(LoadState.ERROR);
            }
        });
    }

    private final class RefreshCallback implements RemoteCallback<Goal> {
        private final String message;
        RefreshCallback(String message) { this.message = message; }
        @Override public void onSuccess(Goal value) {
            feedback.setValue(message);
            refreshGoals();
        }
        @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
    }
}
