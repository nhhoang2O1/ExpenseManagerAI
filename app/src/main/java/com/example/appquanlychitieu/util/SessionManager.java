package com.example.appquanlychitieu.util;

import android.content.Context;
import android.content.SharedPreferences;

import com.example.appquanlychitieu.data.remote.TokenStore;

public class SessionManager {
    private static final String PREF_NAME = "expense_manager_session";
    private static final String KEY_IS_LOGGED_IN = "is_logged_in";
    private static final String KEY_USER_ID = "user_id";
    private static final String KEY_USER_NAME = "user_name";
    private static final String KEY_USER_EMAIL = "user_email";
    private static final String KEY_REMEMBER_ME = "remember_me";
    private static final String KEY_REMOTE_USER_ID = "remote_user_id";

    private final SharedPreferences prefs;
    private final SharedPreferences.Editor editor;
    private final TokenStore tokenStore;

    public SessionManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
        editor = prefs.edit();
        tokenStore = new TokenStore(context.getApplicationContext());
    }

    public void createLoginSession(long userId, String name, String email, boolean rememberMe) {
        editor.putBoolean(KEY_IS_LOGGED_IN, true);
        editor.putLong(KEY_USER_ID, userId);
        editor.putString(KEY_USER_NAME, name);
        editor.putString(KEY_USER_EMAIL, email);
        editor.putBoolean(KEY_REMEMBER_ME, rememberMe);
        editor.commit(); 
    }

    public void createRemoteLoginSession(
            long cacheUserId,
            String remoteUserId,
            String name,
            String email,
            boolean rememberMe,
            String token) {
        createRemoteLoginSession(
                cacheUserId, remoteUserId, name, email, rememberMe,
                token, null, 0);
    }

    public void createRemoteLoginSession(
            long cacheUserId,
            String remoteUserId,
            String name,
            String email,
            boolean rememberMe,
            String accessToken,
            String refreshToken,
            int expiresIn) {
        createLoginSession(cacheUserId, name, email, rememberMe);
        editor.putString(KEY_REMOTE_USER_ID, remoteUserId == null ? "" : remoteUserId).apply();
        if (refreshToken != null && !refreshToken.trim().isEmpty()) {
            tokenStore.savePair(accessToken, refreshToken, expiresIn);
        } else {
            tokenStore.saveToken(accessToken);
        }
    }

    public boolean isLoggedIn() {
        return prefs.getBoolean(KEY_IS_LOGGED_IN, false)
                && prefs.getLong(KEY_USER_ID, -1) > 0;
    }

    public boolean isRememberMe() {
        return prefs.getBoolean(KEY_REMEMBER_ME, false);
    }

    public long getUserId() {
        return prefs.getLong(KEY_USER_ID, -1);
    }

    public String getUserName() {
        return prefs.getString(KEY_USER_NAME, "");
    }

    public String getUserEmail() {
        return prefs.getString(KEY_USER_EMAIL, "");
    }

    public String getRemoteUserId() {
        return prefs.getString(KEY_REMOTE_USER_ID, "");
    }

    public void updateIdentity(String name, String email) {
        editor.putString(KEY_USER_NAME, name == null ? "" : name);
        editor.putString(KEY_USER_EMAIL, email == null ? "" : email);
        editor.apply();
    }

    public String getAuthToken() {
        return tokenStore.getToken();
    }

    public String getRefreshToken() {
        return tokenStore.getRefreshToken();
    }

    public boolean hasAuthToken() {
        return tokenStore.hasToken();
    }

    public void clearAuthToken() {
        tokenStore.clear();
        editor.remove(KEY_REMOTE_USER_ID).apply();
    }

    public void logout() {
        tokenStore.clear();
        editor.clear();
        editor.apply();
    }
}
