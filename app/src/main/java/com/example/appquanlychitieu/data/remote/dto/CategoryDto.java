package com.example.appquanlychitieu.data.remote.dto;

public class CategoryDto {
    public String id;
    public String name;
    public String type;
    public String color;
    public String icon;
    public long version = 1L;
    public boolean isActive = true;

    @Override
    public String toString() {
        return name == null ? "" : name;
    }
}
