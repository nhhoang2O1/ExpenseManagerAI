package com.example.appquanlychitieu.data.model;

public class MonthlySummary {
    private String monthYear;
    private long totalIncome;
    private long totalExpense;

    public String getMonthYear() { return monthYear; }
    public void setMonthYear(String monthYear) { this.monthYear = monthYear; }

    public long getTotalIncome() { return totalIncome; }
    public void setTotalIncome(long totalIncome) { this.totalIncome = totalIncome; }

    public long getTotalExpense() { return totalExpense; }
    public void setTotalExpense(long totalExpense) { this.totalExpense = totalExpense; }

    public long getBalance() { return totalIncome - totalExpense; }
}
