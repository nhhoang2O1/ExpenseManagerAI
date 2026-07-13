package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;

public class GoalRequestDto {
    public String name;
    public BigDecimal targetAmount;
    public BigDecimal currentAmount;

    public GoalRequestDto(String name, BigDecimal targetAmount, BigDecimal currentAmount) {
        this.name = name;
        this.targetAmount = targetAmount;
        this.currentAmount = currentAmount;
    }
}
