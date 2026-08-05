package com.example.appquanlychitieu.ui.receipt;

import android.app.Application;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
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
import java.util.UUID;
import okhttp3.ResponseBody;

/**
 * Owns the complete receipt state machine. Network calls are deliberately
 * asynchronous: a 202 response only advances the state to PROCESSING and a
 * lightweight GET poll observes the backend worker. The draft store makes
 * every transition restartable after Android kills the process.
 */
public class ReceiptViewModel extends AndroidViewModel {
    private static final long POLL_INTERVAL_MS = 1_500L;
    private static final int MAX_POLL_ATTEMPTS = 40;
    private static final String OCR_TIMEOUT_MESSAGE = "OCR processing timed out";
    private static final String RECEIPT_NOT_FOUND_MESSAGE = "Receipt no longer exists";

    private final ReceiptRepository repository;
    private final ReceiptDraftStore draftStore;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final MutableLiveData<UiState> state =
            new MutableLiveData<>(new UiState(Phase.PICK_IMAGE, null, null));
    private final MutableLiveData<List<CategoryDto>> categories =
            new MutableLiveData<>(Collections.emptyList());
    private final MutableLiveData<byte[]> serverImage = new MutableLiveData<>();

    private int pollAttempts;
    private Runnable pendingPoll;
    private long operationToken;
    private long canceledUploadToken = -1L;
    @Nullable private String receiptId;
    @Nullable private String imageUri;
    @Nullable private String idempotencyKey;
    @Nullable private String loadedImageReceiptId;

    public ReceiptViewModel(@NonNull Application application) {
        super(application);
        repository = new ReceiptRepository(application);
        draftStore = new ReceiptDraftStore(application);
        loadCategories();
        resumeDraft();
    }

    public LiveData<UiState> getState() {
        return state;
    }

    public LiveData<List<CategoryDto>> getCategories() {
        return categories;
    }

    public LiveData<byte[]> getServerImage() { return serverImage; }

    public void loadServerImage(@NonNull String id) {
        if (id.equals(loadedImageReceiptId)) return;
        loadedImageReceiptId = id;
        repository.downloadImage(id, new RemoteCallback<ResponseBody>() {
            @Override public void onSuccess(ResponseBody value) {
                try { serverImage.setValue(value.bytes()); }
                catch (Exception exception) { loadedImageReceiptId = null; }
            }
            @Override public void onError(ApiError error) { loadedImageReceiptId = null; }
        });
    }

    /** URI persisted by the draft, used by the Activity to restore its preview. */
    @Nullable
    public Uri getDraftImageUri() {
        return imageUri == null ? null : Uri.parse(imageUri);
    }

    public void start(@NonNull Uri selectedUri) {
        UiState current = state.getValue();
        if (current != null && isBusy(current.phase)) {
            return;
        }

        cancelPolling();
        long token = ++operationToken;
        pollAttempts = 0;
        imageUri = selectedUri.toString();
        idempotencyKey = UUID.randomUUID().toString();
        receiptId = null;
        persist(Phase.UPLOADING, null);
        state.setValue(new UiState(Phase.UPLOADING, null, null));

        repository.upload(selectedUri, idempotencyKey, new RemoteCallback<ReceiptDto>() {
            @Override
            public void onSuccess(ReceiptDto receipt) {
                if (token != operationToken) {
                    // Only an explicit cancel may clean up an upload accepted
                    // after the UI moved on. Lifecycle changes must leave the
                    // durable draft and remote receipt recoverable.
                    if (ReceiptCallbackPolicy.shouldDeleteLateUpload(
                            token, operationToken, canceledUploadToken)) {
                        deleteLateReceipt(receipt);
                    }
                    return;
                }
                if (receipt == null || receipt.id == null) {
                    showError(token, null, "Upload returned no receipt id");
                    return;
                }
                receiptId = receipt.id;
                persist(Phase.PROCESSING, receipt);
                state.setValue(new UiState(Phase.PROCESSING, receipt, null));
                process(token, receipt.id);
            }

            @Override
            public void onError(ApiError error) {
                if (token != operationToken) return;
                showError(token, null, error.getMessage());
            }
        });
    }

