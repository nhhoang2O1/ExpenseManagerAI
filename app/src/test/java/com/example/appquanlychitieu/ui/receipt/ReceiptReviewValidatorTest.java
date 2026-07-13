package com.example.appquanlychitieu.ui.receipt;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.math.BigDecimal;

public class ReceiptReviewValidatorTest {
    @Test
    public void validReview_keepsIntegerVndValues() {
        ReceiptReviewValidator.ValidationResult result =
                ReceiptReviewValidator.validate(
                        "Circle K",
                        "2026-07-09",
                        "125.000",
                        "10,000",
                        "category-id");

        assertTrue(result.valid);
        assertEquals(new BigDecimal("125000"), result.totalAmount);
        assertEquals(new BigDecimal("10000"), result.vatAmount);
    }

    @Test
    public void vatAboveTotal_isRejected() {
        ReceiptReviewValidator.ValidationResult result =
                ReceiptReviewValidator.validate(
                        "GS25",
                        "2026-07-09",
                        "50000",
                        "60000",
                        "category-id");

        assertFalse(result.valid);
        assertEquals(ReceiptReviewValidator.Field.VAT, result.field);
    }

    @Test
    public void malformedIsoDate_isRejected() {
        ReceiptReviewValidator.ValidationResult result =
                ReceiptReviewValidator.validate(
                        "GS25",
                        "09/07/2026",
                        "50000",
                        "",
                        "category-id");

        assertFalse(result.valid);
        assertEquals(ReceiptReviewValidator.Field.DATE, result.field);
    }

    @Test
    public void fractionalVnd_isRejected() {
        ReceiptReviewValidator.ValidationResult result =
                ReceiptReviewValidator.validate(
                        "GS25",
                        "2026-07-09",
                        "50000.50",
                        "",
                        "category-id");

        assertFalse(result.valid);
        assertEquals(ReceiptReviewValidator.Field.TOTAL, result.field);
    }
}
