package com.example.appquanlychitieu.data.remote.dto;

public class ResetPasswordRequestDto {
    public final String email;
    public final String code;
    public final String newPassword;

    public ResetPasswordRequestDto(String email, String code, String newPassword) {
        this.email = email;
        this.code = code;
        this.newPassword = newPassword;
    }
}
