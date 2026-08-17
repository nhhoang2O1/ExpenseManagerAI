package com.example.appquanlychitieu.ui.transaction;

import static org.junit.Assert.assertEquals;

import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;

import org.junit.Test;

import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

public class CategoryDisplayOrderTest {
    @Test
    public void expensePrioritiesComeFirstAndOtherIsLast() {
        List<CategoryDto> ordered = CategoryDisplayOrder.orderedCopy(Arrays.asList(
                category("Khác"), category("Giáo dục"), category("Mua sắm"),
                category("Di chuyển"), category("Ăn uống")), TransactionType.EXPENSE);

        assertEquals(Arrays.asList("Ăn uống", "Di chuyển", "Mua sắm", "Giáo dục", "Khác"),
                names(ordered));
    }

    @Test
    public void salaryComesFirstAndOtherIsLastForIncome() {
        List<CategoryDto> ordered = CategoryDisplayOrder.orderedCopy(Arrays.asList(
                category("Khác"), category("Thưởng"), category("Lương"), category("Đầu tư")),
                TransactionType.INCOME);

        assertEquals("Lương", ordered.get(0).name);
        assertEquals("Khác", ordered.get(ordered.size() - 1).name);
    }

    private static CategoryDto category(String name) {
        CategoryDto value = new CategoryDto();
        value.id = name;
        value.name = name;
        return value;
    }

    private static List<String> names(List<CategoryDto> values) {
        return values.stream().map(item -> item.name).collect(Collectors.toList());
    }
}
