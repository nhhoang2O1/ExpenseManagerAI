package com.example.appquanlychitieu.ui.common;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotEquals;

import org.junit.Test;

public class CategoryVisualResolverTest {
    @Test
    public void validColorIsParsedWithOpaqueAlpha() {
        assertEquals(0xFF2563EB,
                CategoryVisualResolver.resolveChartColor("category", "#2563EB"));
    }

    @Test
    public void invalidColorUsesStableCategoryFallback() {
        int first = CategoryVisualResolver.resolveChartColor("food-id", "invalid");
        int second = CategoryVisualResolver.resolveChartColor("food-id", null);
        assertEquals(first, second);
        assertNotEquals(first,
                CategoryVisualResolver.resolveChartColor("transport-id", null));
    }
}
