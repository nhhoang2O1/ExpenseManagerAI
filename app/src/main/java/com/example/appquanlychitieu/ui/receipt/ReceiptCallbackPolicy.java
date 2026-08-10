package com.example.appquanlychitieu.ui.receipt;

/** Pure transition rules for callbacks arriving after the active OCR operation changed. */
final class ReceiptCallbackPolicy {
    private ReceiptCallbackPolicy() { }

    static boolean isCurrentOperation(long callbackToken, long currentToken) {
        return callbackToken == currentToken;
    }

    static boolean shouldApplyDownloadedImage(
            long callbackToken,
            long currentToken,
            String callbackReceiptId,
            String loadedReceiptId) {
        return isCurrentOperation(callbackToken, currentToken)
                && callbackReceiptId != null
                && callbackReceiptId.equals(loadedReceiptId);
    }

    static boolean shouldDeleteLateUpload(
            long callbackToken,
            long currentToken,
            long canceledUploadToken) {
        return callbackToken != currentToken && callbackToken == canceledUploadToken;
    }
}
