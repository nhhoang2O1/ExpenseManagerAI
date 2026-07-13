package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;

public class ConfirmReceiptRequestDto {
    public final String storeName;
    public final String receiptDate;
    public final BigDecimal totalAmount;
    public final BigDecimal vatAmount;
    public final String categoryId;
    public final String note;

    public ConfirmReceiptRequestDto(
            String storeName,
            String receiptDate,
            BigDecimal totalAmount,
            BigDecimal vatAmount,
            String categoryId,
            String note) {
        this.storeName = storeName;
        this.receiptDate = receiptDate;
        this.totalAmount = totalAmount;
        this.vatAmount = vatAmount;
        this.categoryId = categoryId;
        this.note = note;
    }
}
