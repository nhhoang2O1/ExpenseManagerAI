package com.example.appquanlychitieu.data.model;

public class Budget {
    private long id;
    private long categoryId;
    private double amount;
    private String monthYear; 
    private long userId;
    private String remoteId;
    private String remoteCategoryId;
    private String remoteCategoryName;
    private String remoteCategoryColor;
    private String remoteCategoryIcon;

    public Budget() {}

    public Budget(long categoryId, double amount, String monthYear, long userId) {
        this.categoryId = categoryId;
        this.amount = amount;
        this.monthYear = monthYear;
        this.userId = userId;
    }


    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public long getCategoryId() { return categoryId; }
    public void setCategoryId(long categoryId) { this.categoryId = categoryId; }

    public double getAmount() { return amount; }
    public void setAmount(double amount) { this.amount = amount; }

    public String getMonthYear() { return monthYear; }
    public void setMonthYear(String monthYear) { this.monthYear = monthYear; }

    public long getUserId() { return userId; }
    public void setUserId(long userId) { this.userId = userId; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }

    public String getRemoteCategoryId() { return remoteCategoryId; }
    public void setRemoteCategoryId(String remoteCategoryId) { this.remoteCategoryId = remoteCategoryId; }

    public String getRemoteCategoryName() { return remoteCategoryName; }
    public void setRemoteCategoryName(String remoteCategoryName) { this.remoteCategoryName = remoteCategoryName; }

    public String getRemoteCategoryColor() { return remoteCategoryColor; }
    public void setRemoteCategoryColor(String remoteCategoryColor) { this.remoteCategoryColor = remoteCategoryColor; }

    public String getRemoteCategoryIcon() { return remoteCategoryIcon; }
    public void setRemoteCategoryIcon(String remoteCategoryIcon) { this.remoteCategoryIcon = remoteCategoryIcon; }
}
