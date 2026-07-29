package com.example.appquanlychitieu.data.remote;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import okhttp3.MediaType;
import okhttp3.ResponseBody;

import org.junit.Test;

import java.io.IOException;

import retrofit2.Response;

public class ApiResponseHelperTest {
    private static final MediaType JSON = MediaType.get("application/json");

    @Test
    public void fromResponse_prefersApiMessage() {
        ApiError error = ApiResponseHelper.fromResponse(
                errorResponse(422, "{\"message\":\"Du lieu khong hop le\",\"title\":\"Fallback\"}"));

        assertEquals(422, error.getStatusCode());
        assertEquals("Du lieu khong hop le", error.getMessage());
        assertFalse(error.isNetworkError());
    }

    @Test
    public void fromResponse_usesProblemDetailsTitleWhenMessageIsMissing() {
        ApiError error = ApiResponseHelper.fromResponse(
                errorResponse(409, "{\"title\":\"Du lieu bi xung dot\"}"));

        assertEquals(409, error.getStatusCode());
        assertEquals("Du lieu bi xung dot", error.getMessage());
        assertFalse(error.isNetworkError());
    }

    @Test
    public void fromResponse_reportsStatusWhenErrorBodyCannotBeParsed() {
        ApiError error = ApiResponseHelper.fromResponse(errorResponse(503, "not-json"));

        assertEquals(503, error.getStatusCode());
        assertEquals("May chu tra ve loi 503", error.getMessage());
        assertFalse(error.isNetworkError());
    }

    @Test
    public void fromFailure_preservesUsefulMessageAndFallsBackForBlankMessage() {
        ApiError detailed = ApiResponseHelper.fromFailure(new IOException("timeout"));
        ApiError blank = ApiResponseHelper.fromFailure(new IOException("  "));

        assertEquals(0, detailed.getStatusCode());
        assertEquals("timeout", detailed.getMessage());
        assertTrue(detailed.isNetworkError());
        assertEquals("Khong the ket noi den may chu", blank.getMessage());
        assertTrue(blank.isNetworkError());
    }

    private static Response<Object> errorResponse(int statusCode, String body) {
        return Response.error(statusCode, ResponseBody.Companion.create(body, JSON));
    }
}
