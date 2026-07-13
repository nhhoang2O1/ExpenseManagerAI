package com.example.appquanlychitieu.ui.home;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.repository.RemoteTransactionRepository;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

public class HomeViewModel extends AndroidViewModel {
    private final RemoteTransactionRepository repository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<Double> totalIncome = new MutableLiveData<>(0d);
    private final MutableLiveData<Double> totalExpense = new MutableLiveData<>(0d);
    private final MutableLiveData<Double> balance = new MutableLiveData<>(0d);
    private final MutableLiveData<List<Transaction>> recentTransactions =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<Long> selectedDate =
            new MutableLiveData<>(System.currentTimeMillis());
    private final MutableLiveData<Double> dailyIncome = new MutableLiveData<>(0d);
    private final MutableLiveData<Double> dailyExpense = new MutableLiveData<>(0d);
    private final MutableLiveData<LoadState> loadState =
            new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> remoteError = new MutableLiveData<>();
    private List<Transaction> serverSnapshot = new ArrayList<>();
    private boolean hasLoaded;
    private boolean refreshing;

    public HomeViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteTransactionRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        refreshRemoteTransactions();
    }

    public LiveData<Double> getTotalIncome() { return totalIncome; }
    public LiveData<Double> getTotalExpense() { return totalExpense; }
    public LiveData<Double> getBalance() { return balance; }
    public LiveData<List<Transaction>> getRecentTransactions() { return recentTransactions; }
    public LiveData<Long> getSelectedDate() { return selectedDate; }
    public LiveData<Double> getDailyIncome() { return dailyIncome; }
    public LiveData<Double> getDailyExpense() { return dailyExpense; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getRemoteError() { return remoteError; }

    public void setSelectedDate(long date) {
        selectedDate.setValue(date);
        publish();
    }

    public boolean isRemoteTransaction(Transaction transaction) {
        return transaction != null && transaction.getRemoteId() != null;
    }

    public void deleteTransaction(Transaction transaction) {
        if (!isRemoteTransaction(transaction)) return;
        repository.delete(transaction.getRemoteId(), new RemoteCallback<Void>() {
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
        if (!hasLoaded) loadState.setValue(LoadState.LOADING);
        repository.getTransactions(userId, new RemoteCallback<List<Transaction>>() {
            @Override
            public void onSuccess(List<Transaction> value) {
                refreshing = false;
                hasLoaded = true;
                serverSnapshot = value == null ? new ArrayList<>() : new ArrayList<>(value);
                serverSnapshot.sort(Comparator.comparingLong(Transaction::getDate).reversed());
                remoteError.setValue(null);
                publish();
            }

            @Override
            public void onError(ApiError error) {
                refreshing = false;
                remoteError.setValue(error.getMessage());
                if (!hasLoaded) loadState.setValue(LoadState.ERROR);
            }
        });
    }

    private void publish() {
        long monthStart = DateUtils.getStartOfCurrentMonth();
        long monthEnd = DateUtils.getEndOfCurrentMonth();
        long day = selectedDate.getValue() == null ? System.currentTimeMillis() : selectedDate.getValue();
        long dayStart = DateUtils.getStartOfDay(day);
        long dayEnd = DateUtils.getEndOfDay(day);
        double income = 0d;
        double expense = 0d;
        double dayIncome = 0d;
        double dayExpense = 0d;
        List<Transaction> recent = new ArrayList<>();
        for (Transaction transaction : serverSnapshot) {
            if (transaction.getDate() >= monthStart && transaction.getDate() <= monthEnd) {
                if (transaction.getType() == TransactionType.INCOME) income += transaction.getAmount();
                else expense += transaction.getAmount();
            }
            if (transaction.getDate() >= dayStart && transaction.getDate() <= dayEnd) {
                if (transaction.getType() == TransactionType.INCOME) dayIncome += transaction.getAmount();
                else dayExpense += transaction.getAmount();
            }
            if (recent.size() < 5) recent.add(transaction);
        }
        totalIncome.setValue(income);
        totalExpense.setValue(expense);
        balance.setValue(income - expense);
        dailyIncome.setValue(dayIncome);
        dailyExpense.setValue(dayExpense);
        recentTransactions.setValue(recent);
        loadState.setValue(serverSnapshot.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
    }
}
