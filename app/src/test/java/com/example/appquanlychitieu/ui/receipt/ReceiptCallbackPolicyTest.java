package com.example.appquanlychitieu.ui.receipt;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class ReceiptCallbackPolicyTest {
    @Test
    public void onlyCurrentOperationMayUpdateUi() {
        assertTrue(ReceiptCallbackPolicy.isCurrentOperation(5L, 5L));
        assertFalse(ReceiptCallbackPolicy.isCurrentOperation(4L, 5L));
    }

    @Test
    public void oldImageCallbackCannotReplaceNewReceiptPreview() {
        assertFalse(ReceiptCallbackPolicy.shouldApplyDownloadedImage(
                4L, 5L, "receipt-a", "receipt-b"));
        assertFalse(ReceiptCallbackPolicy.shouldApplyDownloadedImage(
                5L, 5L, "receipt-a", "receipt-b"));
        assertTrue(ReceiptCallbackPolicy.shouldApplyDownloadedImage(
                5L, 5L, "receipt-b", "receipt-b"));
    }

    @Test
    public void lifecycleStaleUploadIsKeptForDraftRecovery() {
        assertFalse(ReceiptCallbackPolicy.shouldDeleteLateUpload(4L, 5L, -1L));
    }

    @Test
    public void explicitlyCanceledLateUploadIsDeleted() {
        assertTrue(ReceiptCallbackPolicy.shouldDeleteLateUpload(4L, 5L, 4L));
    }

    @Test
    public void activeUploadIsNeverTreatedAsLateCleanup() {
        assertFalse(ReceiptCallbackPolicy.shouldDeleteLateUpload(4L, 4L, 4L));
    }
}
