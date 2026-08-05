package com.example.appquanlychitieu.data.remote;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.SharedPreferences;

import androidx.security.crypto.EncryptedSharedPreferences;
import androidx.security.crypto.MasterKey;

import java.io.IOException;
import java.security.GeneralSecurityException;

@SuppressLint("ApplySharedPref") // Authentication state must be durable before requests continue.
public class TokenStore {
    private static final String PREF_NAME = "secure_backend_session";
    private static final String KEY_ACCESS_TOKEN = "jwt_access_token";
    private static final String KEY_REFRESH_TOKEN = "refresh_token";
    private static final String KEY_EXPIRES_IN = "access_token_expires_in";

    private final SharedPreferences preferences;
    private final Object lock = new Object();

    public TokenStore(Context context) {
        try {
            MasterKey masterKey = new MasterKey.Builder(context)
                    .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
                    .build();
            preferences = EncryptedSharedPreferences.create(
                    context,
                    PREF_NAME,
                    masterKey,
                    EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                    EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM);
        } catch (GeneralSecurityException | IOException exception) {
            throw new IllegalStateException("Cannot initialize encrypted token storage", exception);
        }
    }

    public void saveToken(String token) {
        saveAccessToken(token);
    }

    public String getToken() {
        return getAccessToken();
    }

    public boolean hasToken() {
        String token = getAccessToken();
        return token != null && !token.trim().isEmpty();
    }

    public void saveAccessToken(String accessToken) {
        synchronized (lock) {
            preferences.edit().putString(KEY_ACCESS_TOKEN, accessToken).commit();
        }
    }

    public String getAccessToken() {
        synchronized (lock) {
            return preferences.getString(KEY_ACCESS_TOKEN, "");
        }
    }

    public void saveRefreshToken(String refreshToken) {
        synchronized (lock) {
            preferences.edit().putString(KEY_REFRESH_TOKEN, refreshToken).commit();
        }
    }

    public String getRefreshToken() {
        synchronized (lock) {
            return preferences.getString(KEY_REFRESH_TOKEN, "");
        }
    }

    public boolean hasRefreshToken() {
        String token = getRefreshToken();
        return token != null && !token.trim().isEmpty();
    }

    public boolean hasPair() {
        return hasToken() && hasRefreshToken();
    }

    /** Saves both credentials in one synchronous encrypted-preferences edit. */
    public void savePair(String accessToken, String refreshToken, int expiresIn) {
        if (accessToken == null || accessToken.trim().isEmpty()
                || refreshToken == null || refreshToken.trim().isEmpty()) {
            throw new IllegalArgumentException("Both access and refresh tokens are required");
        }
        synchronized (lock) {
            preferences.edit()
                    .putString(KEY_ACCESS_TOKEN, accessToken)
                    .putString(KEY_REFRESH_TOKEN, refreshToken)
                    .putInt(KEY_EXPIRES_IN, expiresIn)
                    .commit();
        }
    }

    public int getExpiresIn() {
        synchronized (lock) {
            return preferences.getInt(KEY_EXPIRES_IN, 0);
        }
    }

    public void clear() {
        synchronized (lock) {
            preferences.edit()
                    .remove(KEY_ACCESS_TOKEN)
                    .remove(KEY_REFRESH_TOKEN)
                    .remove(KEY_EXPIRES_IN)
                    .commit();
        }
    }
}
