package com.example.appquanlychitieu.data.remote.dto;


public class GoalRequestDto {
    public String name;
    public long targetAmount;
    public long currentAmount;

    public GoalRequestDto(String name, long targetAmount, long currentAmount) {
        this.name = name;
        this.targetAmount = targetAmount;
        this.currentAmount = currentAmount;
    }
}
