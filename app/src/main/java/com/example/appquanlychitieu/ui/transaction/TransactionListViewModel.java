package com.example.appquanlychitieu.ui.transaction;

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
import com.example.appquanlychitieu.util.SessionManager;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class TransactionListViewModel extends AndroidViewModel {
    public static final class FilterOptions {
        final String type;
        final long startDate;
        final long endDate;
        final String keyword;

        FilterOptions(String type, long startDate, long endDate, String keyword) {
            this.type = type;
            this.startDate = startDate;
            this.endDate = endDate;
            this.keyword = keyword;
        }
    }

    private final RemoteTransactionRepository repository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<List<Transaction>> transactions =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<LoadState> loadState =
            new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> remoteError = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();

    private FilterOptions filters = new FilterOptions("ALL", 0L, Long.MAX_VALUE, "");
    private List<Transaction> serverSnapshot = new ArrayList<>();
    private boolean hasLoaded;
    private boolean refreshing;

    public TransactionListViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteTransactionRepository(application);
        SessionManager session = new SessionManager(application);
        userId = session.getUserId();
        authenticated = session.hasAuthToken();
        refreshRemoteTransactions();
    }

    public LiveData<List<Transaction>> getTransactions() { return transactions; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getRemoteError() { return remoteError; }
    public LiveData<String> getFeedback() { return feedback; }

    public void setFilterType(String type) {
        filters = new FilterOptions(type, filters.startDate, filters.endDate, filters.keyword);
        publish();
    }

    public void setDateRange(long start, long end) {
        filters = new FilterOptions(filters.type, start, end, filters.keyword);
        publish();
    }

    public void setSearchQuery(String keyword) {
        filters = new FilterOptions(filters.type, filters.startDate, filters.endDate, keyword);
        publish();
    }

    public boolean isRemoteTransaction(Transaction transaction) {
        return transaction != null && transaction.getRemoteId() != null
                && !transaction.getRemoteId().trim().isEmpty();
    }

    public void deleteTransaction(Transaction transaction) {
        if (!isRemoteTransaction(transaction)) {
            remoteError.setValue("Không tìm thấy mã giao dịch trên máy chủ");
            return;
        }
        repository.delete(transaction.getRemoteId(), transaction.getVersion(), new RemoteCallback<Void>() {
            @Override
            public void onSuccess(Void value) {
                feedback.setValue("Đã xóa giao dịch");
                refreshRemoteTransactions();
            }

            @Override
            public void onError(ApiError error) {
                remoteError.setValue(error.getMessage());
            }
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
        List<Transaction> filtered = new ArrayList<>();
        String keyword = filters.keyword == null
                ? "" : filters.keyword.trim().toLowerCase(Locale.ROOT);
        for (Transaction transaction : serverSnapshot) {
            if ("EXPENSE".equals(filters.type) && transaction.getType() != TransactionType.EXPENSE) continue;
            if ("INCOME".equals(filters.type) && transaction.getType() != TransactionType.INCOME) continue;
            if (transaction.getDate() < filters.startDate || transaction.getDate() > filters.endDate) continue;
            String note = transaction.getNote() == null ? "" : transaction.getNote();
            String category = transaction.getRemoteCategoryName() == null
                    ? "" : transaction.getRemoteCategoryName();
            if (!keyword.isEmpty()
                    && !(note + " " + category).toLowerCase(Locale.ROOT).contains(keyword)) continue;
            filtered.add(transaction);
        }
        transactions.setValue(filtered);
        loadState.setValue(filtered.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
    }
}
