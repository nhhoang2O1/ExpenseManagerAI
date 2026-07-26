package com.example.appquanlychitieu.data.remote;

import static org.junit.Assert.assertEquals;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

import java.io.IOException;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

import okhttp3.OkHttpClient;
import okhttp3.Protocol;
import okhttp3.Request;
import okhttp3.Response;
import okhttp3.ResponseBody;
import okhttp3.mockwebserver.MockResponse;
import okhttp3.mockwebserver.MockWebServer;

public class RefreshTokenAuthenticatorTest {
    private MockWebServer server;

    @Before
    public void setUp() throws IOException {
        server = new MockWebServer();
        server.start();
    }

    @After
    public void tearDown() throws IOException {
        server.shutdown();
    }

    @Test
    public void concurrent401sPerformOneRefreshAndBothRetryWithRotatedToken() throws Exception {
        server.enqueue(new MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/json")
                .setBodyDelay(100, TimeUnit.MILLISECONDS)
                .setBody("{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\",\"expiresIn\":900}"));
        InMemoryTokens tokens = new InMemoryTokens("old-access", "old-refresh");
        RefreshTokenAuthenticator authenticator = new RefreshTokenAuthenticator(
                null,
                tokens,
                new OkHttpClient(),
                server.url("/api/auth/refresh").toString());
        Response first401 = unauthorized("https://example.test/api/transactions", "old-access");
        Response second401 = unauthorized("https://example.test/api/budgets", "old-access");
        AtomicReference<Request> firstRetry = new AtomicReference<>();
        AtomicReference<Request> secondRetry = new AtomicReference<>();
        CountDownLatch done = new CountDownLatch(2);
        ExecutorService executor = Executors.newFixedThreadPool(2);

        executor.execute(() -> authenticate(authenticator, first401, firstRetry, done));
        executor.execute(() -> authenticate(authenticator, second401, secondRetry, done));

        org.junit.Assert.assertTrue(done.await(5, TimeUnit.SECONDS));
        executor.shutdownNow();
        assertEquals(1, server.getRequestCount());
        assertEquals("Bearer new-access", firstRetry.get().header("Authorization"));
        assertEquals("Bearer new-access", secondRetry.get().header("Authorization"));
        assertEquals("new-refresh", tokens.refresh.get());
    }

    private static void authenticate(
            RefreshTokenAuthenticator authenticator,
            Response response,
            AtomicReference<Request> result,
            CountDownLatch done) {
        try {
            result.set(authenticator.authenticate(null, response));
        } catch (IOException exception) {
            throw new AssertionError(exception);
        } finally {
            done.countDown();
        }
    }

    private static Response unauthorized(String url, String token) {
        Request request = new Request.Builder()
                .url(url)
                .header("Authorization", "Bearer " + token)
                .build();
        return new Response.Builder()
                .request(request)
                .protocol(Protocol.HTTP_1_1)
                .code(401)
                .message("Unauthorized")
                .body(ResponseBody.create(null, new byte[0]))
                .build();
    }

    private static final class InMemoryTokens implements RefreshTokenAuthenticator.TokenAccess {
        private final AtomicReference<String> access;
        private final AtomicReference<String> refresh;

        private InMemoryTokens(String access, String refresh) {
            this.access = new AtomicReference<>(access);
            this.refresh = new AtomicReference<>(refresh);
        }

        @Override public String getAccessToken() { return access.get(); }
        @Override public String getRefreshToken() { return refresh.get(); }
        @Override public void savePair(String newAccess, String newRefresh, int expiresIn) {
            access.set(newAccess);
            refresh.set(newRefresh);
        }
        @Override public void clear() {
            access.set("");
            refresh.set("");
        }
    }
}
