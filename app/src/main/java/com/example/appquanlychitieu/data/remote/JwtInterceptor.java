package com.example.appquanlychitieu.data.remote;

import java.io.IOException;

import okhttp3.Interceptor;
import okhttp3.Request;
import okhttp3.Response;

public class JwtInterceptor implements Interceptor {
    private final TokenStore tokenStore;

    public JwtInterceptor(TokenStore tokenStore) {
        this.tokenStore = tokenStore;
    }

    @Override
    public Response intercept(Chain chain) throws IOException {
        Request request = chain.request();
        String token = tokenStore.getToken();
        if (token != null && !token.trim().isEmpty()) {
            request = request.newBuilder()
                    .header("Authorization", "Bearer " + token)
                    .build();
        }
        // The Authenticator owns 401 recovery and only clears the session
        // after a refresh attempt has failed.
        return chain.proceed(request);
    }
}
