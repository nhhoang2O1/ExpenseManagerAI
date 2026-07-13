package com.example.appquanlychitieu.ui.receipt;

import android.app.Application;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.ConfirmReceiptRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.repository.ReceiptRepository;

import java.util.Collections;
import java.util.List;

public class ReceiptViewModel extends AndroidViewModel {
    private static final long POLL_INTERVAL_MS = 1500L;
    private static final int MAX_POLL_ATTEMPTS = 40;

    private final ReceiptRepository repository;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final MutableLiveData<UiState> state =
            new MutableLiveData<>(new UiState(Phase.PICK_IMAGE, null, null));
    private final MutableLiveData<List<CategoryDto>> categories =
            new MutableLiveData<>(Collections.emptyList());
    private int pollAttempts;
    private Runnable pendingPoll;

    public ReceiptViewModel(@NonNull Application application) {
        super(application);
        repository = new ReceiptRepository(application);
        loadCategories();
    }

    public LiveData<UiState> getState() {
        return state;
    }

    public LiveData<List<CategoryDto>> getCategories() {
        return categories;
    }

    public void start(Uri imageUri) {
        UiState current = state.getValue();
        if (current != null && (current.phase == Phase.UPLOADING
                || current.phase == Phase.PROCESSING
                || current.phase == Phase.CONFIRMING)) return;
        cancelPolling();
        pollAttempts = 0;
        state.setValue(new UiState(Phase.UPLOADING, null, null));
        repository.upload(imageUri, new RemoteCallback<ReceiptDto>() {
            @Override
            public void onSuccess(ReceiptDto receipt) {
                state.setValue(new UiState(Phase.PROCESSING, receipt, null));
                repository.process(receipt.id, receiptCallback);
            }

            @Override
            public void onError(ApiError error) {
                state.setValue(new UiState(Phase.ERROR, null, error.getMessage()));
            }
        });
    }

    public void retry() {
        UiState current = state.getValue();
        if (current == null || current.receipt == null || current.receipt.id == null) {
            return;
        }
        cancelPolling();
        pollAttempts = 0;
        state.setValue(new UiState(Phase.PROCESSING, current.receipt, null));
        repository.retry(current.receipt.id, receiptCallback);
    }

    public void confirm(ConfirmReceiptRequestDto request) {
        UiState current = state.getValue();
        if (current == null || current.receipt == null || current.receipt.id == null) {
            return;
        }
        if (current.phase == Phase.CONFIRMING || current.phase == Phase.CONFIRMED) return;
        state.setValue(new UiState(Phase.CONFIRMING, current.receipt, null));
        repository.confirm(current.receipt.id, request, new RemoteCallback<TransactionDto>() {
            @Override
            public void onSuccess(TransactionDto value) {
                state.setValue(new UiState(Phase.CONFIRMED, current.receipt, null));
            }

            @Override
            public void onError(ApiError error) {
                state.setValue(new UiState(Phase.REVIEW, current.receipt, error.getMessage()));
            }
        });
    }

    public void reset() {
        cancelPolling();
        state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
    }

    private final RemoteCallback<ReceiptDto> receiptCallback = new RemoteCallback<ReceiptDto>() {
        @Override
        public void onSuccess(ReceiptDto receipt) {
            handleReceipt(receipt);
        }

        @Override
        public void onError(ApiError error) {
            UiState current = state.getValue();
            ReceiptDto receipt = current == null ? null : current.receipt;
            state.setValue(new UiState(Phase.ERROR, receipt, error.getMessage()));
        }
    };

    private void handleReceipt(ReceiptDto receipt) {
        String status = receipt.status == null ? "" : receipt.status;
        if ("REVIEW_REQUIRED".equalsIgnoreCase(status)
                || "OCR_FAILED".equalsIgnoreCase(status)) {
            state.setValue(new UiState(Phase.REVIEW, receipt, null));
        } else if ("CONFIRMED".equalsIgnoreCase(status)) {
            state.setValue(new UiState(Phase.CONFIRMED, receipt, null));
        } else {
            state.setValue(new UiState(Phase.PROCESSING, receipt, null));
            schedulePoll(receipt.id);
        }
    }

    private void schedulePoll(String receiptId) {
        if (receiptId == null || pollAttempts >= MAX_POLL_ATTEMPTS) {
            UiState current = state.getValue();
            state.setValue(new UiState(
                    Phase.ERROR,
                    current == null ? null : current.receipt,
                    "OCR processing timed out"));
            return;
        }
        pollAttempts++;
        pendingPoll = () -> repository.get(receiptId, receiptCallback);
        handler.postDelayed(pendingPoll, POLL_INTERVAL_MS);
    }

    private void loadCategories() {
        repository.getExpenseCategories(new RemoteCallback<List<CategoryDto>>() {
            @Override
            public void onSuccess(List<CategoryDto> value) {
                categories.setValue(value);
            }

            @Override
            public void onError(ApiError error) {
                categories.setValue(Collections.emptyList());
            }
        });
    }

    private void cancelPolling() {
        if (pendingPoll != null) {
            handler.removeCallbacks(pendingPoll);
            pendingPoll = null;
        }
    }

    @Override
    protected void onCleared() {
        cancelPolling();
        super.onCleared();
    }

    public enum Phase {
        PICK_IMAGE,
        UPLOADING,
        PROCESSING,
        REVIEW,
        CONFIRMING,
        CONFIRMED,
        ERROR
    }

    public static final class UiState {
        public final Phase phase;
        public final ReceiptDto receipt;
        public final String error;

        UiState(Phase phase, ReceiptDto receipt, String error) {
            this.phase = phase;
            this.receipt = receipt;
            this.error = error;
        }
    }
}
