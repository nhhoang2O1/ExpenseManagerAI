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
