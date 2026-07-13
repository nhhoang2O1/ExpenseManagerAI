package com.example.appquanlychitieu;

import android.app.Application;

import com.example.appquanlychitieu.util.ThemeManager;

public class ExpenseManagerApp extends Application {
    @Override
    public void onCreate() {
        super.onCreate();
        ThemeManager.applySavedTheme(this);
        
    }
}
