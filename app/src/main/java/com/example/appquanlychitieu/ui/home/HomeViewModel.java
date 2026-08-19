package com.example.appquanlychitieu.ui.home;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.MonthlySummary;
import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteStatisticsRepository;
import com.example.appquanlychitieu.data.repository.RemoteTransactionRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.FinancialCycleUtils;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.time.LocalDate;

/**
 * Home owns remote loading and derives only the day/recent presentation data.
 * Monthly totals come from the backend statistics endpoint, so totals remain
 * correct even when the account has more than one page of transactions.
 */
public class HomeViewModel extends AndroidViewModel {
    private final RemoteTransactionRepository transactionRepository;
    private final RemoteStatisticsRepository statisticsRepository;
    private final long userId;
    private final boolean authenticated;
    private final int financialCycleStartDay;
    private final MutableLiveData<Long> totalIncome = new MutableLiveData<>(0L);
    private final MutableLiveData<Long> totalExpense = new MutableLiveData<>(0L);
    private final MutableLiveData<Long> availableBalance = new MutableLiveData<>(0L);
    private final MutableLiveData<List<Transaction>> recentTransactions =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<Long> selectedDate =
            new MutableLiveData<>(System.currentTimeMillis());
    private final MutableLiveData<Long> dailyIncome = new MutableLiveData<>(0L);
    private final MutableLiveData<Long> dailyExpense = new MutableLiveData<>(0L);
    private final MutableLiveData<LoadState> loadState =
            new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> remoteError = new MutableLiveData<>();
    private List<Transaction> serverSnapshot = new ArrayList<>();
    private boolean hasLoaded;
    private boolean refreshing;
    private int refreshGeneration;

    public HomeViewModel(@NonNull Application application) {
        super(application);
        transactionRepository = new RemoteTransactionRepository(application);
        statisticsRepository = new RemoteStatisticsRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        financialCycleStartDay = session.getFinancialCycleStartDay();
        refreshRemoteTransactions();
    }

    public LiveData<Long> getTotalIncome() { return totalIncome; }
    public LiveData<Long> getTotalExpense() { return totalExpense; }
    public LiveData<Long> getAvailableBalance() { return availableBalance; }
    public LiveData<List<Transaction>> getRecentTransactions() { return recentTransactions; }
    public LiveData<Long> getSelectedDate() { return selectedDate; }
    public LiveData<Long> getDailyIncome() { return dailyIncome; }
    public LiveData<Long> getDailyExpense() { return dailyExpense; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getRemoteError() { return remoteError; }

    public void setSelectedDate(long date) {
        selectedDate.setValue(date);
        publishTransactions();
    }

    public boolean isRemoteTransaction(Transaction transaction) {
        return transaction != null && transaction.getRemoteId() != null;
    }

    public void deleteTransaction(Transaction transaction) {
        if (!isRemoteTransaction(transaction)) return;
        transactionRepository.delete(transaction.getRemoteId(), transaction.getVersion(),
                new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) { refreshRemoteTransactions(); }
            @Override public void onError(ApiError error) { remoteError.setValue(error.getMessage()); }
        });
    }

    public void refreshRemoteTransactions() {
        if (!authenticated || refreshing) {
            if (!authenticated) {
                loadState.setValue(LoadState.ERROR);
                remoteError.setValue("Phiên đăng nhập không hợp lệ");
            }
            return;
        }
        refreshing = true;
        final int generation = ++refreshGeneration;
        if (!hasLoaded) loadState.setValue(LoadState.LOADING);

        // Repository transparently follows every backend page.
        transactionRepository.getTransactions(userId, new RemoteCallback<List<Transaction>>() {
            @Override
            public void onSuccess(List<Transaction> value) {
                if (generation != refreshGeneration) return;
                refreshing = false;
                hasLoaded = true;
                serverSnapshot = value == null ? new ArrayList<>() : new ArrayList<>(value);
                // List.sort is stable. The API already orders same-day entries by CreatedAt,
                // so sorting only by transaction date preserves the true newest-first tie order.
                serverSnapshot.sort(Comparator
                        .comparingLong(Transaction::getDate)
                        .reversed());
                publishTransactions();
            }

            @Override
            public void onError(ApiError error) {
                if (generation != refreshGeneration) return;
                refreshing = false;
                remoteError.setValue(error.getMessage());
                if (!hasLoaded) loadState.setValue(LoadState.ERROR);
            }
        });

        LocalDate today = LocalDate.now();
        LocalDate cycleStart = FinancialCycleUtils.startFor(today, financialCycleStartDay);
        statisticsRepository.getMonthlySummary(cycleStart.getYear(),
                new RemoteCallback<List<MonthlySummary>>() {
                    @Override
                    public void onSuccess(List<MonthlySummary> summaries) {
                        if (generation != refreshGeneration) return;
                        publishMonthlyTotals(summaries);
                    }

                    @Override
                    public void onError(ApiError error) {
                        if (generation != refreshGeneration) return;
                        remoteError.setValue(error.getMessage());
                    }
                });
    }

    private void publishMonthlyTotals(List<MonthlySummary> summaries) {
        String currentMonth = FinancialCycleUtils.keyFor(LocalDate.now(), financialCycleStartDay);
        long income = 0L;
        long expense = 0L;
        if (summaries != null) {
            for (MonthlySummary summary : summaries) {
                if (summary != null && currentMonth.equals(summary.getMonthYear())) {
                    income = summary.getTotalIncome();
                    expense = summary.getTotalExpense();
                    break;
                }
            }
        }
        totalIncome.setValue(income);
        totalExpense.setValue(expense);
        availableBalance.setValue(income - expense);
    }

    private void publishTransactions() {
        long day = selectedDate.getValue() == null
                ? System.currentTimeMillis() : selectedDate.getValue();
        long dayStart = DateUtils.getStartOfDay(day);
        long dayEnd = DateUtils.getEndOfDay(day);
        long dayIncome = 0L;
        long dayExpense = 0L;
        List<Transaction> recent = new ArrayList<>();
        for (Transaction transaction : serverSnapshot) {
            if (transaction.getDate() >= dayStart && transaction.getDate() <= dayEnd) {
                if (transaction.getType() == TransactionType.INCOME) dayIncome += transaction.getAmount();
                else dayExpense += transaction.getAmount();
            }
            if (recent.size() < 5) recent.add(transaction);
        }
        dailyIncome.setValue(dayIncome);
        dailyExpense.setValue(dayExpense);
        recentTransactions.setValue(recent);
        loadState.setValue(serverSnapshot.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
    }
}
