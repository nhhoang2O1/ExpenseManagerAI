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
import com.example.appquanlychitieu.data.remote.dto.RegistrationConfirmationRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReminderDto;
import com.example.appquanlychitieu.data.remote.dto.ReminderRequestDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionRequestDto;
import com.example.appquanlychitieu.data.remote.dto.RefreshTokenRequestDto;
import com.example.appquanlychitieu.data.remote.dto.LogoutRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ForgotPasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ResetPasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ProfileDto;
import com.example.appquanlychitieu.data.remote.dto.UpdateProfileRequestDto;
import com.example.appquanlychitieu.data.remote.dto.UpdateFinancialCycleRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ChangePasswordRequestDto;
import com.example.appquanlychitieu.data.remote.dto.EmailChangeRequestDto;
import com.example.appquanlychitieu.data.remote.dto.EmailChangeConfirmRequestDto;
import com.example.appquanlychitieu.data.remote.dto.DeleteAccountRequestDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.google.gson.JsonElement;

import okhttp3.MultipartBody;
import okhttp3.ResponseBody;
import retrofit2.Call;
import retrofit2.http.Body;
import retrofit2.http.DELETE;
import retrofit2.http.GET;
import retrofit2.http.Header;
import retrofit2.http.HTTP;
import retrofit2.http.Multipart;
import retrofit2.http.POST;
import retrofit2.http.PUT;
import retrofit2.http.Part;
import retrofit2.http.Path;
import retrofit2.http.Query;
import retrofit2.http.Streaming;

public interface ApiService {
    @POST("api/auth/login")
    Call<AuthResponseDto> login(@Body LoginRequestDto request);

    @POST("api/auth/register")
    Call<Void> register(@Body RegisterRequestDto request);

    @POST("api/auth/confirm-registration")
    Call<AuthResponseDto> confirmRegistration(@Body RegistrationConfirmationRequestDto request);

    @POST("api/auth/refresh")
    Call<AuthResponseDto> refresh(@Body RefreshTokenRequestDto request);

    @POST("api/auth/logout")
    Call<Void> logout(@Body LogoutRequestDto request);

    @POST("api/auth/logout-all")
    Call<Void> logoutAll();

    @POST("api/auth/forgot-password")
    Call<Void> forgotPassword(@Body ForgotPasswordRequestDto request);

    @POST("api/auth/reset-password")
    Call<Void> resetPassword(@Body ResetPasswordRequestDto request);

    @GET("api/account/profile")
    Call<ProfileDto> getProfile();

    @PUT("api/account/profile")
    Call<ProfileDto> updateProfile(@Body UpdateProfileRequestDto request);

    @PUT("api/account/financial-cycle")
    Call<ProfileDto> updateFinancialCycle(@Body UpdateFinancialCycleRequestDto request);

    @POST("api/account/change-password")
    Call<Void> changePassword(@Body ChangePasswordRequestDto request);

    @POST("api/account/email-change/request")
    Call<Void> requestEmailChange(@Body EmailChangeRequestDto request);

    @POST("api/account/email-change/confirm")
    Call<ProfileDto> confirmEmailChange(@Body EmailChangeConfirmRequestDto request);

    @HTTP(method = "DELETE", path = "api/account", hasBody = true)
    Call<Void> deleteAccount(@Body DeleteAccountRequestDto request);

    @GET("api/transactions")
    Call<JsonElement> getTransactions(
            @Query("page") int page,
            @Query("pageSize") int pageSize);

    @POST("api/transactions")
    Call<TransactionDto> createTransaction(
            @Header("Idempotency-Key") String idempotencyKey,
            @Body TransactionRequestDto request);

    @PUT("api/transactions/{id}")
    Call<TransactionDto> updateTransaction(
            @Path("id") String transactionId,
            @Header("If-Match") String ifMatch,
            @Body TransactionRequestDto request);

    @DELETE("api/transactions/{id}")
    Call<Void> deleteTransaction(
            @Path("id") String transactionId,
            @Header("If-Match") String ifMatch);

    @GET("api/categories")
    Call<JsonElement> getCategories();

    @POST("api/categories")
    Call<CategoryDto> createCategory(@Body CategoryRequestDto request);

