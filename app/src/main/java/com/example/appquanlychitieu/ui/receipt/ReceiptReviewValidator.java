package com.example.appquanlychitieu.ui.receipt;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.format.DateTimeParseException;

public final class ReceiptReviewValidator {
    private ReceiptReviewValidator() {}

    public static ValidationResult validate(
            String storeName,
            String receiptDate,
            String totalAmount,
            String vatAmount,
            String categoryId) {
        if (storeName == null || storeName.trim().isEmpty()) {
            return ValidationResult.error(Field.STORE);
        }
        try {
            LocalDate.parse(receiptDate == null ? "" : receiptDate.trim());
        } catch (DateTimeParseException exception) {
            return ValidationResult.error(Field.DATE);
        }

        BigDecimal total = parseVnd(totalAmount);
        if (total == null || total.signum() <= 0 || hasFraction(total)) {
            return ValidationResult.error(Field.TOTAL);
        }

        BigDecimal vat = null;
        if (vatAmount != null && !vatAmount.trim().isEmpty()) {
            vat = parseVnd(vatAmount);
            if (vat == null || vat.signum() < 0 || hasFraction(vat)
                    || vat.compareTo(total) > 0) {
                return ValidationResult.error(Field.VAT);
            }
        }

        if (categoryId == null || categoryId.trim().isEmpty()) {
            return ValidationResult.error(Field.CATEGORY);
        }
        return ValidationResult.valid(total, vat);
    }

    public static BigDecimal parseVnd(String value) {
        if (value == null) {
            return null;
        }
        String normalized = value.trim().replace(" ", "");
        if (normalized.isEmpty()) {
            return null;
        }
        if (normalized.matches("[+-]?\\d{1,3}([.,]\\d{3})+")) {
            normalized = normalized.replace(".", "").replace(",", "");
        } else if (normalized.contains(".") && normalized.contains(",")) {
            return null;
        } else {
            normalized = normalized.replace(',', '.');
        }
        try {
            return new BigDecimal(normalized);
        } catch (NumberFormatException exception) {
            return null;
        }
    }

    private static boolean hasFraction(BigDecimal amount) {
        return amount.stripTrailingZeros().scale() > 0;
    }

    public enum Field {
        NONE,
        STORE,
        DATE,
        TOTAL,
        VAT,
        CATEGORY
    }

    public static final class ValidationResult {
        public final boolean valid;
        public final Field field;
        public final BigDecimal totalAmount;
        public final BigDecimal vatAmount;

        private ValidationResult(
                boolean valid,
                Field field,
                BigDecimal totalAmount,
                BigDecimal vatAmount) {
            this.valid = valid;
            this.field = field;
            this.totalAmount = totalAmount;
            this.vatAmount = vatAmount;
        }

        static ValidationResult valid(BigDecimal totalAmount, BigDecimal vatAmount) {
            return new ValidationResult(true, Field.NONE, totalAmount, vatAmount);
        }

        static ValidationResult error(Field field) {
            return new ValidationResult(false, field, null, null);
        }
    }
}
