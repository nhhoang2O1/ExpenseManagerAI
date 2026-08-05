package com.example.appquanlychitieu.ui.receipt;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.SharedPreferences;

import com.example.appquanlychitieu.util.SessionManager;

import androidx.annotation.Nullable;

import java.util.Locale;

/**
 * Small durable store for the receipt workflow.  Receipt images themselves
 * are never copied into preferences; only the persisted content/file URI and
 * the server idempotency key are kept so a process death can safely resume
 * the same upload.
 */
public final class ReceiptDraftStore {
    private static final String PREFS = "receipt_workflow_drafts";
    private static final String KEY_RECEIPT_ID = "receipt_id";
    private static final String KEY_PHASE = "phase";
    private static final String KEY_IMAGE_URI = "image_uri";
    private static final String KEY_IDEMPOTENCY_KEY = "idempotency_key";
    private static final String KEY_STATUS = "status";

    private final SharedPreferences preferences;

    public ReceiptDraftStore(Context context) {
        String userKey = userKey(context);
        preferences = context.getApplicationContext()
                .getSharedPreferences(PREFS + "_" + userKey, Context.MODE_PRIVATE);
    }

    @SuppressLint("ApplySharedPref") // The draft must survive process death immediately after upload.
    public synchronized void save(
            @Nullable String receiptId,
            String phase,
            @Nullable String imageUri,
            @Nullable String idempotencyKey,
            @Nullable String status) {
        preferences.edit()
                .putString(KEY_RECEIPT_ID, receiptId)
                .putString(KEY_PHASE, phase)
                .putString(KEY_IMAGE_URI, imageUri)
                .putString(KEY_IDEMPOTENCY_KEY, idempotencyKey)
                .putString(KEY_STATUS, status)
                // A process death immediately after an upload must not lose
                // the key, so commit synchronously for this tiny record.
                .commit();
    }

    @Nullable
    public synchronized Draft load() {
        String phase = preferences.getString(KEY_PHASE, null);
        if (phase == null || phase.trim().isEmpty()) {
            return null;
        }
        return new Draft(
                emptyToNull(preferences.getString(KEY_RECEIPT_ID, null)),
                phase,
                emptyToNull(preferences.getString(KEY_IMAGE_URI, null)),
                emptyToNull(preferences.getString(KEY_IDEMPOTENCY_KEY, null)),
                emptyToNull(preferences.getString(KEY_STATUS, null)));
    }

    @SuppressLint("ApplySharedPref") // Clear must finish before a new user can open a draft store.
    public synchronized void clear() {
        preferences.edit().clear().commit();
    }

    private static String userKey(Context context) {
        SessionManager session = new SessionManager(context.getApplicationContext());
        String remoteId = session.getRemoteUserId();
        if (remoteId != null && !remoteId.trim().isEmpty()) {
            return safeKey(remoteId);
        }
        String email = session.getUserEmail();
        if (email != null && !email.trim().isEmpty()) {
            return safeKey(email.toLowerCase(Locale.ROOT));
        }
        return "anonymous";
    }

    private static String safeKey(String value) {
        return value.replaceAll("[^A-Za-z0-9_.-]", "_");
    }

    @Nullable
    private static String emptyToNull(@Nullable String value) {
        return value == null || value.trim().isEmpty() ? null : value;
    }

    public static final class Draft {
        @Nullable public final String receiptId;
        public final String phase;
        @Nullable public final String imageUri;
        @Nullable public final String idempotencyKey;
        @Nullable public final String status;

        private Draft(
                @Nullable String receiptId,
                String phase,
                @Nullable String imageUri,
                @Nullable String idempotencyKey,
                @Nullable String status) {
            this.receiptId = receiptId;
            this.phase = phase;
            this.imageUri = imageUri;
            this.idempotencyKey = idempotencyKey;
            this.status = status;
        }
    }
}
