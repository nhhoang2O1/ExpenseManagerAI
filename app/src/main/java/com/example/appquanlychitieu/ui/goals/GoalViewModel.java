package com.example.appquanlychitieu.ui.goals;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.Goal;
import com.example.appquanlychitieu.data.model.GoalHistory;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteGoalRepository;
import com.example.appquanlychitieu.data.repository.RemoteCategoryRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.List;

public class GoalViewModel extends AndroidViewModel {
    private final RemoteGoalRepository repository;
    private final RemoteCategoryRepository categoryRepository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<List<Goal>> goals = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<List<GoalHistory>> history = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState = new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> error = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();
    private final MutableLiveData<List<CategoryDto>> expenseCategories =
            new MutableLiveData<>(new ArrayList<>());
    private boolean hasLoaded;

    public GoalViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteGoalRepository(application);
        categoryRepository = new RemoteCategoryRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        refreshGoals();
        refreshExpenseCategories();
    }

    public long getUserId() { return userId; }
    public boolean usesRemote() { return authenticated; }
    public LiveData<List<Goal>> getGoals() { return goals; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getError() { return error; }
    public LiveData<String> getFeedback() { return feedback; }
    public LiveData<List<CategoryDto>> getExpenseCategories() { return expenseCategories; }

    public void insertGoal(Goal goal) {
        insertGoal(goal, null);
    }

    public void insertGoal(Goal goal, RemoteCallback<Goal> callback) {
        repository.create(goal, new RefreshCallback("Đã tạo mục tiêu", callback));
    }

    public void updateGoal(Goal goal) {
        updateGoal(goal, null);
    }

    public void updateGoal(Goal goal, RemoteCallback<Goal> callback) {
        repository.update(goal, new RefreshCallback("Đã cập nhật mục tiêu", callback));
    }

    public void addFunds(Goal goal, long amount) {
        addFunds(goal, amount, null);
    }

    public void addFunds(Goal goal, long amount, RemoteCallback<Goal> callback) {
        repository.addFunds(goal, amount, new RefreshCallback("Đã cập nhật tiến độ", callback));
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

    public void completeGoal(Goal goal, String categoryId, String transactionDate,
                             RemoteCallback<Goal> callback) {
        repository.complete(goal, categoryId, transactionDate,
                new RefreshCallback("Đã hoàn thành mục tiêu và tạo giao dịch", callback));
    }

    public void cancelGoal(Goal goal) {
        repository.cancel(goal, new RefreshCallback(
                "Đã hủy mục tiêu và hoàn lại số dư khả dụng", null));
    }

    private void refreshExpenseCategories() {
        categoryRepository.getCategories("EXPENSE", new RemoteCallback<List<CategoryDto>>() {
            @Override public void onSuccess(List<CategoryDto> value) {
                expenseCategories.setValue(value == null ? new ArrayList<>() : value);
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
        private final RemoteCallback<Goal> downstream;
        RefreshCallback(String message, RemoteCallback<Goal> downstream) {
            this.message = message;
            this.downstream = downstream;
        }
        @Override public void onSuccess(Goal value) {
            feedback.setValue(message);
            refreshGoals();
            if (downstream != null) downstream.onSuccess(value);
        }
        @Override public void onError(ApiError apiError) {
            error.setValue(apiError.getMessage());
            if (downstream != null) downstream.onError(apiError);
        }
    }
}
