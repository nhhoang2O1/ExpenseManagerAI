package com.example.appquanlychitieu.data.remote;

import android.content.Context;

import com.example.appquanlychitieu.BuildConfig;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;

import java.util.concurrent.TimeUnit;

import okhttp3.OkHttpClient;
import retrofit2.Retrofit;
import retrofit2.converter.gson.GsonConverterFactory;

public final class ApiClient {
    private static volatile ApiService apiService;

    private ApiClient() {}

    public static ApiService getService(Context context) {
        if (apiService == null) {
            synchronized (ApiClient.class) {
                if (apiService == null) {
                    TokenStore tokenStore = new TokenStore(context.getApplicationContext());
                    OkHttpClient client = new OkHttpClient.Builder()
                            .addInterceptor(new JwtInterceptor(tokenStore))
                            // Fail fast when the API host is unavailable. OCR processing still
                            // has a longer read/write window after a connection is established.
                            .connectTimeout(5, TimeUnit.SECONDS)
                            .readTimeout(45, TimeUnit.SECONDS)
                            .writeTimeout(60, TimeUnit.SECONDS)
                            .build();
                    Gson gson = new GsonBuilder().serializeNulls().create();
                    apiService = new Retrofit.Builder()
                            .baseUrl(BuildConfig.BACKEND_BASE_URL)
                            .client(client)
                            .addConverterFactory(GsonConverterFactory.create(gson))
                            .build()
                            .create(ApiService.class);
                }
            }
        }
        return apiService;
    }
}
