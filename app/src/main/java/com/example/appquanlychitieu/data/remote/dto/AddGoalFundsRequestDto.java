package com.example.appquanlychitieu.data.remote.dto;

import java.math.BigDecimal;

public class AddGoalFundsRequestDto {
    public BigDecimal amount;

    public AddGoalFundsRequestDto(BigDecimal amount) {
        this.amount = amount;
    }
}
