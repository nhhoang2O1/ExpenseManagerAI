package com.example.appquanlychitieu.ui.receipt;

import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

/**
 * Tracks the last receipt payload rendered by the review screen and whether
 * the category selection belongs to the user. Receipt id alone is not a
 * generation marker because an OCR retry deliberately reuses that id.
 */
final class ReceiptReviewRenderPolicy {
    private RenderSnapshot lastRendered;
    private boolean categorySelectedByUser;

    boolean shouldRender(ReceiptDto receipt) {
        RenderSnapshot next = RenderSnapshot.from(receipt);
        if (next.equals(lastRendered)) return false;
        lastRendered = next;
        return true;
    }

    String resolveCategoryId(String currentCategoryId, String suggestedCategoryId) {
        return categorySelectedByUser ? currentCategoryId : suggestedCategoryId;
    }

    void markCategorySelectedByUser() {
        categorySelectedByUser = true;
    }

    void restoreCategorySelection(boolean selectedByUser) {
        categorySelectedByUser = selectedByUser;
    }

    boolean isCategorySelectedByUser() {
        return categorySelectedByUser;
    }

    void reset() {
        lastRendered = null;
        categorySelectedByUser = false;
    }

    private static final class RenderSnapshot {
        private final String receiptId;
        private final int processingAttempts;
        private final long version;
        private final String updatedAt;
        private final String status;
        private final String classification;
        private final String storeName;
        private final String receiptDate;
        private final String totalAmount;
        private final List<String> warnings;
        private final String lastError;
        private final String suggestedCategoryId;

        private RenderSnapshot(ReceiptDto receipt) {
            receiptId = receipt.id;
            processingAttempts = receipt.processingAttempts;
            version = receipt.version;
            updatedAt = receipt.updatedAt;
            status = receipt.status;
            classification = receipt.classification;
            storeName = receipt.storeName;
            receiptDate = receipt.receiptDate;
            totalAmount = receipt.totalAmount == null
                    ? null : receipt.totalAmount.toPlainString();
            warnings = new ArrayList<>(receipt.safeWarnings());
            lastError = receipt.lastError;
            suggestedCategoryId = receipt.suggestedCategoryId;
        }

        static RenderSnapshot from(ReceiptDto receipt) {
            return new RenderSnapshot(receipt);
        }

        @Override
        public boolean equals(Object value) {
            if (this == value) return true;
            if (!(value instanceof RenderSnapshot)) return false;
            RenderSnapshot other = (RenderSnapshot) value;
            return processingAttempts == other.processingAttempts
                    && version == other.version
                    && Objects.equals(receiptId, other.receiptId)
                    && Objects.equals(updatedAt, other.updatedAt)
                    && Objects.equals(status, other.status)
                    && Objects.equals(classification, other.classification)
                    && Objects.equals(storeName, other.storeName)
                    && Objects.equals(receiptDate, other.receiptDate)
                    && Objects.equals(totalAmount, other.totalAmount)
                    && Objects.equals(warnings, other.warnings)
                    && Objects.equals(lastError, other.lastError)
                    && Objects.equals(suggestedCategoryId, other.suggestedCategoryId);
        }

        @Override
        public int hashCode() {
            return Objects.hash(
                    receiptId,
                    processingAttempts,
                    version,
                    updatedAt,
                    status,
                    classification,
                    storeName,
                    receiptDate,
                    totalAmount,
                    warnings,
                    lastError,
                    suggestedCategoryId);
        }
    }
}
