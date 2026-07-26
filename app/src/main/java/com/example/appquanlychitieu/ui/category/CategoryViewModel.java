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
import java.util.List;

public final class CategoryViewModel extends AndroidViewModel {
    private final RemoteCategoryRepository repository;
    private final MutableLiveData<List<CategoryDto>> categories = new MutableLiveData<>(new ArrayList<>());
    private final MutableLiveData<String> error = new MutableLiveData<>();

    public CategoryViewModel(@NonNull Application application) {
        super(application);
        repository = new RemoteCategoryRepository(application);
        refresh();
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

    public void save(CategoryDto existing, String name, String type) {
        CategoryRequestDto request = new CategoryRequestDto(name, type,
                existing == null ? "#607D8B" : existing.color,
                existing == null ? "ic_other" : existing.icon);
        RemoteCallback<CategoryDto> callback = new RemoteCallback<CategoryDto>() {
            @Override public void onSuccess(CategoryDto value) { refresh(); }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        };
        if (existing == null) repository.create(request, callback);
        else repository.update(existing, request, callback);
    }

    public void delete(CategoryDto category) {
        repository.delete(category, new RemoteCallback<Void>() {
            @Override public void onSuccess(Void value) { refresh(); }
            @Override public void onError(ApiError apiError) { error.setValue(apiError.getMessage()); }
        });
    }
}
