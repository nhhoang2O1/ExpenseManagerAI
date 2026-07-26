package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;
import java.util.Collections;
import java.util.List;

public class ReceiptDto {
    public String id;
    public String status;
    public String classification;
    public String createdAt;
    public String storeName;
    public String receiptDate;
    public BigDecimal totalAmount;
    public BigDecimal vatAmount;
    public Double overallConfidence;
    public List<String> warnings;
    public String rawText;
    public String modelVersion;
    public int processingAttempts;
    public String nextRetryAt;
    public String lastError;
    public String updatedAt;
    public long version = 1L;
    public String suggestedCategoryId;
    public String suggestedCategoryName;
    public Double categoryConfidence;
    public String categoryReason;

    public List<String> safeWarnings() {
        return warnings == null ? Collections.emptyList() : warnings;
    }
}
