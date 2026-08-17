package com.example.appquanlychitieu.ui.transaction;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;

import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionRequestDto;
import com.example.appquanlychitieu.data.repository.RemoteCategoryRepository;
import com.example.appquanlychitieu.data.repository.RemoteTransactionRepository;

import java.util.List;

/** Owns all data operations for the add/edit transaction screen. */
public final class TransactionFormViewModel extends AndroidViewModel {
    private final RemoteTransactionRepository transactions;
    private final RemoteCategoryRepository categories;

    public TransactionFormViewModel(@NonNull Application application) {
        super(application);
        transactions = new RemoteTransactionRepository(application);
        categories = new RemoteCategoryRepository(application);
    }

    public void loadCategories(String type, RemoteCallback<List<CategoryDto>> callback) {
        categories.getCategories(type, callback);
    }

    public void createCategory(CategoryRequestDto request, RemoteCallback<CategoryDto> callback) {
        categories.create(request, callback);
    }

    public void save(String transactionId, long version, TransactionRequestDto request,
                     RemoteCallback<TransactionDto> callback) {
        if (transactionId == null || transactionId.trim().isEmpty())
            transactions.create(request, callback);
        else
            transactions.update(transactionId, version, request, callback);
    }
}
