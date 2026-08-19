package com.example.appquanlychitieu.ui.statistics;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.CategorySummary;
import com.example.appquanlychitieu.data.model.MonthlySummary;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteStatisticsRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.ui.common.LatestRequest;
import com.example.appquanlychitieu.util.FinancialCycleUtils;
import com.example.appquanlychitieu.util.SessionManager;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;

public class StatisticsViewModel extends AndroidViewModel {
    private final RemoteStatisticsRepository repository;
    private final boolean authenticated;
    private final int financialCycleStartDay;
    private final MutableLiveData<int[]> selectedMonthYear = new MutableLiveData<>();
    private final MutableLiveData<List<CategorySummary>> categorySummary =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<List<MonthlySummary>> monthlySummary =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState =
            new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> remoteError = new MutableLiveData<>();
    private boolean categoryLoaded;
    private boolean monthlyLoaded;
    private final LatestRequest requests = new LatestRequest();

    public StatisticsViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteStatisticsRepository(application);
        SessionManager session = new SessionManager(application);
        authenticated = session.hasAuthToken();
        financialCycleStartDay = session.getFinancialCycleStartDay();
        Calendar calendar = Calendar.getInstance();
        selectedMonthYear.setValue(new int[]{calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH)});
        refreshRemoteStatistics();
    }

    public LiveData<List<CategorySummary>> getCategorySummary() { return categorySummary; }
    public LiveData<List<MonthlySummary>> getMonthlySummary() { return monthlySummary; }
    public MutableLiveData<int[]> getSelectedMonthYear() { return selectedMonthYear; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getRemoteError() { return remoteError; }

    public void previousMonth() { moveMonth(-1); }
    public void nextMonth() { moveMonth(1); }

    public void selectMonth(int year, int monthIndex) {
        selectedMonthYear.setValue(new int[]{year, monthIndex});
        refreshRemoteStatistics();
    }

    private void moveMonth(int amount) {
        int[] current = selectedMonthYear.getValue();
        if (current == null) return;
        Calendar calendar = Calendar.getInstance();
        calendar.set(current[0], current[1], 1);
        calendar.add(Calendar.MONTH, amount);
        selectedMonthYear.setValue(new int[]{calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH)});
        refreshRemoteStatistics();
    }

    public void refreshRemoteStatistics() {
        if (!authenticated) {
            loadState.setValue(LoadState.ERROR);
            remoteError.setValue("Phiên đăng nhập không hợp lệ");
            return;
        }
        int[] month = selectedMonthYear.getValue();
        if (month == null) return;
        categoryLoaded = false;
        monthlyLoaded = false;
        final int generation = requests.begin();
        loadState.setValue(LoadState.LOADING);
        LocalDate cycleStart = FinancialCycleUtils.cycleStartForMonth(month[0], month[1], financialCycleStartDay);
        LocalDate cycleEnd = FinancialCycleUtils.endFor(cycleStart, financialCycleStartDay);
        String from = cycleStart.toString();
        String to = cycleEnd.toString();
        repository.getCategorySummary(from, to, new RemoteCallback<List<CategorySummary>>() {
            @Override public void onSuccess(List<CategorySummary> value) {
                if (!requests.isCurrent(generation)) return;
                categoryLoaded = true;
                categorySummary.setValue(value == null ? new ArrayList<>() : value);
                finishLoad();
            }
            @Override public void onError(ApiError error) {
                if (!requests.isCurrent(generation)) return;
                remoteError.setValue(error.getMessage());
                loadState.setValue(LoadState.ERROR);
            }
        });
        repository.getMonthlySummary(month[0], new RemoteCallback<List<MonthlySummary>>() {
            @Override public void onSuccess(List<MonthlySummary> value) {
                if (!requests.isCurrent(generation)) return;
                monthlyLoaded = true;
                monthlySummary.setValue(value == null ? new ArrayList<>() : value);
                finishLoad();
            }
            @Override public void onError(ApiError error) {
                if (!requests.isCurrent(generation)) return;
                remoteError.setValue(error.getMessage());
                loadState.setValue(LoadState.ERROR);
            }
        });
    }

    private void finishLoad() {
        if (!categoryLoaded || !monthlyLoaded) return;
        remoteError.setValue(null);
        List<CategorySummary> categories = categorySummary.getValue();
        List<MonthlySummary> months = monthlySummary.getValue();
        boolean empty = (categories == null || categories.isEmpty())
                && (months == null || months.isEmpty());
        loadState.setValue(empty ? LoadState.EMPTY : LoadState.CONTENT);
    }

    private String toIsoDate(long millis) {
        return Instant.ofEpochMilli(millis).atZone(ZoneId.of("Asia/Ho_Chi_Minh"))
                .toLocalDate().toString();
    }
}
