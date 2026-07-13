package com.example.appquanlychitieu.data.remote;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.io.IOException;

import retrofit2.Response;

public final class ApiResponseHelper {
    private ApiResponseHelper() {}

    public static ApiError fromResponse(Response<?> response) {
        String message = "Yeu cau khong thanh cong";
        try {
            if (response.errorBody() != null) {
                String raw = response.errorBody().string();
                JsonObject body = JsonParser.parseString(raw).getAsJsonObject();
                if (body.has("message")) {
                    message = body.get("message").getAsString();
                } else if (body.has("title")) {
                    message = body.get("title").getAsString();
                }
            }
        } catch (IOException | RuntimeException ignored) {
            message = "May chu tra ve loi " + response.code();
        }
        return new ApiError(response.code(), message, false);
    }

    public static ApiError fromFailure(Throwable throwable) {
        String message = throwable.getMessage();
        if (message == null || message.trim().isEmpty()) {
            message = "Khong the ket noi den may chu";
        }
        return new ApiError(0, message, true);
    }
}
