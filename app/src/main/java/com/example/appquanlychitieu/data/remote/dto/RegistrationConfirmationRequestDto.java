package com.example.appquanlychitieu.data.remote.dto;

public class RegistrationConfirmationRequestDto {
    public final String email;
    public final String code;

    public RegistrationConfirmationRequestDto(String email, String code) {
        this.email = email;
        this.code = code;
    }
}
