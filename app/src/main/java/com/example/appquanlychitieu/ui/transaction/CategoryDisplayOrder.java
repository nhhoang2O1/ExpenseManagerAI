package com.example.appquanlychitieu.ui.transaction;

import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;

import java.text.Collator;
import java.text.Normalizer;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;

/** Defines the user-facing order of categories in the transaction form. */
public final class CategoryDisplayOrder {
    private static final int NORMAL_PRIORITY = 100;
    private static final int OTHER_PRIORITY = 1_000;

    private CategoryDisplayOrder() {}

    public static List<CategoryDto> orderedCopy(List<CategoryDto> source, TransactionType type) {
        List<CategoryDto> result = source == null ? new ArrayList<>() : new ArrayList<>(source);
        Collator vietnamese = Collator.getInstance(new Locale("vi", "VN"));
        vietnamese.setStrength(Collator.PRIMARY);
        result.sort(Comparator
                .comparingInt((CategoryDto item) -> priority(item, type))
                .thenComparing(item -> item == null || item.name == null ? "" : item.name,
                        vietnamese)
                .thenComparing(item -> item == null || item.id == null ? "" : item.id));
        return result;
    }

    public static boolean isOther(CategoryDto category) {
        return category != null && "khac".equals(normalize(category.name));
    }

    private static int priority(CategoryDto category, TransactionType type) {
        String name = normalize(category == null ? null : category.name);
        if ("khac".equals(name)) return OTHER_PRIORITY;
        if (type == TransactionType.INCOME) {
            return "luong".equals(name) ? 0 : NORMAL_PRIORITY;
        }
        if ("an uong".equals(name)) return 0;
        if ("di chuyen".equals(name)) return 1;
        if ("mua sam".equals(name)) return 2;
        return NORMAL_PRIORITY;
    }

    private static String normalize(String value) {
        if (value == null) return "";
        return Normalizer.normalize(value.trim().toLowerCase(Locale.ROOT), Normalizer.Form.NFD)
                .replaceAll("\\p{M}+", "")
                .replace('đ', 'd')
                .replaceAll("\\s+", " ");
    }
}
