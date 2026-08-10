package com.example.appquanlychitieu.ui.transaction;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;

import org.junit.Test;

public class TransactionListAdapterDiffTest {
    @Test
    public void remoteIdDefinesIdentityAndVisibleFieldsDefineContent() {
        Transaction first = transaction("remote-1", 100_000L);
        Transaction same = transaction("remote-1", 100_000L);
        Transaction changed = transaction("remote-1", 120_000L);
        assertTrue(TransactionListAdapter.sameItem(first, same));
        assertTrue(TransactionListAdapter.sameContent(first, same));
        assertFalse(TransactionListAdapter.sameContent(first, changed));
    }

    @Test
    public void dateHeaderOnlyStartsWhenCalendarDayChanges() {
        Transaction first = transaction("remote-1", 100_000L);
        Transaction sameDay = transaction("remote-2", 120_000L);
        sameDay.setDate(first.getDate() + 60_000L);
        Transaction nextDay = transaction("remote-3", 130_000L);
        nextDay.setDate(first.getDate() + 86_400_000L);

        assertTrue(TransactionListAdapter.startsNewDay(null, first));
        assertFalse(TransactionListAdapter.startsNewDay(first, sameDay));
        assertTrue(TransactionListAdapter.startsNewDay(first, nextDay));
    }

    private Transaction transaction(String id, long amount) {
        Transaction value = new Transaction(amount, "Ghi chú", 1_700_000_000_000L,
                1L, TransactionType.EXPENSE, 1L);
        value.setRemoteId(id);
        value.setRemoteCategoryName("Ăn uống");
        value.setRemoteCategoryColor("#2563EB");
        value.setRemoteCategoryIcon("ic_food");
        return value;
    }
}
