package com.example.appquanlychitieu.data.remote.dto;


public class BudgetRequestDto {
    public String categoryId;
    public long amount;
    public String monthYear;

    public BudgetRequestDto(String categoryId, long amount, String monthYear) {
        this.categoryId = categoryId;
        this.amount = amount;
        this.monthYear = monthYear;
    }
}
