package com.example.appquanlychitieu.ui.common;

import android.view.View;

import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;

public final class EdgeToEdgeHelper {
    private EdgeToEdgeHelper() {}

    public static void applySystemBars(View root) {
        int start = root.getPaddingLeft();
        int top = root.getPaddingTop();
        int end = root.getPaddingRight();
        int bottom = root.getPaddingBottom();
        ViewCompat.setOnApplyWindowInsetsListener(root, (view, insets) -> {
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            view.setPadding(start + bars.left, top + bars.top, end + bars.right, bottom + bars.bottom);
            return insets;
        });
        ViewCompat.requestApplyInsets(root);
    }

    public static void applyImePadding(View view) {
        int start = view.getPaddingLeft();
        int top = view.getPaddingTop();
        int end = view.getPaddingRight();
        int bottom = view.getPaddingBottom();
        ViewCompat.setOnApplyWindowInsetsListener(view, (target, insets) -> {
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            target.setPadding(start, top, end, bottom + Math.max(ime.bottom, bars.bottom));
            return insets;
        });
        ViewCompat.requestApplyInsets(view);
    }

    public static void applySystemBarsAndIme(View root) {
        int start = root.getPaddingLeft();
        int top = root.getPaddingTop();
        int end = root.getPaddingRight();
        int bottom = root.getPaddingBottom();
        ViewCompat.setOnApplyWindowInsetsListener(root, (view, insets) -> {
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            view.setPadding(
                    start + bars.left,
                    top + bars.top,
                    end + bars.right,
                    bottom + Math.max(bars.bottom, ime.bottom));
            return insets;
        });
        ViewCompat.requestApplyInsets(root);
    }
}
