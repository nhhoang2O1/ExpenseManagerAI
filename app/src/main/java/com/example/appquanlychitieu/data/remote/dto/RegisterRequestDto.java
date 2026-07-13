package com.example.appquanlychitieu.data.remote.dto;

public class RegisterRequestDto {
    public final String name;
    public final String email;
    public final String password;

    public RegisterRequestDto(String name, String email, String password) {
        this.name = name;
        this.email = email;
        this.password = password;
    }
}
