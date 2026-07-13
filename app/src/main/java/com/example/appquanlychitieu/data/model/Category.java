package com.example.appquanlychitieu.data.model;

public class Category {
    private long id;
    private String name;
    private String icon;
    private String color;
    private TransactionType type;
    private boolean isDefault;

    public Category() {}

    public Category(String name, String icon, String color, TransactionType type, boolean isDefault) {
        this.name = name;
        this.icon = icon;
        this.color = color;
        this.type = type;
        this.isDefault = isDefault;
    }

    public long getId() { return id; }
    public void setId(long id) { this.id = id; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public String getIcon() { return icon; }
    public void setIcon(String icon) { this.icon = icon; }

    public String getColor() { return color; }
    public void setColor(String color) { this.color = color; }

    public TransactionType getType() { return type; }
    public void setType(TransactionType type) { this.type = type; }

    public boolean isDefault() { return isDefault; }
    public void setDefault(boolean aDefault) { isDefault = aDefault; }
}
