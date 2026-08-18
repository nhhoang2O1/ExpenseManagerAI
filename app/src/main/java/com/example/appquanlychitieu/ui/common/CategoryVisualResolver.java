package com.example.appquanlychitieu.ui.common;

import android.content.Context;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.content.res.ColorStateList;
import android.widget.ImageView;

import androidx.annotation.ColorInt;
import androidx.appcompat.content.res.AppCompatResources;
import androidx.core.widget.ImageViewCompat;
import androidx.core.graphics.ColorUtils;

import com.example.appquanlychitieu.R;

import java.util.Locale;
import java.util.Set;

public final class CategoryVisualResolver {
    private static final String EMOJI_PREFIX = "emoji:";
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
    private static final int[] CUSTOM_CHART_COLORS = {
            0xFF0F766E, 0xFFDB2777, 0xFF7C3AED, 0xFF0284C7,
            0xFF65A30D, 0xFFB45309, 0xFF9333EA, 0xFF0E7490,
            0xFFC026D3, 0xFF15803D
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

    public static int resolveCustomChartColor(String categoryId, Set<Integer> usedColors) {
        int start = Math.floorMod(categoryId == null ? 0 : categoryId.hashCode(),
                CUSTOM_CHART_COLORS.length);
        for (int offset = 0; offset < CUSTOM_CHART_COLORS.length; offset++) {
            int candidate = CUSTOM_CHART_COLORS[(start + offset) % CUSTOM_CHART_COLORS.length];
            if (usedColors == null || usedColors.add(candidate)) return candidate;
        }
        return CUSTOM_CHART_COLORS[start];
    }

    public static boolean isDefaultCategoryName(String name) {
        if (name == null) return false;
        String value = name.trim().toLowerCase(Locale.ROOT);
        return value.equals("ăn uống") || value.equals("di chuyển") || value.equals("mua sắm")
                || value.equals("nhà ở") || value.equals("giải trí") || value.equals("sức khỏe")
                || value.equals("giáo dục") || value.equals("hóa đơn") || value.equals("khác")
                || value.equals("lương") || value.equals("quà tặng") || value.equals("đầu tư")
                || value.equals("làm thêm");
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

    public static boolean isEmojiIcon(String rawIcon) {
        return rawIcon != null && rawIcon.trim().startsWith(EMOJI_PREFIX)
                && isEmoji(extractEmoji(rawIcon));
    }

    public static boolean isEmoji(String value) {
        if (value == null) return false;
        String candidate = value.trim();
        if (candidate.isEmpty() || candidate.length() > 16) return false;
        for (int offset = 0; offset < candidate.length();) {
            int codePoint = candidate.codePointAt(offset);
            if ((codePoint >= 0x1F000 && codePoint <= 0x1FAFF)
                    || (codePoint >= 0x2300 && codePoint <= 0x23FF)
                    || (codePoint >= 0x2600 && codePoint <= 0x27BF)) return true;
            offset += Character.charCount(codePoint);
        }
        return false;
    }

    public static String toEmojiIcon(String emoji) {
        String candidate = emoji == null ? "" : emoji.trim();
        return isEmoji(candidate) ? EMOJI_PREFIX + candidate : "ic_other";
    }

    public static String extractEmoji(String rawIcon) {
        if (rawIcon == null) return "";
        String value = rawIcon.trim();
        return value.startsWith(EMOJI_PREFIX) ? value.substring(EMOJI_PREFIX.length()) : "";
    }

    public static Drawable resolveIconDrawable(Context context, String rawIcon, int sizePx) {
        if (!isEmojiIcon(rawIcon)) {
            Drawable drawable = AppCompatResources.getDrawable(context, resolveIcon(rawIcon));
            if (drawable != null) drawable.setBounds(0, 0, sizePx, sizePx);
            return drawable;
        }
        Bitmap bitmap = Bitmap.createBitmap(sizePx, sizePx, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(bitmap);
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.SUBPIXEL_TEXT_FLAG);
        paint.setTypeface(Typeface.DEFAULT);
        paint.setTextAlign(Paint.Align.CENTER);
        paint.setTextSize(sizePx * 0.82f);
        Paint.FontMetrics metrics = paint.getFontMetrics();
        float baseline = sizePx / 2f - (metrics.ascent + metrics.descent) / 2f;
        canvas.drawText(extractEmoji(rawIcon), sizePx / 2f, baseline, paint);
        BitmapDrawable drawable = new BitmapDrawable(context.getResources(), bitmap);
        drawable.setBounds(0, 0, sizePx, sizePx);
        return drawable;
    }

    public static void bindIcon(ImageView view, String rawIcon, @ColorInt int vectorTint) {
        if (isEmojiIcon(rawIcon)) {
            ImageViewCompat.setImageTintList(view, null);
            view.clearColorFilter();
            int size = Math.max(view.getLayoutParams().width, view.getLayoutParams().height);
            if (size <= 0) size = Math.round(24 * view.getResources().getDisplayMetrics().density);
            view.setImageDrawable(resolveIconDrawable(view.getContext(), rawIcon, size));
            return;
        }
        view.setImageResource(resolveIcon(rawIcon));
        ImageViewCompat.setImageTintList(view, ColorStateList.valueOf(vectorTint));
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
