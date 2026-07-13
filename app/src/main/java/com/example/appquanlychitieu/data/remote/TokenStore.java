package com.example.appquanlychitieu.data.remote;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.security.crypto.EncryptedSharedPreferences;
import androidx.security.crypto.MasterKey;

import java.io.IOException;
import java.security.GeneralSecurityException;

public class TokenStore {
    private static final String PREF_NAME = "secure_backend_session";
    private static final String KEY_TOKEN = "jwt_access_token";

    private final SharedPreferences preferences;

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
        preferences.edit().putString(KEY_TOKEN, token).apply();
    }

    public String getToken() {
        return preferences.getString(KEY_TOKEN, "");
    }

    public boolean hasToken() {
        String token = getToken();
        return token != null && !token.trim().isEmpty();
    }

    public void clear() {
        preferences.edit().remove(KEY_TOKEN).apply();
    }
}
