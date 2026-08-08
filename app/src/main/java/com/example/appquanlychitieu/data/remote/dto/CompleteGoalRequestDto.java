package com.example.appquanlychitieu.data.remote.dto;

public final class CompleteGoalRequestDto {
    public final String categoryId;
    public final String transactionDate;
    public final String note;

    public CompleteGoalRequestDto(String categoryId, String transactionDate, String note) {
        this.categoryId = categoryId;
        this.transactionDate = transactionDate;
        this.note = note;
    }
}
