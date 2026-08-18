package com.example.appquanlychitieu.ui.budget;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.Budget;
import com.example.appquanlychitieu.data.model.CategorySummary;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.example.appquanlychitieu.data.repository.RemoteBudgetRepository;
import com.example.appquanlychitieu.data.repository.RemoteCategoryRepository;
import com.example.appquanlychitieu.data.repository.RemoteStatisticsRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.ui.common.LatestRequest;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.util.FinancialCycleUtils;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Locale;
import java.time.LocalDate;

public class BudgetViewModel extends AndroidViewModel {
    private final RemoteBudgetRepository repository;
    private final RemoteCategoryRepository categoryRepository;
    private final RemoteStatisticsRepository statisticsRepository;
    private final long userId;
    private final boolean authenticated;
    private final int financialCycleStartDay;
    private final MutableLiveData<int[]> selectedMonthYear = new MutableLiveData<>();
    private final MutableLiveData<List<Budget>> budgets = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState = new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> error = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();
    private final MutableLiveData<List<CategoryDto>> categories = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<Map<Long, Long>> spentByCategory = new MutableLiveData<>(new HashMap<>());
    private boolean hasLoaded;
    private final LatestRequest budgetRequests = new LatestRequest();
    private final LatestRequest spentRequests = new LatestRequest();

    public BudgetViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteBudgetRepository(application);
        categoryRepository = new RemoteCategoryRepository(application);
        statisticsRepository = new RemoteStatisticsRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        financialCycleStartDay = session.getFinancialCycleStartDay();
        Calendar calendar = Calendar.getInstance();
        selectedMonthYear.setValue(new int[]{calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH)});
        refreshBudgets();
        loadCategories();
    }

    public long getUserId() { return userId; }
    public boolean usesRemote() { return authenticated; }
    public LiveData<List<Budget>> getBudgets() { return budgets; }
    public MutableLiveData<int[]> getSelectedMonthYear() { return selectedMonthYear; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getError() { return error; }
    public LiveData<String> getFeedback() { return feedback; }
    public LiveData<List<CategoryDto>> getCategories() { return categories; }
    public LiveData<Map<Long, Long>> getSpentByCategory() { return spentByCategory; }

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

    public void loadCategories() {
        categoryRepository.getCategories("EXPENSE", new RemoteCallback<List<CategoryDto>>() {
            @Override public void onSuccess(List<CategoryDto> value) {
                categories.setValue(value == null ? new ArrayList<>() : value);
            }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }

    public void createCategory(String name, RemoteCallback<CategoryDto> callback) {
        categoryRepository.create(new CategoryRequestDto(name, "EXPENSE", "#607D8B", "other"), callback);
    }

    public void loadSpent(int year, int monthIndex) {
        final int generation = spentRequests.begin();
        LocalDate cycleStart = FinancialCycleUtils.cycleStartForMonth(year, monthIndex, financialCycleStartDay);
        LocalDate cycleEnd = FinancialCycleUtils.endFor(cycleStart, financialCycleStartDay);
        String from = cycleStart.toString();
        String to = cycleEnd.toString();
        statisticsRepository.getCategorySummary(from, to,
                new RemoteCallback<List<CategorySummary>>() {
                    @Override public void onSuccess(List<CategorySummary> value) {
                        if (!spentRequests.isCurrent(generation)) return;
                        Map<Long, Long> totals = new HashMap<>();
                        if (value != null) for (CategorySummary item : value)
                            totals.put(item.getCategoryId(), item.getTotalAmount());
                        spentByCategory.setValue(totals);
                    }
                    @Override public void onError(ApiError apiError) {
                        if (!spentRequests.isCurrent(generation)) return;
                        error.setValue(apiError.getMessage());
                    }
                });
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

    public void updateBudget(Budget budget, long amount) {
        if (budget == null || budget.getRemoteId() == null) return;
        repository.update(budget.getRemoteId(), budget.getVersion(), budget.getRemoteCategoryId(), amount,
                budget.getMonthYear(), new RemoteCallback<Budget>() {
                    @Override public void onSuccess(Budget value) {
                        feedback.setValue("Đã cập nhật ngân sách");
                        refreshBudgets();
                    }
                    @Override public void onError(ApiError apiError) {
                        error.setValue(apiError.getMessage());
                    }
                });
    }

    public void deleteBudget(Budget budget) {
        if (budget == null || budget.getRemoteId() == null) return;
        repository.delete(budget.getRemoteId(), budget.getVersion(), new RemoteCallback<Void>() {
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
        LocalDate cycleStart = FinancialCycleUtils.cycleStartForMonth(month[0], month[1], financialCycleStartDay);
        String key = String.format(Locale.ROOT, "%04d-%02d", cycleStart.getYear(), cycleStart.getMonthValue());
        final int generation = budgetRequests.begin();
        repository.getBudgets(key, userId, new RemoteCallback<List<Budget>>() {
            @Override
            public void onSuccess(List<Budget> value) {
                if (!budgetRequests.isCurrent(generation)) return;
                hasLoaded = true;
                List<Budget> result = value == null ? new ArrayList<>() : value;
                budgets.setValue(result);
                error.setValue(null);
                loadState.setValue(result.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
            }

            @Override
            public void onError(ApiError apiError) {
                if (!budgetRequests.isCurrent(generation)) return;
                error.setValue(apiError.getMessage());
                if (!hasLoaded) loadState.setValue(LoadState.ERROR);
            }
        });
    }
}
