package com.example.appquanlychitieu.data.model;

public class Goal {
    private long id;
    
    private String name;
    private long targetAmount;
    private long currentAmount;
    private long userId;
    private String remoteId;
    private long version = 1L;
    private String status = "ACTIVE";
    private String completedAt;

    public Goal(String name, long targetAmount, long currentAmount, long userId) {
        this.name = name;
        this.targetAmount = targetAmount;
        this.currentAmount = currentAmount;
        this.userId = userId;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public long getTargetAmount() { return targetAmount; }
    public void setTargetAmount(long targetAmount) { this.targetAmount = targetAmount; }

    public long getCurrentAmount() { return currentAmount; }
    public void setCurrentAmount(long currentAmount) { this.currentAmount = currentAmount; }

    public long getUserId() { return userId; }
    public void setUserId(long userId) { this.userId = userId; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }
    public long getVersion() { return version; }
    public void setVersion(long version) { this.version = version; }
    public String getStatus() { return status; }
    public void setStatus(String status) { this.status = status == null ? "ACTIVE" : status; }
    public String getCompletedAt() { return completedAt; }
    public void setCompletedAt(String completedAt) { this.completedAt = completedAt; }
    public boolean isActive() { return "ACTIVE".equals(status); }
    public boolean isReadyToComplete() { return "READY_TO_COMPLETE".equals(status); }
    public boolean isCompleted() { return "COMPLETED".equals(status); }
    public boolean isCancelled() { return "CANCELLED".equals(status); }
}
