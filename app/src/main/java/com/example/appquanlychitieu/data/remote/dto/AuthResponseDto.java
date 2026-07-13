package com.example.appquanlychitieu.data.remote.dto;

import com.google.gson.annotations.SerializedName;

public class AuthResponseDto {
    public String token;
    public String accessToken;
    public String id;
    public String name;
    public String email;
    public UserDto user;

    public String resolvedToken() {
        return token != null ? token : accessToken;
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
