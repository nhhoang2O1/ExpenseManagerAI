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
import java.text.Collator;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

public class TransactionListViewModel extends AndroidViewModel {
    public static final class FilterOptions {
        final String type;
        final long startDate;
        final long endDate;
        final String category;
        final TransactionType categoryType;

        FilterOptions(String type, long startDate, long endDate, String category,
                      TransactionType categoryType) {
            this.type = type;
            this.startDate = startDate;
            this.endDate = endDate;
            this.category = category;
            this.categoryType = categoryType;
        }
    }

    public static final class CategoryFilterOption {
        private final String name;
        private final TransactionType type;

        CategoryFilterOption(String name, TransactionType type) {
            this.name = name;
            this.type = type;
        }

        public String getName() { return name; }
        public TransactionType getType() { return type; }
    }

    private final RemoteTransactionRepository repository;
    private final long userId;
    private final boolean authenticated;
    private final MutableLiveData<List<Transaction>> transactions =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<List<CategoryFilterOption>> categoryOptions =
            new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<String> selectedCategory = new MutableLiveData<>("");
    private TransactionType selectedCategoryType;
    private final MutableLiveData<LoadState> loadState =
            new MutableLiveData<>(LoadState.LOADING);
    private final MutableLiveData<String> remoteError = new MutableLiveData<>();
    private final MutableLiveData<String> feedback = new MutableLiveData<>();

    private FilterOptions filters = new FilterOptions("ALL", 0L, Long.MAX_VALUE, "", null);
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
    public LiveData<List<CategoryFilterOption>> getCategoryOptions() { return categoryOptions; }
    public LiveData<String> getSelectedCategory() { return selectedCategory; }
    public TransactionType getSelectedCategoryType() { return selectedCategoryType; }
    public LiveData<LoadState> getLoadState() { return loadState; }
    public LiveData<String> getRemoteError() { return remoteError; }
    public LiveData<String> getFeedback() { return feedback; }

    public void setFilterType(String type) {
        TransactionType requiredType = "EXPENSE".equals(type) ? TransactionType.EXPENSE
                : "INCOME".equals(type) ? TransactionType.INCOME : null;
        String category = filters.category;
        TransactionType categoryType = filters.categoryType;
        if (requiredType != null && categoryType != null && requiredType != categoryType) {
            category = "";
            categoryType = null;
            selectedCategory.setValue("");
            selectedCategoryType = null;
        }
        filters = new FilterOptions(type, filters.startDate, filters.endDate, category, categoryType);
        publishCategoryOptions();
        publish();
    }

    public void setDateRange(long start, long end) {
        filters = new FilterOptions(filters.type, start, end, filters.category, filters.categoryType);
        publish();
    }

    public void setCategoryFilter(String category, TransactionType categoryType) {
        selectedCategory.setValue(category == null ? "" : category);
        selectedCategoryType = categoryType;
        filters = new FilterOptions(filters.type, filters.startDate, filters.endDate,
                category, categoryType);
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
                publishCategoryOptions();
                publish();
                remoteError.setValue(null);
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
        String categoryFilter = filters.category == null ? "" : filters.category.trim();
        for (Transaction transaction : serverSnapshot) {
            if ("EXPENSE".equals(filters.type) && transaction.getType() != TransactionType.EXPENSE) continue;
            if ("INCOME".equals(filters.type) && transaction.getType() != TransactionType.INCOME) continue;
            if (transaction.getDate() < filters.startDate || transaction.getDate() > filters.endDate) continue;
            String category = transaction.getRemoteCategoryName() == null
                    ? "" : transaction.getRemoteCategoryName();
            if (!categoryFilter.isEmpty() && !category.equalsIgnoreCase(categoryFilter)) continue;
            if (!categoryFilter.isEmpty() && filters.categoryType != null
                    && transaction.getType() != filters.categoryType) continue;
            filtered.add(transaction);
        }
        filtered.sort(Comparator.comparingLong(Transaction::getDate).reversed());
        transactions.setValue(filtered);
        loadState.setValue(filtered.isEmpty() ? LoadState.EMPTY : LoadState.CONTENT);
    }

    private void publishCategoryOptions() {
        List<CategoryFilterOption> result = new ArrayList<>();
        Set<String> seen = new HashSet<>();
        for (Transaction transaction : serverSnapshot) {
            String name = transaction.getRemoteCategoryName();
            if (name == null || name.trim().isEmpty()) continue;
            TransactionType type = transaction.getType();
            if ("EXPENSE".equals(filters.type) && type != TransactionType.EXPENSE) continue;
            if ("INCOME".equals(filters.type) && type != TransactionType.INCOME) continue;
            String normalizedName = name.trim();
            String key = type.name() + "\u0000" + normalizedName.toLowerCase(Locale.ROOT);
            if (seen.add(key)) result.add(new CategoryFilterOption(normalizedName, type));
        }
        Collator collator = Collator.getInstance(new Locale("vi", "VN"));
        collator.setStrength(Collator.PRIMARY);
        result.sort(Comparator
                .comparingInt((CategoryFilterOption option) ->
                        option.type == TransactionType.EXPENSE ? 0 : 1)
                .thenComparing(option -> option.name, collator));
        categoryOptions.setValue(result);
    }
}
