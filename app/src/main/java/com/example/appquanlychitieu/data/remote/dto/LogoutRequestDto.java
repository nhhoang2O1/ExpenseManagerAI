package com.example.appquanlychitieu.data.remote.dto;

public class LogoutRequestDto {
    public final String refreshToken;

    public LogoutRequestDto(String refreshToken) {
        this.refreshToken = refreshToken;
    }
}
