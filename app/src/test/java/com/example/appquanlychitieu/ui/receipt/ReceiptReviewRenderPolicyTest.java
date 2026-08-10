package com.example.appquanlychitieu.ui.receipt;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;

import org.junit.Test;

import java.math.BigDecimal;
import java.util.Collections;

public class ReceiptReviewRenderPolicyTest {
    @Test
    public void sameReceiptIdWithNewRetryPayloadMustRenderAgain() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();
        ReceiptDto original = receipt(
                "receipt-1", 1, 3L, "Old Store", "2026-08-10", "10000", "food");
        ReceiptDto retried = receipt(
                "receipt-1", 2, 6L, "New Store", "2026-08-11", "25000", "shopping");

        assertTrue(policy.shouldRender(original));
        assertTrue(policy.shouldRender(retried));
    }

    @Test
    public void userCategorySelectionSurvivesRetrySuggestion() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();
        policy.markCategorySelectedByUser();

        assertEquals("user-category", policy.resolveCategoryId(
                "user-category", "new-auto-category"));
    }

    @Test
    public void untouchedAutoSuggestionCanBeReplacedAfterRetry() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();

        assertEquals("new-auto-category", policy.resolveCategoryId(
                "old-auto-category", "new-auto-category"));
        assertNull(policy.resolveCategoryId("old-auto-category", null));
    }

    @Test
    public void duplicatePayloadFromSameGenerationIsNotRenderedTwice() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();
        ReceiptDto first = receipt(
                "receipt-1", 2, 6L, "Store", "2026-08-11", "25000", "food");
        ReceiptDto duplicate = receipt(
                "receipt-1", 2, 6L, "Store", "2026-08-11", "25000", "food");

        assertTrue(policy.shouldRender(first));
        assertFalse(policy.shouldRender(duplicate));
    }

    @Test
    public void retakeResetsCategoryAndOldRetryOperationRemainsStale() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();
        policy.shouldRender(receipt(
                "receipt-1", 2, 6L, "Store", "2026-08-11", "25000", "food"));
        policy.markCategorySelectedByUser();

        policy.reset();

        assertFalse(policy.isCategorySelectedByUser());
        assertNull(policy.resolveCategoryId(null, null));
        assertTrue(policy.shouldRender(receipt(
                "receipt-2", 1, 1L, "Other", "2026-08-12", "30000", null)));
        assertFalse(ReceiptCallbackPolicy.isCurrentOperation(7L, 8L));
    }

    @Test
    public void warningChangeWithinSameAttemptIsFreshContent() {
        ReceiptReviewRenderPolicy policy = new ReceiptReviewRenderPolicy();
        ReceiptDto first = receipt(
                "receipt-1", 2, 6L, "Store", "2026-08-11", "25000", "food");
        ReceiptDto changed = receipt(
                "receipt-1", 2, 6L, "Store", "2026-08-11", "25000", "food");
        changed.warnings = Collections.singletonList("LOW_OCR_CONFIDENCE");

        assertTrue(policy.shouldRender(first));
        assertTrue(policy.shouldRender(changed));
    }

    private static ReceiptDto receipt(
            String id,
            int processingAttempts,
            long version,
            String store,
            String date,
            String amount,
            String suggestion) {
        ReceiptDto receipt = new ReceiptDto();
        receipt.id = id;
        receipt.status = "REVIEW_REQUIRED";
        receipt.classification = "SUPPORTED";
        receipt.processingAttempts = processingAttempts;
        receipt.version = version;
        receipt.updatedAt = "2026-08-11T10:00:00Z";
        receipt.storeName = store;
        receipt.receiptDate = date;
        receipt.totalAmount = new BigDecimal(amount);
        receipt.warnings = Collections.emptyList();
        receipt.suggestedCategoryId = suggestion;
        return receipt;
    }
}
