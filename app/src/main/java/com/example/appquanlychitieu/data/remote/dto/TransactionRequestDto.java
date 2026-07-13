package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;

public class TransactionRequestDto {
    public final BigDecimal amount;
    public final String type;
    public final String transactionDate;
    public final String categoryId;
    public final String note;
    public final String storeName;

    public TransactionRequestDto(
            BigDecimal amount,
            String type,
            String transactionDate,
            String categoryId,
            String note,
            String storeName) {
        this.amount = amount;
        this.type = type;
        this.transactionDate = transactionDate;
        this.categoryId = categoryId;
        this.note = note;
        this.storeName = storeName;
    }
}