    public void retry() {
        UiState current = state.getValue();
        if (current == null || current.receipt == null || current.receipt.id == null
                || isBusy(current.phase)) {
            return;
        }

        cancelPolling();
        long token = ++operationToken;
        pollAttempts = 0;
        receiptId = current.receipt.id;
        persist(Phase.PROCESSING, current.receipt);
        state.setValue(new UiState(Phase.PROCESSING, current.receipt, null));
        repository.retry(receiptId, receiptCallback(token));
    }

    public void confirm(@NonNull ConfirmReceiptRequestDto request) {
        UiState current = state.getValue();
        if (current == null || current.receipt == null || current.receipt.id == null
                || current.phase == Phase.CONFIRMING || current.phase == Phase.CONFIRMED) {
            return;
        }

        long token = ++operationToken;
        receiptId = current.receipt.id;
        persist(Phase.CONFIRMING, current.receipt);
        state.setValue(new UiState(Phase.CONFIRMING, current.receipt, null));
        repository.confirm(receiptId, request, new RemoteCallback<TransactionDto>() {
            @Override
            public void onSuccess(TransactionDto value) {
                if (token != operationToken) return;
                cancelPolling();
                draftStore.clear();
                state.setValue(new UiState(Phase.CONFIRMED, current.receipt, null));
            }

            @Override
            public void onError(ApiError error) {
                if (token != operationToken) return;
                // A timeout can happen after the server committed. Keeping
                // the receipt draft lets the user retry; confirmation is
                // idempotent on the backend by receipt id.
                persist(Phase.REVIEW, current.receipt);
                state.setValue(new UiState(Phase.REVIEW, current.receipt, error.getMessage()));
            }
        });
    }

    /**
     * Deletes the remote receipt (if one exists), clears the durable draft,
     * and returns to image picking. The callback is invoked only after the
     * server acknowledges deletion; callers can therefore avoid losing a
     * draft when the device is offline.
     */
    public void deleteAndReset(@Nullable RemoteCallback<Void> callback) {
        cancelPolling();
        UiState stateBeforeCancel = state.getValue();
        if (stateBeforeCancel != null && stateBeforeCancel.phase == Phase.UPLOADING) {
            canceledUploadToken = operationToken;
        }
        long token = ++operationToken;
        String id = receiptId;
        if (id == null) {
            ReceiptDraftStore.Draft draft = draftStore.load();
            id = draft == null ? null : draft.receiptId;
        }

        if (id == null) {
            draftStore.clear();
            receiptId = null;
            imageUri = null;
            idempotencyKey = null;
            state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
            notifySuccess(callback);
            return;
        }

        receiptId = id;
        ReceiptDto currentReceipt = currentReceipt();
        persist(Phase.CANCELING, currentReceipt);
        state.setValue(new UiState(Phase.CANCELING, currentReceipt, null));
        repository.delete(id, new RemoteCallback<Void>() {
            @Override
            public void onSuccess(Void value) {
                if (token != operationToken) return;
                draftStore.clear();
                receiptId = null;
                imageUri = null;
                idempotencyKey = null;
                loadedImageReceiptId = null;
                serverImage.setValue(null);
                state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
                notifySuccess(callback);
            }

            @Override
            public void onError(ApiError error) {
                if (token != operationToken) return;
                persist(Phase.CANCELING, currentReceipt);
                state.setValue(new UiState(Phase.ERROR, currentReceipt, error.getMessage()));
                notifyError(callback, error);
            }
        });
    }

    public void reset() {
        deleteAndReset(null);
    }

    private void process(long token, String id) {
        repository.process(id, receiptCallback(token));
    }

