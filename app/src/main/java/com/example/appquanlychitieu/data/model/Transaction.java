package com.example.appquanlychitieu.data.model;

public class Transaction {
    private long id;
    /** Whole Vietnamese đồng; persisted money never uses floating point. */
    private long amount;
    private String note;
    private long date; 
    private Long categoryId; 
    private TransactionType type;
    private long userId;
    private String remoteCategoryName;
    private String remoteId;
    private String remoteCategoryId;
    private String remoteStoreName;
    private String remoteCategoryColor;
    private String remoteCategoryIcon;
    private long version = 1L;

    public Transaction() {}

    public Transaction(long amount, String note, long date, Long categoryId, TransactionType type, long userId) {
        this.amount = amount;
        this.note = note;
        this.date = date;
        this.categoryId = categoryId;
        this.type = type;
        this.userId = userId;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public long getAmount() { return amount; }
    public void setAmount(long amount) { this.amount = amount; }

    public String getNote() { return note; }
    public void setNote(String note) { this.note = note; }

    public long getDate() { return date; }
    public void setDate(long date) { this.date = date; }

    public Long getCategoryId() { return categoryId; }
    public void setCategoryId(Long categoryId) { this.categoryId = categoryId; }

    public TransactionType getType() { return type; }
    public void setType(TransactionType type) { this.type = type; }

    public long getUserId() { return userId; }
    public void setUserId(long userId) { this.userId = userId; }

    public String getRemoteCategoryName() { return remoteCategoryName; }
    public void setRemoteCategoryName(String remoteCategoryName) { this.remoteCategoryName = remoteCategoryName; }

    public String getRemoteId() { return remoteId; }
    public void setRemoteId(String remoteId) { this.remoteId = remoteId; }

    public String getRemoteCategoryId() { return remoteCategoryId; }
    public void setRemoteCategoryId(String remoteCategoryId) { this.remoteCategoryId = remoteCategoryId; }

    public String getRemoteStoreName() { return remoteStoreName; }
    public void setRemoteStoreName(String remoteStoreName) { this.remoteStoreName = remoteStoreName; }

    public String getRemoteCategoryColor() { return remoteCategoryColor; }
    public void setRemoteCategoryColor(String remoteCategoryColor) { this.remoteCategoryColor = remoteCategoryColor; }

    public String getRemoteCategoryIcon() { return remoteCategoryIcon; }
    public void setRemoteCategoryIcon(String remoteCategoryIcon) { this.remoteCategoryIcon = remoteCategoryIcon; }
    public long getVersion() { return version; }
    public void setVersion(long version) { this.version = version; }
}
