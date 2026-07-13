package com.example.appquanlychitieu.data.model;

public class Reminder {
    private long id;
    
    private String content;
    private int dayOfMonth; 
    private int hour;
    private int minute;
    private long userId;
    private boolean isActive;
    private String remoteId;

    public Reminder(String content, int dayOfMonth, int hour, int minute, long userId, boolean isActive) {
        this.content = content;
        this.dayOfMonth = dayOfMonth;
        this.hour = hour;
        this.minute = minute;
        this.userId = userId;
        this.isActive = isActive;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public String getContent() { return content; }
    public void setContent(String content) { this.content = content; }

    public int getDayOfMonth() { return dayOfMonth; }
    public void setDayOfMonth(int dayOfMonth) { this.dayOfMonth = dayOfMonth; }

    public int getHour() { return hour; }
    public void setHour(int hour) { this.hour = hour; }

    public int getMinute() { return minute; }
    public void setMinute(int minute) { this.minute = minute; }

    public long getUserId() { return userId; }
    public void setUserId(long userId) { this.userId = userId; }

    public boolean isActive() { return isActive; }
    public void setActive(boolean active) { isActive = active; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }
}
