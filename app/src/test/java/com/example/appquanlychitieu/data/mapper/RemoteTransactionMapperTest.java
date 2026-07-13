package com.example.appquanlychitieu.data.mapper;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.google.gson.Gson;

import org.junit.Test;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.ZoneId;

public class RemoteTransactionMapperTest {
    @Test
    public void mapsIntegerVndAndIsoDateToLocalReadModel() {
        TransactionDto dto = new TransactionDto();
        dto.id = "6b96c8a4-remote";
        dto.amount = new BigDecimal("125000");
        dto.transactionDate = "2026-07-09";
        dto.type = "EXPENSE";
        dto.note = "Hoa don";
        dto.categoryName = "An uong";

        Transaction mapped = RemoteTransactionMapper.toLocalView(dto, 42L);

        long expectedEpoch = LocalDate.of(2026, 7, 9)
                .atStartOfDay(ZoneId.of("Asia/Ho_Chi_Minh"))
                .toInstant()
                .toEpochMilli();
        assertEquals(125000d, mapped.getAmount(), 0d);
        assertEquals(expectedEpoch, mapped.getDate());
        assertEquals(TransactionType.EXPENSE, mapped.getType());
        assertEquals("An uong", mapped.getRemoteCategoryName());
        assertTrue(mapped.getId() < 0);
    }

    @Test
    public void parsesBackendTransactionDateField() {
        TransactionDto dto = new Gson().fromJson(
                "{\"id\":\"remote-1\",\"amount\":42000,"
                        + "\"transactionDate\":\"2026-07-09\","
                        + "\"type\":\"EXPENSE\",\"categoryName\":\"An uong\"}",
                TransactionDto.class);

        Transaction mapped = RemoteTransactionMapper.toLocalView(dto, 42L);

        assertEquals(
                LocalDate.of(2026, 7, 9)
                        .atStartOfDay(ZoneId.of("Asia/Ho_Chi_Minh"))
                        .toInstant()
                        .toEpochMilli(),
                mapped.getDate());
    }
}
