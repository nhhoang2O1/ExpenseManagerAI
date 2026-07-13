package com.example.appquanlychitieu.data.model;

public class GoalHistory {
    private long id;
    
    private long goalId;
    private double amountAdded;
    private long date;
    private String remoteId;
    private String remoteGoalId;

    public GoalHistory(long goalId, double amountAdded, long date) {
        this.goalId = goalId;
        this.amountAdded = amountAdded;
        this.date = date;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public long getGoalId() { return goalId; }
    public void setGoalId(long goalId) { this.goalId = goalId; }

    public double getAmountAdded() { return amountAdded; }
    public void setAmountAdded(double amountAdded) { this.amountAdded = amountAdded; }

    public long getDate() { return date; }
    public void setDate(long date) { this.date = date; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }

    public String getRemoteGoalId() { return remoteGoalId; }
    public void setRemoteGoalId(String remoteGoalId) { this.remoteGoalId = remoteGoalId; }
}
