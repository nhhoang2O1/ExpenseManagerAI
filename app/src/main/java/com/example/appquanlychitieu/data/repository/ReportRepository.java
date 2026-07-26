package com.example.appquanlychitieu.data.repository;

import android.content.Context;

import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiResponseHelper;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.RemoteCallback;

import okhttp3.ResponseBody;
import retrofit2.Call;
import retrofit2.Callback;
import retrofit2.Response;

public final class ReportRepository {
    private final ApiService api;

    public ReportRepository(Context context) {
        api = ApiClient.getService(context.getApplicationContext());
    }

    public void download(
            int year,
            int month,
            String format,
            RemoteCallback<ResponseBody> callback) {
        Call<ResponseBody> call;
        if ("csv".equalsIgnoreCase(format)) call = api.downloadMonthlyCsv(year, month);
        else if ("pdf".equalsIgnoreCase(format)) call = api.downloadMonthlyPdf(year, month);
        else call = api.downloadMonthlyReport(year, month);
        call.enqueue(new Callback<ResponseBody>() {
            @Override
            public void onResponse(Call<ResponseBody> call, Response<ResponseBody> response) {
                if (response.isSuccessful() && response.body() != null) callback.onSuccess(response.body());
                else callback.onError(ApiResponseHelper.fromResponse(response));
            }

            @Override
            public void onFailure(Call<ResponseBody> call, Throwable throwable) {
                callback.onError(ApiResponseHelper.fromFailure(throwable));
            }
        });
    }
}
