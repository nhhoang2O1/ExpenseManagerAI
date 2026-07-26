package com.example.appquanlychitieu.data.repository;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.remote.ApiClient;
import com.example.appquanlychitieu.data.remote.ApiService;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.io.IOException;
import java.lang.reflect.Field;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

import okhttp3.mockwebserver.MockResponse;
import okhttp3.mockwebserver.MockWebServer;
import retrofit2.Retrofit;
import retrofit2.converter.gson.GsonConverterFactory;

public class RemoteTransactionRepositoryPaginationTest {
    private MockWebServer server;
    private Field serviceField;

    @Before
    public void setUp() throws Exception {
        server = new MockWebServer();
        server.start();
        ApiService service = new Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(GsonConverterFactory.create())
                .build()
                .create(ApiService.class);
        serviceField = ApiClient.class.getDeclaredField("apiService");
        serviceField.setAccessible(true);
        serviceField.set(null, service);
    }

    @After
    public void tearDown() throws Exception {
        serviceField.set(null, null);
        server.shutdown();
    }

    @Test
    public void loadsEveryServerPageWithoutDroppingOrDuplicatingItems() throws Exception {
        server.enqueue(page(1, 2, "t-1", "t-2"));
        server.enqueue(page(2, 2, "t-3"));
        AtomicReference<List<Transaction>> result = new AtomicReference<>();
        AtomicReference<ApiError> error = new AtomicReference<>();
        CountDownLatch done = new CountDownLatch(1);

        new RemoteTransactionRepository(null).getTransactions(42L,
                new RemoteCallback<List<Transaction>>() {
                    @Override public void onSuccess(List<Transaction> value) {
                        result.set(value);
                        done.countDown();
                    }
                    @Override public void onError(ApiError value) {
                        error.set(value);
                        done.countDown();
                    }
                });

        org.junit.Assert.assertTrue(done.await(5, TimeUnit.SECONDS));
        assertNull(error.get());
        assertEquals(3, result.get().size());
        assertEquals("t-1", result.get().get(0).getRemoteId());
        assertEquals("t-3", result.get().get(2).getRemoteId());
        assertEquals("/api/transactions?page=1&pageSize=100", server.takeRequest().getPath());
        assertEquals("/api/transactions?page=2&pageSize=100", server.takeRequest().getPath());
    }

    private static MockResponse page(int page, int totalPages, String... ids) {
        StringBuilder items = new StringBuilder();
        for (String id : ids) {
            if (items.length() > 0) items.append(',');
            items.append("{\"id\":\"").append(id)
                    .append("\",\"amount\":1000,\"transactionDate\":\"2026-07-01\",\"type\":\"EXPENSE\"}");
        }
        return new MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/json")
                .setBody("{\"items\":[" + items + "],\"page\":" + page
                        + ",\"totalPages\":" + totalPages + "}");
    }
}
