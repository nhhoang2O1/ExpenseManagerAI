package com.example.appquanlychitieu.ui.receipt;

/** Pure transition rules for callbacks arriving after the active OCR operation changed. */
final class ReceiptCallbackPolicy {
    private ReceiptCallbackPolicy() { }

    static boolean shouldDeleteLateUpload(
            long callbackToken,
            long currentToken,
            long canceledUploadToken) {
        return callbackToken != currentToken && callbackToken == canceledUploadToken;
    }
}
