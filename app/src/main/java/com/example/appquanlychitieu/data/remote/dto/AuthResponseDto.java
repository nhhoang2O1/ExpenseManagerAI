package com.example.appquanlychitieu.data.remote.dto;

import com.google.gson.annotations.SerializedName;

public class AuthResponseDto {
    public String token;
    public String accessToken;
    public String refreshToken;
    public int expiresIn;
    public String id;
    public String name;
    public String email;
    public UserDto user;

    public String resolvedToken() {
        if (token != null && !token.trim().isEmpty()) {
            return token;
        }
        return accessToken;
    }

    public String resolvedRefreshToken() {
        return refreshToken;
    }

    public String resolvedId() {
        return user != null && user.id != null ? user.id : id;
    }

    public String resolvedName() {
        return user != null && user.name != null ? user.name : name;
    }

    public String resolvedEmail() {
        return user != null && user.email != null ? user.email : email;
    }

    public static class UserDto {
        @SerializedName("id")
        public String id;
        public String name;
        public String email;
    }
}
