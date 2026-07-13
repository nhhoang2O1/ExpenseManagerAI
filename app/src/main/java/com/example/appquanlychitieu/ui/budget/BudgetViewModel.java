package com.example.appquanlychitieu.ui.budget;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.Budget;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteBudgetRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;

public class BudgetViewModel extends AndroidViewModel {
    private final RemoteBudgetRepository repository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<int[]> selectedMonthYear = new MutableLiveData<>();
    private final MutableLiveData<List<Budget>> budgets = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState = new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> error = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();
    private boolean hasLoaded;

    public BudgetViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteBudgetRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        Calendar calendar = Calendar.getInstance();
        selectedMonthYear.setValue(new int[]{calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH)});
        refreshBudgets();
    }

    public long getUserId() { return userId; }
    public boolean usesRemote() { return authenticated; }
    public LiveData<List<Budget>> getBudgets() { return budgets; }
    public MutableLiveData<int[]> getSelectedMonthYear() { return selectedMonthYear; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getError() { return error; }
    public LiveData<String> getFeedback() { return feedback; }

    public void previousMonth() { moveMonth(-1); }
    public void nextMonth() { moveMonth(1); }

    private void moveMonth(int amount) {
        int[] current = selectedMonthYear.getValue();
        if (current == null) return;
        Calendar calendar = Calendar.getInstance();
        calendar.set(current[0], current[1], 1);
        calendar.add(Calendar.MONTH, amount);
        selectedMonthYear.setValue(new int[]{calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH)});
        hasLoaded = false;
        refreshBudgets();
    }

    public void insertBudget(Budget budget) {
        repository.create(budget.getRemoteCategoryId(), budget.getAmount(), budget.getMonthYear(),
                new RemoteCallback<Budget>() {
                    @Override public void onSuccess(Budget value) {
                        feedback.setValue("Đã lưu ngân sách");
                        refreshBudgets();
                    }
                    @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
                });
    }

    public void deleteBudget(Budget budget) {
        if (budget == null || budget.getRemoteId() == null) return;
        repository.delete(budget.getRemoteId(), new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) {
                feedback.setValue("Đã xóa ngân sách");
                refreshBudgets();
            }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }

    public void refreshBudgets() {
        if (!authenticated) {
            loadState.setValue(LoadState.ERROR);
            error.setValue("Phiên đăng nhập không hợp lệ");
            return;
        }
        int[] month = selectedMonthYear.getValue();
        if (month == null) return;
        if (!hasLoaded) loadState.setValue(LoadState.LOADING);
        String key = String.format("%04d-%02d", month[0], month[1] + 1);
        repository.getBudgets(key, userId, new RemoteCallback<List<Budget>>() {
            @Override
            public void onSuccess(List<Budget> value) {
                hasLoaded = true;
                List<Budget> result = value == null ? new ArrayList<>() : value;
                budgets.setValue(result);
                error.setValue(null);
                loadState.setValue(result.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
            }

            @Override
            public void onError(ApiError apiError) {
                error.setValue(apiError.getMessage());
                if (!hasLoaded) loadState.setValue(LoadState.ERROR);
            }
        });
    }
}
