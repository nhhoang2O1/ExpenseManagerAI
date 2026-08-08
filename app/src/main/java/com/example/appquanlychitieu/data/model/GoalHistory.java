package com.example.appquanlychitieu.data.model;

public class GoalHistory {
    private long id;
    
    private long goalId;
    private long amountAdded;
    private long date;
    private String remoteId;
    private String remoteGoalId;
    private String actionType = "FUND";

    public GoalHistory(long goalId, long amountAdded, long date) {
        this.goalId = goalId;
        this.amountAdded = amountAdded;
        this.date = date;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public long getGoalId() { return goalId; }
    public void setGoalId(long goalId) { this.goalId = goalId; }

    public long getAmountAdded() { return amountAdded; }
    public void setAmountAdded(long amountAdded) { this.amountAdded = amountAdded; }

    public long getDate() { return date; }
    public void setDate(long date) { this.date = date; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }

    public String getRemoteGoalId() { return remoteGoalId; }
    public void setRemoteGoalId(String remoteGoalId) { this.remoteGoalId = remoteGoalId; }
    public String getActionType() { return actionType; }
    public void setActionType(String actionType) {
        this.actionType = actionType == null ? "FUND" : actionType;
    }
}
