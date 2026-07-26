package com.example.appquanlychitieu.data.remote.dto;

public class EmailChangeRequestDto {
    public final String newEmail;
    public final String currentPassword;

    public EmailChangeRequestDto(String newEmail, String currentPassword) {
        this.newEmail = newEmail;
        this.currentPassword = currentPassword;
    }
}
