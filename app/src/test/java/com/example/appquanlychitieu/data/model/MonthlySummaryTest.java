package com.example.appquanlychitieu.data.model;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class MonthlySummaryTest {
    @Test
    public void expense_only_account_has_negative_net_cash_flow() {
        MonthlySummary summary = new MonthlySummary();
        summary.setTotalIncome(0L);
        summary.setTotalExpense(104_000L);
        assertEquals(-104_000L, summary.getBalance());
    }

    @Test
    public void income_account_uses_the_same_monthly_formula() {
        MonthlySummary summary = new MonthlySummary();
        summary.setTotalIncome(300_000L);
        summary.setTotalExpense(135_000L);
        assertEquals(165_000L, summary.getBalance());
    }
}
