package com.example.appquanlychitieu.data.remote;

public class ApiError {
    private final int statusCode;
    private final String message;
    private final boolean networkError;

    public ApiError(int statusCode, String message, boolean networkError) {
        this.statusCode = statusCode;
        this.message = message;
        this.networkError = networkError;
    }

    public int getStatusCode() {
        return statusCode;
    }

    public String getMessage() {
        return message;
    }

    public boolean isNetworkError() {
        return networkError;
    }
}
