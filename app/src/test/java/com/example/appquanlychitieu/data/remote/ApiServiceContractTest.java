package com.example.appquanlychitieu.data.remote;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;

import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.io.IOException;

import okhttp3.mockwebserver.MockResponse;
import okhttp3.mockwebserver.MockWebServer;
import okhttp3.mockwebserver.RecordedRequest;
import retrofit2.Response;
import retrofit2.Retrofit;
import retrofit2.converter.gson.GsonConverterFactory;

/** Verifies the Android client contract without depending on a running backend. */
public class ApiServiceContractTest {
    private MockWebServer server;
    private ApiService service;

    @Before
    public void setUp() throws IOException {
        server = new MockWebServer();
        server.start();
        service = new Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(GsonConverterFactory.create())
                .build()
                .create(ApiService.class);
    }

    @After
    public void tearDown() throws IOException {
        server.shutdown();
    }

    @Test
    public void transactionPaginationUsesPageAndPageSize() throws Exception {
        server.enqueue(json(200, "{\"items\":[],\"page\":3,\"totalPages\":7}"));

        service.getTransactions(3, 25).execute();

        RecordedRequest request = server.takeRequest();
        assertEquals("GET", request.getMethod());
        assertEquals("/api/transactions?page=3&pageSize=25", request.getPath());
    }

    @Test
    public void acceptedReceiptCanBePolledUntilReview() throws Exception {
        server.enqueue(json(202, "{\"id\":\"r-1\",\"status\":\"QUEUED\"}"));
        server.enqueue(json(200, "{\"id\":\"r-1\",\"status\":\"REVIEW_REQUIRED\"}"));

        Response<ReceiptDto> accepted = service.processReceipt("r-1").execute();
        Response<ReceiptDto> reviewed = service.getReceipt("r-1").execute();

        assertEquals(202, accepted.code());
        assertNotNull(accepted.body());
        assertEquals("QUEUED", accepted.body().status);
        assertEquals("REVIEW_REQUIRED", reviewed.body().status);
        RecordedRequest process = server.takeRequest();
        RecordedRequest poll = server.takeRequest();
        assertEquals("POST", process.getMethod());
        assertEquals("/api/receipts/r-1/process", process.getPath());
        assertEquals("GET", poll.getMethod());
        assertEquals("/api/receipts/r-1", poll.getPath());
    }

    private static MockResponse json(int code, String body) {
        return new MockResponse()
                .setResponseCode(code)
                .addHeader("Content-Type", "application/json")
                .setBody(body);
    }
}