    @PUT("api/categories/{id}")
    Call<CategoryDto> updateCategory(
            @Path("id") String id,
            @Header("If-Match") String ifMatch,
            @Body CategoryRequestDto request);

    @DELETE("api/categories/{id}")
    Call<Void> deleteCategory(@Path("id") String id, @Header("If-Match") String ifMatch);

    @GET("api/budgets")
    Call<JsonElement> getBudgets(@Query("monthYear") String monthYear);

    @POST("api/budgets")
    Call<BudgetDto> createBudget(@Body BudgetRequestDto request);

    @PUT("api/budgets/{id}")
    Call<BudgetDto> updateBudget(
            @Path("id") String budgetId,
            @Header("If-Match") String ifMatch,
            @Body BudgetRequestDto request);

    @DELETE("api/budgets/{id}")
    Call<Void> deleteBudget(@Path("id") String budgetId, @Header("If-Match") String ifMatch);

    @GET("api/goals")
    Call<JsonElement> getGoals();

    @POST("api/goals")
    Call<GoalDto> createGoal(@Body GoalRequestDto request);

    @PUT("api/goals/{id}")
    Call<GoalDto> updateGoal(
            @Path("id") String goalId,
            @Header("If-Match") String ifMatch,
            @Body GoalRequestDto request);

    @DELETE("api/goals/{id}")
    Call<Void> deleteGoal(@Path("id") String goalId, @Header("If-Match") String ifMatch);

    @POST("api/goals/{id}/funds")
    Call<GoalDto> addGoalFunds(
            @Path("id") String goalId,
            @Header("Idempotency-Key") String idempotencyKey,
            @Header("If-Match") String ifMatch,
            @Body AddGoalFundsRequestDto request);

    @GET("api/goals/available-balance")
    Call<com.example.appquanlychitieu.data.remote.dto.AvailableBalanceDto> getAvailableBalance(
            @Query("year") int year, @Query("month") int month);

    @POST("api/goals/{id}/complete")
    Call<GoalDto> completeGoal(
            @Path("id") String goalId,
            @Header("Idempotency-Key") String idempotencyKey,
            @Header("If-Match") String ifMatch,
            @Body com.example.appquanlychitieu.data.remote.dto.CompleteGoalRequestDto request);

    @POST("api/goals/{id}/cancel")
    Call<GoalDto> cancelGoal(
            @Path("id") String goalId,
            @Header("If-Match") String ifMatch);

    @GET("api/goals/{id}/history")
    Call<JsonElement> getGoalHistory(@Path("id") String goalId);

    @GET("api/reminders")
    Call<JsonElement> getReminders();

    @POST("api/reminders")
    Call<ReminderDto> createReminder(
            @Header("Idempotency-Key") String idempotencyKey,
            @Body ReminderRequestDto request);

    @PUT("api/reminders/{id}")
    Call<ReminderDto> updateReminder(
            @Path("id") String reminderId,
            @Header("If-Match") String ifMatch,
            @Body ReminderRequestDto request);

    @DELETE("api/reminders/{id}")
    Call<Void> deleteReminder(@Path("id") String reminderId, @Header("If-Match") String ifMatch);

    @GET("api/statistics/by-category")
    Call<JsonElement> getStatisticsByCategory(
            @Query("from") String from,
            @Query("to") String to);

    @GET("api/statistics/monthly")
    Call<JsonElement> getMonthlyStatistics(@Query("year") int year);

    @GET("api/reports/range.xlsx")
    Call<ResponseBody> downloadRangeReport(
            @Query("from") String from,
            @Query("to") String to);

    @GET("api/reports/range.pdf")
    Call<ResponseBody> downloadRangePdf(
            @Query("from") String from,
            @Query("to") String to);

    @Multipart
    @POST("api/receipts")
    Call<ReceiptDto> uploadReceipt(
            @Header("Idempotency-Key") String idempotencyKey,
            @Part MultipartBody.Part image);

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

    @DELETE("api/receipts/{id}")
    Call<Void> deleteReceipt(@Path("id") String receiptId);

    @Streaming
    @GET("api/receipts/{id}/image")
    Call<ResponseBody> downloadReceiptImage(@Path("id") String receiptId);
}
