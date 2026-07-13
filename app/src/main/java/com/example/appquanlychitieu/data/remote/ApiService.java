package com.example.appquanlychitieu.data.remote;

import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.remote.dto.AddGoalFundsRequestDto;
import com.example.appquanlychitieu.data.remote.dto.BudgetDto;
import com.example.appquanlychitieu.data.remote.dto.BudgetRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ConfirmReceiptRequestDto;
import com.example.appquanlychitieu.data.remote.dto.GoalDto;
import com.example.appquanlychitieu.data.remote.dto.GoalRequestDto;
import com.example.appquanlychitieu.data.remote.dto.LoginRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.example.appquanlychitieu.data.remote.dto.RegisterRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReminderDto;
import com.example.appquanlychitieu.data.remote.dto.ReminderRequestDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionRequestDto;
import com.google.gson.JsonElement;

import okhttp3.MultipartBody;
import okhttp3.ResponseBody;
import retrofit2.Call;
import retrofit2.http.Body;
import retrofit2.http.DELETE;
import retrofit2.http.GET;
import retrofit2.http.Multipart;
import retrofit2.http.POST;
import retrofit2.http.PUT;
import retrofit2.http.Part;
import retrofit2.http.Path;
import retrofit2.http.Query;

public interface ApiService {
    @POST("api/auth/login")
    Call<AuthResponseDto> login(@Body LoginRequestDto request);

    @POST("api/auth/register")
    Call<AuthResponseDto> register(@Body RegisterRequestDto request);

    @GET("api/transactions")
    Call<JsonElement> getTransactions(@Query("pageSize") int pageSize);

    @POST("api/transactions")
    Call<TransactionDto> createTransaction(@Body TransactionRequestDto request);

    @PUT("api/transactions/{id}")
    Call<TransactionDto> updateTransaction(
            @Path("id") String transactionId,
            @Body TransactionRequestDto request);

    @DELETE("api/transactions/{id}")
    Call<Void> deleteTransaction(@Path("id") String transactionId);

    @GET("api/categories")
    Call<JsonElement> getCategories();

    @GET("api/budgets")
    Call<JsonElement> getBudgets(@Query("monthYear") String monthYear);

    @POST("api/budgets")
    Call<BudgetDto> createBudget(@Body BudgetRequestDto request);

    @PUT("api/budgets/{id}")
    Call<BudgetDto> updateBudget(
            @Path("id") String budgetId,
            @Body BudgetRequestDto request);

    @DELETE("api/budgets/{id}")
    Call<Void> deleteBudget(@Path("id") String budgetId);

    @GET("api/goals")
    Call<JsonElement> getGoals();

    @POST("api/goals")
    Call<GoalDto> createGoal(@Body GoalRequestDto request);

    @PUT("api/goals/{id}")
    Call<GoalDto> updateGoal(
            @Path("id") String goalId,
            @Body GoalRequestDto request);

    @DELETE("api/goals/{id}")
    Call<Void> deleteGoal(@Path("id") String goalId);

    @POST("api/goals/{id}/funds")
    Call<GoalDto> addGoalFunds(
            @Path("id") String goalId,
            @Body AddGoalFundsRequestDto request);

    @GET("api/goals/{id}/history")
    Call<JsonElement> getGoalHistory(@Path("id") String goalId);

    @GET("api/reminders")
    Call<JsonElement> getReminders();

    @POST("api/reminders")
    Call<ReminderDto> createReminder(@Body ReminderRequestDto request);

    @PUT("api/reminders/{id}")
    Call<ReminderDto> updateReminder(
            @Path("id") String reminderId,
            @Body ReminderRequestDto request);

    @DELETE("api/reminders/{id}")
    Call<Void> deleteReminder(@Path("id") String reminderId);

    @GET("api/statistics/by-category")
    Call<JsonElement> getStatisticsByCategory(
            @Query("from") String from,
            @Query("to") String to);

    @GET("api/statistics/monthly")
    Call<JsonElement> getMonthlyStatistics(@Query("year") int year);

    @GET("api/reports/monthly.xlsx")
    Call<ResponseBody> downloadMonthlyReport(
            @Query("year") int year,
            @Query("month") int month);

    @Multipart
    @POST("api/receipts")
    Call<ReceiptDto> uploadReceipt(@Part MultipartBody.Part image);

    @POST("api/receipts/{id}/process")
    Call<ReceiptDto> processReceipt(@Path("id") String receiptId);

    @GET("api/receipts/{id}")
    Call<ReceiptDto> getReceipt(@Path("id") String receiptId);

    @POST("api/receipts/{id}/retry")
    Call<ReceiptDto> retryReceipt(@Path("id") String receiptId);

    @POST("api/receipts/{id}/confirm")
    Call<TransactionDto> confirmReceipt(
            @Path("id") String receiptId,
            @Body ConfirmReceiptRequestDto request);
}
