package com.example.appquanlychitieu.data.remote.dto;

import com.google.gson.annotations.SerializedName;

import java.math.BigDecimal;

public class TransactionDto {
    public String id;
    public BigDecimal amount;
    public String note;
    public String storeName;
    @SerializedName(value = "transactionDate", alternate = {"date"})
    public String transactionDate;
    public String type;
    public String categoryId;
    public String categoryName;
    public String categoryColor;
    public String categoryIcon;
    public CategoryDto category;

    public String resolvedCategoryName() {
        return category != null && category.name != null ? category.name : categoryName;
    }

    public String resolvedCategoryColor() {
        return category != null && category.color != null ? category.color : categoryColor;
    }

    public String resolvedCategoryIcon() {
        return category != null && category.icon != null ? category.icon : categoryIcon;
    }

    public String resolvedNote() {
        if (note != null && !note.trim().isEmpty()) {
            return note;
        }
        return storeName == null ? "" : storeName;
    }
}
