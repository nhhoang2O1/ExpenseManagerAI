package com.example.appquanlychitieu.data.remote.dto;

public final class CategoryRequestDto {
    public final String name;
    public final String type;
    public final String color;
    public final String icon;

    public CategoryRequestDto(String name, String type, String color, String icon) {
        this.name = name;
        this.type = type;
        this.color = color;
        this.icon = icon;
    }
}
