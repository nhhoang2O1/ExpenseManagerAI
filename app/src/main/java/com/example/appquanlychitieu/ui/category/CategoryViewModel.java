package com.example.appquanlychitieu.ui.category;

import android.app.Application;

import androidx.annotation.NonNull;
import androidx.lifecycle.AndroidViewModel;
import androidx.lifecycle.LiveData;
import androidx.lifecycle.MutableLiveData;

import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.example.appquanlychitieu.data.repository.RemoteCategoryRepository;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

public final class CategoryViewModel extends AndroidViewModel {
    private static final String[] CATEGORY_COLORS = {
            "#E11D48", "#16A34A", "#9333EA", "#CA8A04", "#0284C7"
    };

    private final RemoteCategoryRepository repository;
    private final MutableLiveData<List<CategoryDto>> categories = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<String> error = new MutableLiveData<>();

    public CategoryViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteCategoryRepository(application);
        refreshIncludingInactive();
    }

    public LiveData<List<CategoryDto>> getCategories() { return categories; }
    public LiveData<String> getError() { return error; }

    public void refresh() {
        repository.getCategories(null, new RemoteCallback<List<CategoryDto>>() {
            @Override public void onSuccess(List<CategoryDto> value) {
                categories.setValue(value == null ? new ArrayList<>() : value);
            }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }

    public void refreshIncludingInactive() {
        repository.getCategories(null, new RemoteCallback<List<CategoryDto>>() {
            @Override public void onSuccess(List<CategoryDto> value) {
                categories.setValue(value == null ? new ArrayList<>() : value);
            }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        }, true);
    }

    public void save(CategoryDto existing, String name, String type, String icon) {
        String color = existing == null ? chooseUnusedColor() : existing.color;
        if (color == null) {
            error.setValue("Đã dùng hết 5 màu mới cho danh mục");
            return;
        }
        CategoryRequestDto request = new CategoryRequestDto(name, type,
                color,
                icon, existing == null || existing.isActive);
        RemoteCallback<CategoryDto> callback = new RemoteCallback<CategoryDto>() {
            @Override public void onSuccess(CategoryDto value) { refresh(); }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        };
        if (existing == null) repository.create(request, callback);
        else repository.update(existing, request, callback);
    }

    private String chooseUnusedColor() {
        Set<String> usedColors = new HashSet<>();
        List<CategoryDto> currentCategories = categories.getValue();
        if (currentCategories != null) {
            for (CategoryDto category : currentCategories) {
                if (category.color != null && !category.color.trim().isEmpty()) {
                    usedColors.add(category.color.trim().toUpperCase(Locale.ROOT));
                }
            }
        }

        List<String> availableColors = new ArrayList<>();
        for (String color : CATEGORY_COLORS) {
            if (!usedColors.contains(color)) availableColors.add(color);
        }
        return availableColors.isEmpty() ? null : availableColors.get(0);
    }

    public void delete(CategoryDto category) {
        repository.delete(category, new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) { refresh(); }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }

    public void setActive(CategoryDto category, boolean active) {
        if (category == null) return;
        CategoryRequestDto request = new CategoryRequestDto(category.name, category.type,
                category.color, category.icon, active);
        repository.update(category, request, new RemoteCallback<CategoryDto>() {
            @Override public void onSuccess(CategoryDto value) { refreshIncludingInactive(); }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }
}
