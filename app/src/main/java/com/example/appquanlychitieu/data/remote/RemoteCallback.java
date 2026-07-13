package com.example.appquanlychitieu.data.remote;

public interface RemoteCallback<T> {
    void onSuccess(T value);
    void onError(ApiError error);
}
