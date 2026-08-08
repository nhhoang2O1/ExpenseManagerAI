package com.example.appquanlychitieu.data.mapper;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;

import java.time.LocalDate;
import java.time.ZoneId;

public final class RemoteTransactionMapper {
    private static final ZoneId APP_ZONE = ZoneId.of("Asia/Ho_Chi_Minh");

    private RemoteTransactionMapper() {}

    public static Transaction toLocalView(TransactionDto remote, long cacheUserId) {
        Transaction local = new Transaction();
        local.setId(toReadOnlyId(remote.id));
        local.setAmount(toVnd(remote.amount));
        local.setNote(remote.resolvedNote());
        local.setDate(toEpochMillis(remote.transactionDate));
        local.setCategoryId(null);
        local.setType("INCOME".equalsIgnoreCase(remote.type)
                ? TransactionType.INCOME
                : TransactionType.EXPENSE);
        local.setUserId(cacheUserId);
        local.setRemoteCategoryName(remote.resolvedCategoryName());
        local.setRemoteId(remote.id);
        local.setRemoteCategoryId(remote.categoryId);
        local.setRemoteStoreName(remote.storeName);
        local.setRemoteCategoryColor(remote.resolvedCategoryColor());
        local.setRemoteCategoryIcon(remote.resolvedCategoryIcon());
        local.setRemoteReceiptId(remote.receiptId);
        local.setRemoteGoalId(remote.goalId);
        local.setVersion(remote.version);
        return local;
    }

    public static long toEpochMillis(String isoDate) {
        if (isoDate == null || isoDate.trim().isEmpty()) {
            return System.currentTimeMillis();
        }
        try {
            return LocalDate.parse(isoDate)
                    .atStartOfDay(APP_ZONE)
                    .toInstant()
                    .toEpochMilli();
        } catch (RuntimeException ignored) {
            return System.currentTimeMillis();
        }
    }

    public static long toReadOnlyId(String remoteId) {
        long hash = remoteId == null ? 1L : remoteId.hashCode();
        return -Math.max(1L, Math.abs(hash));
    }

    private static long toVnd(java.math.BigDecimal amount) {
        if (amount == null) return 0L;
        try {
            return amount.longValueExact();
        } catch (ArithmeticException ignored) {
            // The backend contract is integer VND. Keep a deterministic
            // fallback for malformed legacy payloads.
            return amount.setScale(0, java.math.RoundingMode.HALF_UP).longValue();
        }
    }
}
