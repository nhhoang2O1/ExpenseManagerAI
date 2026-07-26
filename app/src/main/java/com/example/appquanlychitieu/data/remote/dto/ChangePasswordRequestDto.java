package com.example.appquanlychitieu.data.remote.dto;

public class ChangePasswordRequestDto {
    public final String currentPassword;
    public final String newPassword;

    public ChangePasswordRequestDto(String currentPassword, String newPassword) {
        this.currentPassword = currentPassword;
        this.newPassword = newPassword;
    }
}
