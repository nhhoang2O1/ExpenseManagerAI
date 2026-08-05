package com.example.appquanlychitieu.ui.common;

import android.content.Context;
import android.content.res.Configuration;
import android.graphics.Color;

import androidx.annotation.ColorInt;
import androidx.core.graphics.ColorUtils;

import com.example.appquanlychitieu.R;

import java.util.Locale;

public final class CategoryVisualResolver {
    private static final int[] FALLBACK_COLORS = {
            0xFF2563EB,
            0xFF0B6B53,
            0xFFD97706,
            0xFFD64555,
            0xFF7C3AED,
            0xFF0891B2,
            0xFFBE185D,
            0xFF475569
    };

    private CategoryVisualResolver() {}

    public static CategoryVisual resolve(Context context, String categoryId, String rawColor) {
        int base = parseColor(rawColor, fallback(categoryId));
        boolean dark = (context.getResources().getConfiguration().uiMode
                & Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES;
        int surface = context.getColor(R.color.surface);
        int container = ColorUtils.blendARGB(base, surface, dark ? 0.68f : 0.84f);
        int onBase = ColorUtils.calculateLuminance(base) > 0.46d ? Color.BLACK : Color.WHITE;
        int onContainer = ColorUtils.calculateLuminance(container) > 0.46d
                ? context.getColor(R.color.text_primary)
                : Color.WHITE;
        return new CategoryVisual(base, container, onBase, onContainer);
    }

    @ColorInt
    public static int resolveChartColor(String categoryId, String rawColor) {
        return parseColor(rawColor, fallback(categoryId));
    }

    public static int resolveIcon(String rawIcon) {
        if (rawIcon == null) return R.drawable.ic_other;
        switch (rawIcon.trim().toLowerCase(Locale.ROOT)) {
            case "ic_food": return R.drawable.ic_food;
            case "ic_transport": return R.drawable.ic_transport;
            case "ic_shopping": return R.drawable.ic_shopping;
            case "ic_house": return R.drawable.ic_house;
            case "ic_entertainment": return R.drawable.ic_entertainment;
            case "ic_health": return R.drawable.ic_health;
            case "ic_education": return R.drawable.ic_education;
            case "ic_bill": return R.drawable.ic_bill;
            case "ic_salary": return R.drawable.ic_salary;
            case "ic_gift": return R.drawable.ic_gift;
            case "ic_invest": return R.drawable.ic_invest;
            case "ic_freelance": return R.drawable.ic_freelance;
            default: return R.drawable.ic_other;
        }
    }

    private static int parseColor(String rawColor, int fallback) {
        if (rawColor == null) return fallback;
        String value = rawColor.trim().toUpperCase(Locale.ROOT);
        if (!value.matches("#[0-9A-F]{6}([0-9A-F]{2})?")) return fallback;
        try {
            long parsed = Long.parseLong(value.substring(1), 16);
            if (value.length() == 7) parsed |= 0xFF000000L;
            return (int) parsed;
        } catch (NumberFormatException ignored) { return fallback; }
    }

    private static int fallback(String categoryId) {
        int hash = categoryId == null ? 0 : categoryId.hashCode();
        return FALLBACK_COLORS[Math.floorMod(hash, FALLBACK_COLORS.length)];
    }

    public static final class CategoryVisual {
        public final int baseColor;
        public final int containerColor;
        public final int onBaseColor;
        public final int onContainerColor;

        CategoryVisual(int baseColor, int containerColor, int onBaseColor, int onContainerColor) {
            this.baseColor = baseColor;
            this.containerColor = containerColor;
            this.onBaseColor = onBaseColor;
            this.onContainerColor = onContainerColor;
        }
    }
}
