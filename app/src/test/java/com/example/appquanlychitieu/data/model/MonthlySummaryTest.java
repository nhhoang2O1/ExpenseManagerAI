package com.example.appquanlychitieu.data.model;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class MonthlySummaryTest {
    @Test
    public void expense_only_account_keeps_monthly_savings_in_remaining_amount() {
        MonthlySummary summary = new MonthlySummary();
        summary.setTotalIncome(0L);
        summary.setTotalExpense(104_000L);
        summary.setTotalSavings(100_000L);

        assertEquals(100_000L, summary.getTotalSavings());
        assertEquals(-204_000L, summary.getBalance());
    }

    @Test
    public void income_account_uses_the_same_monthly_formula() {
        MonthlySummary summary = new MonthlySummary();
        summary.setTotalIncome(300_000L);
        summary.setTotalExpense(135_000L);
        summary.setTotalSavings(80_000L);

        assertEquals(80_000L, summary.getTotalSavings());
        assertEquals(85_000L, summary.getBalance());
    }
}
