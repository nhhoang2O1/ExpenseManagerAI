package com.example.appquanlychitieu.data.remote;

import android.content.Context;

import androidx.annotation.Nullable;

import com.example.appquanlychitieu.BuildConfig;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.remote.dto.RefreshTokenRequestDto;
import com.google.gson.Gson;

import java.io.IOException;

import okhttp3.Authenticator;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;
import okhttp3.Route;

/**
 * Synchronous OkHttp authenticator with a process-wide single-flight lock.
 * A request is retried at most once and the refresh call uses a bare client so
 * it can never recursively invoke this authenticator.
 */
public final class RefreshTokenAuthenticator implements Authenticator {
    private static final Object REFRESH_LOCK = new Object();
    private static final MediaType JSON = MediaType.get("application/json; charset=utf-8");

    private final Context appContext;
    private final TokenAccess tokenStore;
    private final OkHttpClient refreshClient;
    private final String refreshUrl;
    private final Gson gson = new Gson();

    public RefreshTokenAuthenticator(Context context, TokenStore tokenStore) {
        this(
                context.getApplicationContext(),
                new TokenAccess() {
                    @Override public String getAccessToken() { return tokenStore.getAccessToken(); }
                    @Override public String getRefreshToken() { return tokenStore.getRefreshToken(); }
                    @Override public void savePair(String access, String refresh, int expiresIn) {
                        tokenStore.savePair(access, refresh, expiresIn);
                    }
                    @Override public void clear() { tokenStore.clear(); }
                },
                new OkHttpClient.Builder().build(),
                BuildConfig.BACKEND_BASE_URL + "api/auth/refresh");
    }

    /** Test seam for exercising concurrency against a local MockWebServer. */
    RefreshTokenAuthenticator(
            Context context,
            TokenAccess tokenStore,
            OkHttpClient refreshClient,
            String refreshUrl) {
        this.appContext = context;
        this.tokenStore = tokenStore;
        this.refreshClient = refreshClient;
        this.refreshUrl = refreshUrl;
    }

    @Nullable
    @Override
    public Request authenticate(@Nullable Route route, Response response) throws IOException {
        if (responseCount(response) > 1 || isAuthEndpoint(response.request())) {
            expire();
            return null;
        }

        String refreshToken = tokenStore.getRefreshToken();
        if (empty(refreshToken)) {
            expire();
            return null;
        }

        synchronized (REFRESH_LOCK) {
            String requestAccess = bearerToken(response.request());
            String currentAccess = tokenStore.getAccessToken();
            if (!empty(currentAccess) && !currentAccess.equals(requestAccess)) {
                return response.request().newBuilder()
                        .header("Authorization", "Bearer " + currentAccess)
                        .build();
            }

            AuthResponseDto refreshed = refresh(refreshToken);
            if (refreshed == null || empty(refreshed.resolvedToken())
                    || empty(refreshed.resolvedRefreshToken())) {
                expire();
                return null;
            }

            tokenStore.savePair(
                    refreshed.resolvedToken(),
                    refreshed.resolvedRefreshToken(),
                    refreshed.expiresIn);
            return response.request().newBuilder()
                    .header("Authorization", "Bearer " + refreshed.resolvedToken())
                    .build();
        }
    }

    @Nullable
    private AuthResponseDto refresh(String refreshToken) throws IOException {
        Request request = new Request.Builder()
                .url(refreshUrl)
                .post(RequestBody.create(
                        gson.toJson(new RefreshTokenRequestDto(refreshToken)),
                        JSON))
                .build();
        try (Response response = refreshClient.newCall(request).execute()) {
            if (!response.isSuccessful() || response.body() == null) return null;
            return gson.fromJson(response.body().charStream(), AuthResponseDto.class);
        }
    }

    private void expire() {
        tokenStore.clear();
        SessionEvents.notifyExpired(appContext);
    }

    private static boolean isAuthEndpoint(Request request) {
        String path = request.url().encodedPath();
        return path.endsWith("/api/auth/login")
                || path.endsWith("/api/auth/register")
                || path.endsWith("/api/auth/refresh");
    }

    private static int responseCount(Response response) {
        int count = 1;
        while ((response = response.priorResponse()) != null) count++;
        return count;
    }

    private static String bearerToken(Request request) {
        String header = request.header("Authorization");
        return header != null && header.startsWith("Bearer ") ? header.substring(7) : "";
    }

    private static boolean empty(String value) {
        return value == null || value.trim().isEmpty();
    }

    interface TokenAccess {
        String getAccessToken();
        String getRefreshToken();
        void savePair(String accessToken, String refreshToken, int expiresIn);
        void clear();
    }
}
