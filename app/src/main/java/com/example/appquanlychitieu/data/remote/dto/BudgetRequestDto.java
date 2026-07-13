package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;

public class BudgetRequestDto {
    public String categoryId;
    public BigDecimal amount;
    public String monthYear;

    public BudgetRequestDto(String categoryId, BigDecimal amount, String monthYear) {
        this.categoryId = categoryId;
        this.amount = amount;
        this.monthYear = monthYear;
    }
}
