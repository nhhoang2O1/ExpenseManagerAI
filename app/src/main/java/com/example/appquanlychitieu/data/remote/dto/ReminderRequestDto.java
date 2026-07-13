package com.example.appquanlychitieu.data.remote.dto;

public class ReminderRequestDto {
    public String content;
    public int dayOfMonth;
    public int hour;
    public int minute;
    public boolean isActive;

    public ReminderRequestDto(String content, int dayOfMonth, int hour, int minute, boolean isActive) {
        this.content = content;
        this.dayOfMonth = dayOfMonth;
        this.hour = hour;
        this.minute = minute;
        this.isActive = isActive;
    }
}
