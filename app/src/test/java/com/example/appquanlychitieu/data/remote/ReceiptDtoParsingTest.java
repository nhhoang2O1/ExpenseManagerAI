package com.example.appquanlychitieu.data.remote;

import static org.junit.Assert.assertEquals;

import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.google.gson.Gson;

import org.junit.Test;

import java.math.BigDecimal;

public class ReceiptDtoParsingTest {
    @Test
    public void parsesCamelCaseReceiptContractWithoutFloatingPointLoss() {
        String json = "{"
                + "\"id\":\"receipt-1\","
                + "\"status\":\"REVIEW_REQUIRED\","
                + "\"classification\":\"GENERIC\","
                + "\"storeName\":\"Circle K\","
                + "\"receiptDate\":\"2026-07-09\","
                + "\"totalAmount\":999999999999999999,"
                + "\"vatAmount\":10000,"
                + "\"overallConfidence\":0.91"
                + "}";

        ReceiptDto receipt = new Gson().fromJson(json, ReceiptDto.class);

        assertEquals("REVIEW_REQUIRED", receipt.status);
        assertEquals("2026-07-09", receipt.receiptDate);
        assertEquals(new BigDecimal("999999999999999999"), receipt.totalAmount);
        assertEquals(new BigDecimal("10000"), receipt.vatAmount);
    }
}