    private RemoteCallback<ReceiptDto> receiptCallback(long token) {
        return new RemoteCallback<ReceiptDto>() {
            @Override
            public void onSuccess(ReceiptDto receipt) {
                // Process/retry/poll responses are read-side observations.
                // A stale observation must never mutate server state.
                if (token != operationToken) return;
                handleReceipt(receipt, token);
            }

            @Override
            public void onError(ApiError error) {
                if (token != operationToken) return;
                ReceiptDto current = currentReceipt();
                if (error.getStatusCode() == 404) {
                    draftStore.clear();
                    receiptId = null;
                    state.setValue(new UiState(Phase.ERROR, null, RECEIPT_NOT_FOUND_MESSAGE));
                    return;
                }
                showError(token, current, error.getMessage());
            }
        };
    }

    private void handleReceipt(@Nullable ReceiptDto receipt, long token) {
        if (receipt == null || receipt.id == null) {
            showError(token, currentReceipt(), "Server returned an invalid receipt");
            return;
        }
        receiptId = receipt.id;
        String status = receipt.status == null ? "" : receipt.status;
        if ("CONFIRMED".equalsIgnoreCase(status)) {
            cancelPolling();
            draftStore.clear();
            state.setValue(new UiState(Phase.CONFIRMED, receipt, null));
        } else if ("REVIEW_REQUIRED".equalsIgnoreCase(status)
                || "OCR_FAILED".equalsIgnoreCase(status)) {
            persist(Phase.REVIEW, receipt);
            state.setValue(new UiState(Phase.REVIEW, receipt, null));
        } else {
            persist(Phase.PROCESSING, receipt);
            state.setValue(new UiState(Phase.PROCESSING, receipt, null));
            schedulePoll(receipt.id, token);
        }
    }

    private void schedulePoll(String id, long token) {
        if (id == null || token != operationToken) return;
        if (pollAttempts >= MAX_POLL_ATTEMPTS) {
            UiState current = state.getValue();
            showError(token, current == null ? null : current.receipt, OCR_TIMEOUT_MESSAGE);
            return;
        }
        pollAttempts++;
        pendingPoll = () -> {
            pendingPoll = null;
            if (token == operationToken) {
                repository.get(id, receiptCallback(token));
            }
        };
        handler.postDelayed(pendingPoll, POLL_INTERVAL_MS);
    }

    private void resumeDraft() {
        ReceiptDraftStore.Draft draft = draftStore.load();
        if (draft == null) return;
        imageUri = draft.imageUri;
        receiptId = draft.receiptId;
        idempotencyKey = draft.idempotencyKey;
        long token = ++operationToken;

        Phase phase = parsePhase(draft.phase);
        if (phase == Phase.CANCELING) {
            state.setValue(new UiState(Phase.CANCELING, placeholderReceipt(draft), null));
            if (receiptId == null) {
                draftStore.clear();
                state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
            } else {
                repository.delete(receiptId, new RemoteCallback<Void>() {
                    @Override
                    public void onSuccess(Void value) {
                        if (token != operationToken) return;
                        draftStore.clear();
                        state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
                    }

                    @Override
                    public void onError(ApiError error) {
                        if (token != operationToken) return;
                        state.setValue(new UiState(
                                Phase.ERROR,
                                placeholderReceipt(draft),
                                error.getMessage()));
                    }
                });
            }
            return;
        }

        ReceiptDto placeholder = placeholderReceipt(draft);
        if (phase == Phase.UPLOADING && imageUri != null) {
            state.setValue(new UiState(Phase.UPLOADING, placeholder, null));
            String key = idempotencyKey == null ? UUID.randomUUID().toString() : idempotencyKey;
            idempotencyKey = key;
            draftStore.save(null, Phase.UPLOADING.name(), imageUri, key, null);
            Uri uri = Uri.parse(imageUri);
            repository.upload(uri, key, new RemoteCallback<ReceiptDto>() {
                @Override
                public void onSuccess(ReceiptDto receipt) {
                    if (token != operationToken) {
                        if (ReceiptCallbackPolicy.shouldDeleteLateUpload(
                                token, operationToken, canceledUploadToken)) {
                            deleteLateReceipt(receipt);
                        }
                        return;
                    }
                    if (receipt == null || receipt.id == null) {
                        showError(token, null, "Upload returned no receipt id");
                        return;
                    }
                    receiptId = receipt.id;
                    persist(Phase.PROCESSING, receipt);
                    state.setValue(new UiState(Phase.PROCESSING, receipt, null));
                    process(token, receipt.id);
                }

                @Override
                public void onError(ApiError error) {
                    if (token == operationToken) showError(token, placeholder, error.getMessage());
                }
            });
            return;
        }

        if (receiptId != null) {
            state.setValue(new UiState(phase, placeholder, null));
            repository.get(receiptId, receiptCallback(token));
        } else {
            draftStore.clear();
            state.setValue(new UiState(Phase.PICK_IMAGE, null, null));
        }
    }

