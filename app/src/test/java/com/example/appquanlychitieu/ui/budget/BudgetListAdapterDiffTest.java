package com.example.appquanlychitieu.ui.budget;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.example.appquanlychitieu.data.model.Budget;

import org.junit.Test;

public class BudgetListAdapterDiffTest {
    @Test
    public void backendIdDefinesIdentity() {
        Budget first = budget("budget-1", 1_000_000L);
        Budget same = budget("budget-1", 1_000_000L);
        Budget changed = budget("budget-1", 2_000_000L);
        assertTrue(BudgetListAdapter.sameItem(first, same));
        assertTrue(BudgetListAdapter.sameContent(first, same));
        assertFalse(BudgetListAdapter.sameContent(first, changed));
    }

    private Budget budget(String id, long amount) {
        Budget value = new Budget(10L, amount, "2026-07", 1L);
        value.setRemoteId(id);
        value.setRemoteCategoryName("Ăn uống");
        value.setRemoteCategoryColor("#0B6B53");
        value.setRemoteCategoryIcon("ic_food");
        return value;
    }
}