    private void loadCategories() {
        repository.getExpenseCategories(new RemoteCallback<List<CategoryDto>>() {
            @Override
            public void onSuccess(List<CategoryDto> value) {
                categories.setValue(value == null ? Collections.emptyList() : value);
            }

            @Override
            public void onError(ApiError error) {
                categories.setValue(Collections.emptyList());
            }
        });
    }

    private void persist(Phase phase, @Nullable ReceiptDto receipt) {
        if (receipt != null && receipt.id != null) receiptId = receipt.id;
        draftStore.save(
                receiptId,
                phase.name(),
                imageUri,
                idempotencyKey,
                receipt == null ? null : receipt.status);
    }

    private void showError(long token, @Nullable ReceiptDto receipt, String message) {
        if (token != operationToken) return;
        Phase currentPhase = state.getValue() == null
                ? Phase.ERROR
                : state.getValue().phase;
        Phase durablePhase = currentPhase == Phase.UPLOADING
                ? Phase.UPLOADING
                : (currentPhase == Phase.CONFIRMING ? Phase.CONFIRMING : Phase.REVIEW);
        if (receiptId != null || imageUri != null) persist(durablePhase, receipt);
        state.setValue(new UiState(Phase.ERROR, receipt, message));
    }

    @Nullable
    private ReceiptDto currentReceipt() {
        UiState current = state.getValue();
        return current == null ? null : current.receipt;
    }

    @Nullable
    private static ReceiptDto placeholderReceipt(ReceiptDraftStore.Draft draft) {
        if (draft.receiptId == null) return null;
        ReceiptDto receipt = new ReceiptDto();
        receipt.id = draft.receiptId;
        receipt.status = draft.status;
        return receipt;
    }

    private static Phase parsePhase(@Nullable String value) {
        if (value == null) return Phase.PICK_IMAGE;
        try {
            return Phase.valueOf(value);
        } catch (IllegalArgumentException ignored) {
            return Phase.PICK_IMAGE;
        }
    }

    private void deleteLateReceipt(@Nullable ReceiptDto receipt) {
        if (receipt == null || receipt.id == null) return;
        repository.delete(receipt.id, new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) { }
            @Override public void onError(ApiError error) { }
        });
    }

    private static boolean isBusy(Phase phase) {
        return phase == Phase.UPLOADING
                || phase == Phase.PROCESSING
                || phase == Phase.CONFIRMING
                || phase == Phase.CANCELING;
    }

    private static void notifySuccess(@Nullable RemoteCallback<Void> callback) {
        if (callback != null) callback.onSuccess(null);
    }

    private static void notifyError(@Nullable RemoteCallback<Void> callback, ApiError error) {
        if (callback != null) callback.onError(error);
    }

    private void cancelPolling() {
        if (pendingPoll != null) {
            handler.removeCallbacks(pendingPoll);
            pendingPoll = null;
        }
    }

    @Override
    protected void onCleared() {
        operationToken++;
        cancelPolling();
        super.onCleared();
    }

    public enum Phase {
        PICK_IMAGE,
        UPLOADING,
        PROCESSING,
        REVIEW,
        CONFIRMING,
        CANCELING,
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
